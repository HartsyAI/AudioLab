using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to the drum pattern; every edit answers with what it came to, so a caller can follow it.</summary>
public sealed class BeatActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Adds a lane at the end of the pattern.</summary>
    public Func<string?, Task<EditResult>> AddLane { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Removes a lane from the pattern.</summary>
    public Func<BeatLaneId, Task<EditResult>> RemoveLane { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Moves a lane one place up or down.</summary>
    public Func<BeatLaneId, MoveDirection, Task<EditResult>> MoveLane { get; init; } =
        (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Renames a lane.</summary>
    public Func<BeatLaneId, string, Task<EditResult>> RenameLane { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets the swing of the pattern.</summary>
    public Func<double, Task<EditResult>> SetSwing { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets the step count of the pattern.</summary>
    public Func<int, Task<EditResult>> SetSteps { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static BeatActions For(BeatEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new BeatActions
        {
            AddLane = name => show(editor.AddLaneAsync(name)),
            RemoveLane = lane => show(editor.RemoveLaneAsync(lane)),
            MoveLane = (lane, direction) => show(editor.MoveLaneAsync(lane, direction)),
            RenameLane = (lane, name) => show(editor.RenameLaneAsync(lane, name)),
            SetSwing = swing => show(editor.SetSwingAsync(swing)),
            SetSteps = steps => show(editor.SetStepsAsync(steps)),
        };
    }
}
