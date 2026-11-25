using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.SignalR;
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

    /// <summary>Folds a recorded pass into a lane, one commit per segment, then sets the mode when one is given: one gesture, one undo step (D25).</summary>
    /// <remarks>
    /// A refusal after some segments landed does not take them back, since undo is shared by every client. The reason says how many landed, so an
    /// edit that partly applied is not reported as one that did nothing; a refused mode change after the commits says the lane was written.
    /// </remarks>
    public async Task<EditResult> CommitPassAsync(TrackId track, AutomationParameter parameter, AutomationPass pass, AutomationMode? mode)
    {
        if (live.Document is not { } document)
        {
            return EditResult.Refused("The project is not open yet.");
        }
        string gesture = live.NewGesture();
        int landed = 0;
        try
        {
            foreach (IReadOnlyList<WrittenPoint> segment in pass.Segments)
            {
                CommandReply reply = await live.SendAsync(
                    new CommitAutomationWrite
                    {
                        Project = document.Id,
                        Track = track,
                        Parameter = parameter,
                        Points = [.. segment],
                        ReturnToOld = pass.ReturnToOld,
                    },
                    gesture);
                if (!reply.Succeeded)
                {
                    string why = CommandRefusal.Describe(reply);
                    return EditResult.Refused(landed == 0 ? why : $"{landed} of {pass.Segments.Count} recorded parts were written; the rest was refused: {why}");
                }
                landed++;
            }
            if (mode is not { } chosen)
            {
                return EditResult.Applied(changed: true, []);
            }
            CommandReply moded = await live.SendAsync(new SetAutomationMode { Project = document.Id, Track = track, Mode = chosen }, gesture);
            return moded.Succeeded
                ? EditResult.Applied(changed: true, [])
                : EditResult.Refused("The pass was written; its mode was not changed: " + CommandRefusal.Describe(moded));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HubException or OperationCanceledException)
        {
            return EditResult.Refused(landed == 0
                ? "The connection to the server was lost, so the pass was not written."
                : $"{landed} of {pass.Segments.Count} recorded parts were written before the connection was lost.");
        }
        finally
        {
            await live.EndGestureAsync(gesture);
        }
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
