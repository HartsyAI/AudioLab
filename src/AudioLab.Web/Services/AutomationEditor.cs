using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Session;

namespace AudioLab.Web.Services;

/// <summary>The edits of a track's automation lanes the session screen makes, each one command sent by wire name; refusals come back as words.</summary>
public sealed class AutomationEditor(LiveProject live)
{
    /// <summary>Adds a point to a lane at a time and value; the server's rule places it.</summary>
    public Task<EditResult> AddPointAsync(TrackId track, AutomationParameter parameter, double time, double value)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new AddAutomationPoint { Project = project, Track = track, Parameter = parameter, Time = time, Value = value }));
    }

    /// <summary>Moves a point to an absolute time and value, as one gesture, so one undo step.</summary>
    public Task<EditResult> MovePointAsync(TrackId track, AutomationParameter parameter, AutomationMoveTarget target)
    {
        return EditRunner.RunAsync(
            live,
            async project =>
            {
                string gesture = live.NewGesture();
                try
                {
                    return await live.SendAsync(
                        new MoveAutomationPoint
                        {
                            Project = project,
                            Track = track,
                            Parameter = parameter,
                            Index = target.Index,
                            Time = target.Time,
                            Value = target.Value,
                        },
                        gesture);
                }
                finally
                {
                    await live.EndGestureAsync(gesture);
                }
            });
    }

    /// <summary>Removes the point at an index from a lane.</summary>
    public Task<EditResult> DeletePointAsync(TrackId track, AutomationParameter parameter, int index)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new DeleteAutomationPoint { Project = project, Track = track, Parameter = parameter, Index = index }));
    }

    /// <summary>Folds a recorded pass into a lane and, when a mode is given, sets it after the pass: one gesture, so one undo step (D25).</summary>
    public Task<EditResult> CommitPassAsync(TrackId track, AutomationParameter parameter, AutomationPass pass, AutomationMode? mode)
    {
        return EditRunner.RunAsync(
            live,
            async project =>
            {
                string gesture = live.NewGesture();
                try
                {
                    CommandReply committed = await live.SendAsync(
                        new CommitAutomationWrite
                        {
                            Project = project,
                            Track = track,
                            Parameter = parameter,
                            Points = [.. pass.Points],
                            ReturnToOld = pass.ReturnToOld,
                        },
                        gesture);
                    if (!committed.Succeeded || mode is not { } chosen)
                    {
                        return committed;
                    }
                    return await live.SendAsync(new SetAutomationMode { Project = project, Track = track, Mode = chosen }, gesture);
                }
                finally
                {
                    await live.EndGestureAsync(gesture);
                }
            });
    }

    /// <summary>Sets how a track's lanes behave during playback.</summary>
    public Task<EditResult> SetModeAsync(TrackId track, AutomationMode mode)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetAutomationMode { Project = project, Track = track, Mode = mode }));
    }

    /// <summary>Shows or hides a track's lane and chooses the parameter it shows; a null leaves that one as it is.</summary>
    public Task<EditResult> SetViewAsync(TrackId track, bool? visible, AutomationParameter? parameter)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new SetAutomationView { Project = project, Track = track, Visible = visible, Parameter = parameter }));
    }
}
