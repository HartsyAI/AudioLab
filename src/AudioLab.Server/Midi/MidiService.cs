using System.Buffers;
using AudioLab.Audio.Midi;
using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Routes;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Midi;

/// <summary>Turns a .mid file into a MIDI clip of a user's project, and a clip or a track of it back into a .mid file.</summary>
/// <remarks>
/// An import is one command, <c>midi_clip_import</c>, applied in process through the same pipeline as every edit: one undo step, the
/// project's size limit, an announcement to every client of the project. The file never travels as JSON, so the command limits
/// of the hub and MCP do not bound it; <see cref="MidiOptions.MaxFileBytes"/> and the project's own limits do. The project's tempo
/// is left as it is: the notes keep the seconds the file's own tempo map gave them.
/// </remarks>
public sealed class MidiService(
    ProjectEditService edits,
    OpenProjects open,
    ProjectReader reader,
    [FromKeyedServices(GateKeys.Midi)] ConcurrencyGate gate,
    MidiOptions options,
    ILogger<MidiService> logger)
{
    private const int ReadBlockBytes = 64 * 1024;
    private const int MaxNamedClips = 8;

    /// <summary>Reads the file from <paramref name="body"/> and puts its notes on <paramref name="track"/> as a new clip.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="project">The project to import into.</param>
    /// <param name="track">The instrument track that gets the clip.</param>
    /// <param name="start">Where the clip starts on the timeline, in seconds.</param>
    /// <param name="name">The clip's name, or null for the next "MIDI N".</param>
    /// <param name="body">The bytes of the file.</param>
    /// <param name="cancellationToken">Cancels the import before the clip is added.</param>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="GateBusyException">The user or the server is running as many imports as are allowed.</exception>
    /// <exception cref="MidiRefusedException">The file is not a MIDI file the codec accepts, or the command refused it.</exception>
    public async Task<MidiImportResult> ImportAsync(
        UserId user,
        ProjectId project,
        TrackId track,
        double start,
        string? name,
        Stream body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!double.IsFinite(start) || start < 0 || start > ProjectLimits.MaxSeconds)
        {
            throw new MidiRefusedException(CommandErrorCode.Invalid, "start must be a time from 0 seconds.");
        }
        using IDisposable lease = gate.Enter(user);
        // Before the body is read, so that a project that is not the user's costs nothing.
        _ = await open.OpenAsync(user, project, cancellationToken);
        byte[] bytes = await ReadAsync(body, cancellationToken);
        MidiImport import = await ParseAsync(bytes, cancellationToken);
        ImportMidiClip command = new()
        {
            Project = project,
            Track = track,
            Start = start,
            Length = import.Length,
            Name = name,
            Notes = [.. import.Notes.Select(note => new ImportedNote(note.Pitch, note.Start, note.Duration, note.Velocity))],
            ControlChanges = [.. import.ControlChanges.Select(change => new ImportedControlChange(change.Time, change.Controller, change.Value))],
        };
        ClientId client = ClientId.From("import-" + Guid.NewGuid().ToString("N"));
        CommandReply reply = await edits.ApplyCommandAsync(user, project, client, command, "midi_clip_import", cancellationToken);
        if (!reply.Succeeded)
        {
            HubCommandError error = reply.Errors[0];
            throw new MidiRefusedException(error.Code, error.Message);
        }
        return new MidiImportResult(
            reply.Created[0],
            reply.Revision,
            command.Notes.Count,
            command.ControlChanges.Count,
            import.Length,
            import.Bpm,
            import.TempoChanges.Count,
            import.IgnoredNoteOffs,
            import.UnclosedNotes);
    }

    /// <summary>Writes a MIDI clip of the project as a format 0 file.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="MidiRefusedException">There is no such MIDI clip, or the clip cannot be written to a file.</exception>
    public async Task<MidiExport> ExportClipAsync(UserId user, ProjectId project, ClipId clip, CancellationToken cancellationToken)
    {
        using IDisposable lease = gate.Enter(user);
        ProjectDocument document = await reader.CurrentAsync(user, project, cancellationToken);
        MidiClip found = document.Tracks.SelectMany(track => track.MidiClips).FirstOrDefault(candidate => candidate.Id == clip)
            ?? throw new MidiRefusedException(CommandErrorCode.NotFound, "There is no such MIDI clip.");
        return await WriteAsync(document, found, found.Name, cancellationToken);
    }

    /// <summary>Writes the MIDI clip of a track as a format 0 file; a track with none or with several is refused, naming its clips.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="MidiRefusedException">There is no such track, or it does not have exactly one MIDI clip.</exception>
    public async Task<MidiExport> ExportTrackAsync(UserId user, ProjectId project, TrackId track, CancellationToken cancellationToken)
    {
        using IDisposable lease = gate.Enter(user);
        ProjectDocument document = await reader.CurrentAsync(user, project, cancellationToken);
        Track found = document.Tracks.FirstOrDefault(candidate => candidate.Id == track)
            ?? throw new MidiRefusedException(CommandErrorCode.NotFound, "There is no such track.");
        if (found.MidiClips.Count == 0)
        {
            throw new MidiRefusedException(CommandErrorCode.Rejected, "The track has no MIDI clip to export.");
        }
        if (found.MidiClips.Count > 1)
        {
            string ids = string.Join(", ", found.MidiClips.Take(MaxNamedClips).Select(clip => clip.Id.Value));
            string more = found.MidiClips.Count > MaxNamedClips ? ", ..." : string.Empty;
            throw new MidiRefusedException(
                CommandErrorCode.Rejected,
                $"The track has {found.MidiClips.Count} MIDI clips and a .mid file holds one; export a clip by its id: {ids}{more}.");
        }
        return await WriteAsync(document, found.MidiClips[0], found.Name, cancellationToken);
    }

    private static async Task<MidiExport> WriteAsync(ProjectDocument document, MidiClip clip, string name, CancellationToken cancellationToken)
    {
        try
        {
            // On a worker: a clip of a million notes is a long write.
            byte[] bytes = await Task.Run(() => MidiFile.Write(clip, document.Bpm, document.TempoMap, document.TimeSignature), cancellationToken);
            return new MidiExport(bytes, AttachmentResponse.FileName(name, "MIDI clip", ".mid"));
        }
        catch (MidiFormatException exception)
        {
            throw new MidiRefusedException(CommandErrorCode.Rejected, exception.Message);
        }
    }

    private async Task<MidiImport> ParseAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => MidiFile.Read(bytes), cancellationToken);
        }
        catch (MidiFormatException exception)
        {
            MidiLog.FileRefused(logger, bytes.Length, exception.Message);
            throw new MidiRefusedException(CommandErrorCode.Invalid, $"Not an accepted MIDI file: {exception.Message}");
        }
    }

    // The limit is counted on the bytes that arrive, whatever length the client announced.
    private async Task<byte[]> ReadAsync(Stream body, CancellationToken cancellationToken)
    {
        using MemoryStream file = new();
        byte[] block = ArrayPool<byte>.Shared.Rent(ReadBlockBytes);
        try
        {
            int read;
            while ((read = await body.ReadAsync(block.AsMemory(0, ReadBlockBytes), cancellationToken)) > 0)
            {
                if (file.Length + read > options.MaxFileBytes)
                {
                    throw new MidiRefusedException(
                        CommandErrorCode.Limit,
                        $"The file is larger than the {options.MaxFileBytes} bytes the server accepts.");
                }
                file.Write(block, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(block);
        }
        return file.ToArray();
    }
}
