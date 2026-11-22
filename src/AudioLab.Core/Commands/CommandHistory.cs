using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The current document of one open project and its undo and redo steps, with the commands of one gesture merged into one step.</summary>
/// <remarks>In memory only. Not thread safe: the caller holds the project's lock around every call.</remarks>
public sealed class CommandHistory
{
    /// <summary>The number of undo steps kept when none is asked for.</summary>
    public const int DefaultDepth = 100;

    /// <summary>The most undo steps a history may be asked to keep.</summary>
    public const int MaxDepth = 10_000;

    private readonly CommandRegistry _registry;

    private readonly IIdService _ids;

    private readonly int _depth;

    private readonly List<HistoryStep> _undo = [];

    private readonly List<HistoryStep> _redo = [];

    private GestureId? _open;

    /// <summary>Starts a history at <paramref name="initial"/>, which must already be a valid document.</summary>
    /// <param name="initial">The opened project.</param>
    /// <param name="registry">The commands that edit it.</param>
    /// <param name="ids">The id service handlers allocate from.</param>
    /// <param name="depth">The most undo steps kept; a gesture counts as one.</param>
    /// <exception cref="ArgumentException">The document breaks a project rule, or the depth is out of range.</exception>
    public CommandHistory(ProjectDocument initial, CommandRegistry registry, IIdService ids, int depth = DefaultDepth)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaxDepth);
        IReadOnlyList<string> problems = ProjectValidator.Validate(initial);
        if (problems.Count > 0)
        {
            throw new ArgumentException($"The document breaks the project rules: {problems[0]}", nameof(initial));
        }
        Current = initial;
        _registry = registry;
        _ids = ids;
        _depth = depth;
    }

    /// <summary>The document as it stands.</summary>
    public ProjectDocument Current { get; private set; }

    /// <summary>The gesture whose commands are still being merged into one step, or null.</summary>
    public GestureId? OpenGesture => _open;

    /// <summary>How many steps Undo can go back.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>How many steps Redo can go forward.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>The name of the command that began the step Undo would reverse, or null when there is none.</summary>
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;

    /// <summary>The name of the command that began the step Redo would reapply, or null when there is none.</summary>
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Applies a command; a refusal changes nothing, and a command that changes nothing adds no step.</summary>
    /// <remarks>
    /// A refusal does not even close an open gesture, and a command that leaves the document equal keeps the redo steps.
    /// Otherwise the command is a new step, unless it carries the open gesture's id, in which case it merges into that
    /// gesture's step. Any other gesture, or none, first closes the open one.
    /// </remarks>
    /// <param name="command">The command to apply.</param>
    /// <param name="gesture">The client's id for the gesture the command belongs to, or null for a single edit.</param>
    public CommandResult Apply(Command command, GestureId? gesture = null)
    {
        CommandResult result = _registry.Execute(Current, command, _ids);
        if (result.Document is not { } edited || edited == Current)
        {
            return result;
        }
        if (gesture is null || gesture != _open)
        {
            CloseGesture();
            _undo.Add(new HistoryStep(_registry.NameOf(command) ?? string.Empty, Current));
            if (_undo.Count > _depth)
            {
                _undo.RemoveAt(0);
            }
            _redo.Clear();
            _open = gesture;
        }
        Current = edited;
        return result;
    }

    /// <summary>Closes the gesture as the release of a drag does, if it is the open one; one that ended where it began leaves no step.</summary>
    /// <remarks>A release of another gesture, such as a late one from a second client, changes nothing.</remarks>
    public void EndGesture(GestureId gesture)
    {
        if (gesture == _open)
        {
            CloseGesture();
        }
    }

    /// <summary>Goes back one step, closing an open gesture first; null when there is nothing to undo.</summary>
    public ProjectDocument? Undo()
    {
        CloseGesture();
        if (_undo.Count == 0)
        {
            return null;
        }
        HistoryStep step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new HistoryStep(step.Label, Current));
        Current = step.Document;
        return Current;
    }

    /// <summary>Goes forward one step; null when there is nothing to redo. The same document comes back, ids included.</summary>
    public ProjectDocument? Redo()
    {
        CloseGesture();
        if (_redo.Count == 0)
        {
            return null;
        }
        HistoryStep step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(new HistoryStep(step.Label, Current));
        Current = step.Document;
        return Current;
    }

    private void CloseGesture()
    {
        if (_open is not null && _undo.Count > 0 && _undo[^1].Document == Current)
        {
            _undo.RemoveAt(_undo.Count - 1);
        }
        _open = null;
    }
}
