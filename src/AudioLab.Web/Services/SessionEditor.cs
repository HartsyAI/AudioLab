using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>The edits the session screen makes, each one command sent by wire name; the server decides and its reasons come back as words.</summary>
public sealed class SessionEditor(LiveProject live)
{
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

    private Task<EditResult> RunAsync(Func<ProjectId, Task<CommandReply>> send) => EditRunner.RunAsync(live, send);
}
