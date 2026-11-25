using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>The sends of a track to an aux bus, each one command sent by wire name; the server decides and its reasons come back as words.</summary>
public sealed class SendEditor(LiveProject live)
{
    /// <summary>Routes a track to a bus at the server's starting level, after the fader.</summary>
    public Task<EditResult> AddAsync(TrackId track, TrackId bus)
    {
        return RunAsync(project => live.SendAsync(new AddSend { Project = project, Track = track, Bus = bus }));
    }

    /// <summary>Removes a track's send to a bus.</summary>
    public Task<EditResult> RemoveAsync(TrackId track, TrackId bus)
    {
        return RunAsync(project => live.SendAsync(new RemoveSend { Project = project, Track = track, Bus = bus }));
    }

    /// <summary>Sets the level of a send to an absolute gain, as one gesture: a drag is one undo step.</summary>
    public Task<EditResult> SetLevelAsync(TrackId track, TrackId bus, double level)
    {
        return GestureAsync((project, gesture) => live.SendAsync(new SetSendLevel { Project = project, Track = track, Bus = bus, Level = level }, gesture));
    }

    /// <summary>Mutes or unmutes a send.</summary>
    public Task<EditResult> SetMutedAsync(TrackId track, TrackId bus, bool muted)
    {
        return RunAsync(project => live.SendAsync(new SetSendMuted { Project = project, Track = track, Bus = bus, Muted = muted }));
    }

    /// <summary>Takes a send before (true) or after the track's fader.</summary>
    public Task<EditResult> SetPreAsync(TrackId track, TrackId bus, bool pre)
    {
        return RunAsync(project => live.SendAsync(new SetSendPre { Project = project, Track = track, Bus = bus, Pre = pre }));
    }

    private Task<EditResult> RunAsync(Func<ProjectId, Task<CommandReply>> send) => EditRunner.RunAsync(live, send);

    private Task<EditResult> GestureAsync(Func<ProjectId, string, Task<CommandReply>> send)
    {
        return RunAsync(async project =>
        {
            string gesture = live.NewGesture();
            try
            {
                return await send(project, gesture);
            }
            finally
            {
                await live.EndGestureAsync(gesture);
            }
        });
    }
}
