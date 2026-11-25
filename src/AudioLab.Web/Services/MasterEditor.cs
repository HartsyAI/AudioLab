using AudioLab.Core.Commands;

namespace AudioLab.Web.Services;

/// <summary>The master's edits: its fader and its limiter, each one command sent by wire name; the server decides and its reasons come back as words.</summary>
public sealed class MasterEditor(LiveProject live)
{
    /// <summary>Sets the master fader to an absolute gain, as one gesture: a drag is one undo step.</summary>
    public Task<EditResult> SetVolumeAsync(double gain)
    {
        return EditRunner.RunInGestureAsync(live, (project, gesture) => live.SendAsync(new SetMasterVolume { Project = project, Volume = gain }, gesture));
    }

    /// <summary>Switches the master limiter on or off.</summary>
    public Task<EditResult> SetLimiterAsync(bool enabled)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetMasterLimiter { Project = project, Enabled = enabled }));
    }
}
