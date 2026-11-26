using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Services;

/// <summary>The drum pattern edits of the session screen: one command each, with refusals in the server's words.</summary>
/// <param name="live">The open project.</param>
public sealed class BeatEditor(LiveProject live)
{
    /// <summary>Adds a lane at the end of the pattern; its id is in <see cref="EditResult.Created"/>.</summary>
    public Task<EditResult> AddLaneAsync(string? name)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new AddBeatLane { Project = project, Name = name }));
    }

    /// <summary>Removes a lane from the pattern.</summary>
    public Task<EditResult> RemoveLaneAsync(BeatLaneId lane)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new RemoveBeatLane { Project = project, Lane = lane }));
    }

    /// <summary>Moves a lane one place up or down; at either end the server changes nothing.</summary>
    public Task<EditResult> MoveLaneAsync(BeatLaneId lane, MoveDirection direction)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new MoveBeatLane { Project = project, Lane = lane, Direction = direction }));
    }

    /// <summary>Renames a lane.</summary>
    public Task<EditResult> RenameLaneAsync(BeatLaneId lane, string name)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new RenameBeatLane { Project = project, Lane = lane, Name = name }));
    }

    /// <summary>Sets the swing of the pattern to an absolute value, from 0 to 1.</summary>
    public Task<EditResult> SetSwingAsync(double swing)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetBeatSwing { Project = project, Swing = swing }));
    }

    /// <summary>Sets how many steps the pattern has, up to the most a pattern may have.</summary>
    public Task<EditResult> SetStepsAsync(int steps)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetBeatSteps { Project = project, Steps = steps }));
    }

    /// <summary>Makes the given steps of a lane fire, or silences them, to an absolute state; the others stay as they are.</summary>
    /// <param name="lane">The lane to edit.</param>
    /// <param name="steps">The steps to set, each counted from 0.</param>
    /// <param name="on">True to make the steps fire, false to silence them.</param>
    /// <param name="gesture">The gesture of a paint drag, so its commands are one undo step; null for a single edit.</param>
    public Task<EditResult> SetLaneStepsAsync(BeatLaneId lane, IReadOnlyList<int> steps, bool on, string? gesture = null)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new SetBeatLaneSteps { Project = project, Lane = lane, Steps = [.. steps], On = on }, gesture));
    }

    /// <summary>Sets the gain of a lane to an absolute value, from 0 up to the most a lane may have; 1 is unity.</summary>
    public Task<EditResult> SetLaneGainAsync(BeatLaneId lane, double gain)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetBeatLaneGain { Project = project, Lane = lane, Gain = gain }));
    }

    /// <summary>Opens a gesture for a paint drag; every command sent with it is one undo step once it ends.</summary>
    public string BeginStroke() => live.NewGesture();

    /// <summary>Closes the gesture of a paint drag.</summary>
    public Task EndStrokeAsync(string gesture) => live.EndGestureAsync(gesture);
}
