using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Services;

/// <summary>The edits the session screen makes, each one command sent by wire name; the server decides and its reasons come back as words.</summary>
public sealed class SessionEditor(LiveProject live)
{
    private const string Lost = "The connection to the server was lost, so the edit was not sent.";

    /// <summary>Adds an empty track at the bottom; the new track's id is in <see cref="EditResult.Created"/>.</summary>
    public Task<EditResult> AddTrackAsync(TrackKind kind = TrackKind.Audio)
    {
        return RunAsync(project => live.SendAsync(new AddTrack { Project = project, Kind = kind }));
    }

    /// <summary>Renames a track.</summary>
    public Task<EditResult> RenameTrackAsync(TrackId track, string name)
    {
        return RunAsync(project => live.SendAsync(new RenameTrack { Project = project, Track = track, Name = name }));
    }

    /// <summary>Mutes or unmutes a track.</summary>
    public Task<EditResult> SetMutedAsync(TrackId track, bool muted)
    {
        return RunAsync(project => live.SendAsync(new SetTrackMuted { Project = project, Track = track, Muted = muted }));
    }

    /// <summary>Solos or unsolos a track.</summary>
    public Task<EditResult> SetSoloedAsync(TrackId track, bool soloed)
    {
        return RunAsync(project => live.SendAsync(new SetTrackSoloed { Project = project, Track = track, Soloed = soloed }));
    }

    /// <summary>Turns the project's metronome on or off, for everyone in it.</summary>
    public Task<EditResult> SetMetronomeAsync(bool on)
    {
        return RunAsync(project => live.SendAsync(new SetMetronome { Project = project, Enabled = on }));
    }

    /// <summary>Moves a clip to an absolute start, and to another track when one is given: one gesture, one command, one undo step.</summary>
    public Task<EditResult> MoveClipAsync(ClipId clip, double start, TrackId? track)
    {
        return RunAsync(async project =>
        {
            string gesture = live.NewGesture();
            try
            {
                return await live.SendAsync(new MoveClip { Project = project, Clip = clip, Start = start, Track = track }, gesture);
            }
            finally
            {
                await live.EndGestureAsync(gesture);
            }
        });
    }

    /// <summary>Goes back one undo step for everyone in the project.</summary>
    public Task<EditResult> UndoAsync() => RunAsync(_ => live.UndoAsync());

    /// <summary>Goes forward one redo step for everyone in the project.</summary>
    public Task<EditResult> RedoAsync() => RunAsync(_ => live.RedoAsync());

    private async Task<EditResult> RunAsync(Func<ProjectId, Task<CommandReply>> send)
    {
        if (live.Document is not { } document)
        {
            return EditResult.Refused("The project is not open yet.");
        }
        try
        {
            CommandReply reply = await send(document.Id);
            return reply.Succeeded ? EditResult.Applied(reply.Changed, reply.Created) : EditResult.Refused(CommandRefusal.Describe(reply));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HubException or OperationCanceledException)
        {
            return EditResult.Refused(exception is HubException hub && !string.IsNullOrWhiteSpace(hub.Message) ? hub.Message : Lost);
        }
    }
}
