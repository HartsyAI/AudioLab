using AudioLab.Audio;
using AudioLab.Audio.Instruments;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Routes;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Render;

/// <summary>Exports a user's project as a WAV mixdown or a ZIP of stems; every check that can come before the first byte is made here.</summary>
/// <remarks>
/// The project is the user's own or not found, and what it names of media is the user's own or refused, so an export never touches
/// another user's files. The document is the open project's current state, including changes not yet saved, or the stored file.
/// </remarks>
public sealed class ExportService(
    OpenProjects open,
    ProjectReader reader,
    RenderMediaProvider media,
    [FromKeyedServices(GateKeys.Export)] ConcurrencyGate gate,
    RenderOptions options,
    TimeProvider time,
    ILogger<ExportService> logger)
{
    private const string ZipType = "application/zip";
    private const string WavType = "audio/wav";

    private static readonly int[] _rates = [44100, 48000, 88200, 96000];

    /// <summary>Checks the request and the project, takes a place in the gate and measures the file; nothing is written yet.</summary>
    /// <exception cref="ExportRefusedException">The request is wrong, the server is busy, media is missing or the export is too large.</exception>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The stored project is not valid.</exception>
    /// <exception cref="IOException">The project store or the user's media cannot be read.</exception>
    public async Task<PreparedExport> PrepareAsync(
        UserId user,
        ProjectId project,
        ExportKind kind,
        ExportRequest? body,
        CancellationToken requestAborted)
    {
        ExportRequest request = body ?? new ExportRequest();
        CheckShape(request, kind);
        IDisposable lease = Enter(user);
        CancellationTokenSource limit = new(options.MaxTime, time);
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, limit.Token);
        bool prepared = false;
        try
        {
            ProjectDocument document = await reader.CurrentAsync(user, project, linked.Token);
            CheckTracks(document, request);
            await RequireMediaAsync(document, linked.Token);
            RenderMedia renderMedia = await media.OpenAsync(user, request.SampleRate, linked.Token);
            PreparedExport export = await MeasureAsync(document, kind, request, renderMedia, new Hold(lease, limit, linked, requestAborted));
            prepared = true;
            return export;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !requestAborted.IsCancellationRequested)
        {
            throw new ExportRefusedException(ExportRefusal.TimedOut, "The export took longer than the server allows; ask for a shorter range.");
        }
        finally
        {
            if (!prepared)
            {
                lease.Dispose();
                linked.Dispose();
                limit.Dispose();
            }
        }
    }

    private async Task<PreparedExport> MeasureAsync(
        ProjectDocument document,
        ExportKind kind,
        ExportRequest request,
        RenderMedia renderMedia,
        Hold hold)
    {
        WavOptions wav = new() { Format = FormatOf(request.Format), Dither = DitherOf(request.Dither), Seed = request.Seed };
        OfflineOptions offline = OfflineOf(request, renderMedia);
        IReadOnlyList<TrackId>? tracks = request.Tracks;
        ExportSize size;
        try
        {
            // On a worker, as the write is: planning decodes the files the project plays.
            size = await Task.Run(
                () => kind == ExportKind.Mix
                    ? WavExporter.Measure(document, renderMedia.Source, wav, offline)
                    : StemExporter.Measure(document, renderMedia.Source, wav, offline, tracks),
                hold.Linked.Token);
        }
        catch (MissingMediaException exception)
        {
            string message = "A clip or an instrument of the project plays a media file that is not available.";
            throw new ExportRefusedException(ExportRefusal.MissingMedia, message, [.. exception.Missing.Select(id => id.Value)]);
        }
        catch (Exception exception) when (exception is ExportTooLargeException or RenderLimitException)
        {
            throw new ExportRefusedException(ExportRefusal.TooLarge, exception.Message);
        }
        CheckSize(size, request.SampleRate);
        string baseName = AttachmentResponse.FileName(document.Name, "Project", string.Empty);
        if (kind == ExportKind.Mix)
        {
            Task<WavResult> Write(Stream stream, CancellationToken token) =>
                WavExporter.WriteAsync(document, renderMedia.Source, stream, wav, offline, token);
            return new PreparedExport(WavType, $"{baseName}.wav", size.Bytes, Write, hold, logger);
        }
        async Task<WavResult> WriteZip(Stream stream, CancellationToken token)
        {
            await StemExporter.WriteZipAsync(document, renderMedia.Source, stream, wav, offline, tracks, token);
            return default;
        }
        return new PreparedExport(ZipType, $"{baseName} stems.zip", size.Bytes, WriteZip, hold, logger);
    }

    private IDisposable Enter(UserId user)
    {
        try
        {
            return gate.Enter(user);
        }
        catch (GateBusyException exception)
        {
            throw new ExportRefusedException(ExportRefusal.Busy, exception.Message);
        }
    }

    private void CheckSize(ExportSize size, int sampleRate)
    {
        if (size.Files == 0 || size.Frames == 0)
        {
            throw new ExportRefusedException(ExportRefusal.Empty, "The project has no sound in the range asked for, or no track to make a stem of.");
        }
        if (size.Frames / (double)sampleRate > options.MaxSeconds)
        {
            throw new ExportRefusedException(
                ExportRefusal.TooLarge,
                $"The export would be longer than the {options.MaxSeconds:0} seconds the server allows; ask for a shorter range.");
        }
        if (size.Bytes > options.MaxBytes)
        {
            throw new ExportRefusedException(
                ExportRefusal.TooLarge,
                $"The export would be {size.Bytes} bytes, more than the {options.MaxBytes} the server allows; "
                + "ask for a shorter range or a smaller format.");
        }
    }

    private async Task RequireMediaAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        try
        {
            await open.RequireMediaAsync(document, cancellationToken);
        }
        catch (ProjectFormatException exception)
        {
            throw new ExportRefusedException(
                ExportRefusal.MissingMedia,
                "The project names media files that are missing or no longer match.",
                exception.Problems);
        }
    }

    private void CheckShape(ExportRequest request, ExportKind kind)
    {
        if (!_rates.Contains(request.SampleRate))
        {
            Bad($"sampleRate must be one of {string.Join(", ", _rates)}.");
        }
        if (!Enum.IsDefined(request.Format) || !Enum.IsDefined(request.Dither))
        {
            Bad("format or dither is not a known value.");
        }
        if (!double.IsFinite(request.Start) || request.Start < 0 || request.Start > ProjectLimits.MaxSeconds)
        {
            Bad("start must be a time of at least 0 seconds.");
        }
        if (request.End is { } end && (!double.IsFinite(end) || end <= request.Start || end > ProjectLimits.MaxSeconds))
        {
            Bad("end must be a time after the start.");
        }
        if (request.TailSeconds is { } tail)
        {
            if (!request.Tail)
            {
                Bad("tailSeconds needs tail to be true.");
            }
            if (!double.IsFinite(tail) || tail < 0 || tail > options.MaxTailSeconds)
            {
                Bad($"tailSeconds must be from 0 to {options.MaxTailSeconds:0} seconds.");
            }
        }
        if (kind == ExportKind.Mix && request.Tracks is not null)
        {
            Bad("tracks are for a stem export only.");
        }
        if (request.Tracks is { } tracks && (tracks.Count == 0 || tracks.Distinct().Count() != tracks.Count))
        {
            Bad("tracks must name each track once, and at least one.");
        }
    }

    private static void CheckTracks(ProjectDocument document, ExportRequest request)
    {
        if (request.Tracks is not { } tracks)
        {
            return;
        }
        HashSet<TrackId> known = [.. document.Tracks.Select(track => track.Id)];
        string[] unknown = [.. tracks.Where(id => !known.Contains(id)).Select(id => id.Value)];
        if (unknown.Length > 0)
        {
            throw new ExportRefusedException(ExportRefusal.BadOptions, "tracks names a track the project does not have.", unknown);
        }
    }

    private static void Bad(string message) => throw new ExportRefusedException(ExportRefusal.BadOptions, message);

    private static OfflineOptions OfflineOf(ExportRequest request, RenderMedia renderMedia)
    {
        return new OfflineOptions
        {
            SampleRate = request.SampleRate,
            Start = request.Start,
            End = request.End,
            IncludeTail = request.Tail,
            TailFrames = request.TailSeconds is { } tail ? (long)Math.Round(tail * request.SampleRate) : null,
            Sources = new InstrumentSourceFactory(),
            Cache = renderMedia.Cache,
        };
    }

    private static WavFormat FormatOf(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Pcm16 => WavFormat.Pcm16,
            ExportFormat.Pcm24 => WavFormat.Pcm24,
            _ => WavFormat.Float32,
        };
    }

    private static DitherKind DitherOf(ExportDither dither)
    {
        return dither switch
        {
            ExportDither.None => DitherKind.None,
            ExportDither.Rectangular => DitherKind.Rectangular,
            _ => DitherKind.Triangular,
        };
    }
}
