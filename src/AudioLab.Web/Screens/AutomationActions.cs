using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to a track's automation lane; every edit answers with what it came to, or why not.</summary>
public sealed class AutomationActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Adds a point to a lane.</summary>
    public Func<TrackId, AutomationParameter, double, double, Task<EditResult>> Add { get; init; } =
        (_, _, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Moves a point of a lane to an absolute time and value.</summary>
    public Func<TrackId, AutomationParameter, AutomationMoveTarget, Task<EditResult>> Move { get; init; } =
        (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Removes a point of a lane.</summary>
    public Func<TrackId, AutomationParameter, int, Task<EditResult>> Delete { get; init; } =
        (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets a track's automation mode.</summary>
    public Func<TrackId, AutomationMode, Task<EditResult>> SetMode { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Shows or hides a track's lane (a null leaves it) and chooses its parameter (a null leaves it).</summary>
    public Func<TrackId, bool?, AutomationParameter?, Task<EditResult>> SetView { get; init; } =
        (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Folds a recorded pass into a lane, and sets the mode after it when one is given (D25).</summary>
    public Func<TrackId, AutomationParameter, AutomationPass, AutomationMode?, Task<EditResult>> CommitPass { get; init; } =
        (_, _, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static AutomationActions For(AutomationEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new AutomationActions
        {
            Add = (track, parameter, time, value) => show(editor.AddPointAsync(track, parameter, time, value)),
            Move = (track, parameter, target) => show(editor.MovePointAsync(track, parameter, target)),
            Delete = (track, parameter, index) => show(editor.DeletePointAsync(track, parameter, index)),
            SetMode = (track, mode) => show(editor.SetModeAsync(track, mode)),
            SetView = (track, visible, parameter) => show(editor.SetViewAsync(track, visible, parameter)),
            CommitPass = (track, parameter, pass, mode) => show(editor.CommitPassAsync(track, parameter, pass, mode)),
        };
    }
}
