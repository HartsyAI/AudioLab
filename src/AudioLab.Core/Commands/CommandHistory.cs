using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The current document of one open project and its undo and redo steps, with the commands of one gesture merged into one step.</summary>
/// <remarks>
/// In memory only. Not thread safe: the caller holds the project's lock around every call. A step is a whole-document
/// snapshot, so undoing it also reverts what other clients changed after it; there is no per-client undo.
/// </remarks>
public sealed class CommandHistory
{
    /// <summary>The most gestures that may be open at once, one per client; the next is refused.</summary>
    public const int MaxOpenGestures = 64;

    /// <summary>The number of undo steps kept when none is asked for.</summary>
    public const int DefaultDepth = 100;

    /// <summary>The most undo steps a history may be asked to keep.</summary>
    public const int MaxDepth = 10_000;

    private readonly CommandRegistry _registry;

    private readonly IIdService _ids;

    private readonly int _depth;

    private readonly List<HistoryStep> _undo = [];

    private readonly List<HistoryStep> _redo = [];

    private readonly List<OpenGesture> _open = [];

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

    /// <summary>The document as it stands, including the changes of gestures that are still open.</summary>
    public ProjectDocument Current { get; private set; }

    /// <summary>How many steps Undo can go back; the steps of open gestures count once they close.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>How many steps Redo can go forward; an open gesture clears them when it closes with a change.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>The name of the command that began the step Undo would reverse, or null when there is none.</summary>
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;

    /// <summary>The name of the command that began the step Redo would reapply, or null when there is none.</summary>
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Every document the history could show again: the current one, each undo and redo step, and each open gesture's start.</summary>
    /// <remarks>A media store applies its reference rule to all of these, as an undo step can play a file the project no longer does.</remarks>
    public IEnumerable<ProjectDocument> HeldDocuments()
    {
        yield return Current;
        foreach (HistoryStep step in _undo)
        {
            yield return step.Document;
        }
        foreach (HistoryStep step in _redo)
        {
            yield return step.Document;
        }
        foreach (OpenGesture gesture in _open)
        {
            yield return gesture.Before;
        }
    }

    /// <summary>The gesture this client has open, or null.</summary>
    public GestureId? OpenGestureOf(ClientId client)
    {
        int index = _open.FindIndex(open => open.Client == client);
        return index >= 0 ? _open[index].Id : null;
    }

    /// <summary>Applies a command; a refusal changes nothing, and a command that changes nothing adds no step.</summary>
    /// <remarks>
    /// A client has at most one open gesture, so another client's commands never close it. A command that carries the
    /// client's open gesture id merges into that gesture. A command that was accepted with a different gesture id, or
    /// none, closes it first, even if it changes nothing. A gesture's undo step is recorded, and the redo steps are cleared, when the gesture closes with a
    /// change; one that ends where it began leaves the history exactly as it was.
    /// </remarks>
    /// <param name="command">The command to apply.</param>
    /// <param name="client">Who sent it, as the hub names the connection.</param>
    /// <param name="gesture">The client's id for the gesture the command belongs to, or null for a single edit.</param>
    /// <exception cref="ArgumentException">The client id or the gesture id is <c>default</c>.</exception>
    /// <returns>The result; opening a gesture past <see cref="MaxOpenGestures"/> is refused with <see cref="CommandErrorCode.Limit"/>.</returns>
    public CommandResult Apply(Command command, ClientId client, GestureId? gesture = null)
    {
        RequireClient(client);
        if (gesture is { } given && given.Value.Length == 0)
        {
            throw new ArgumentException("A gesture id is required; pass null for a single edit.", nameof(gesture));
        }
        int index = _open.FindIndex(open => open.Client == client);
        if (gesture is not null && index < 0 && _open.Count >= MaxOpenGestures)
        {
            return CommandResult.Refuse(CommandErrorCode.Limit, $"{MaxOpenGestures} gestures are already open in this project.");
        }
        CommandResult result = _registry.Execute(Current, command, _ids);
        if (result.Document is not { } edited)
        {
            return result;
        }
        if (!_registry.IsUndoable(command))
        {
            // Live state: it changes the document now and is no step, so no gesture is opened or closed by it.
            Current = edited;
            return result;
        }
        if (index >= 0 && gesture != _open[index].Id)
        {
            Close(index);
            index = -1;
        }
        if (edited == Current)
        {
            return result;
        }
        if (index >= 0)
        {
            Current = edited;
            return result;
        }
        string label = _registry.NameOf(command) ?? string.Empty;
        if (gesture is { } id)
        {
            _open.Add(new OpenGesture(client, id, label, Current));
        }
        else
        {
            Push(new HistoryStep(label, Current));
        }
        Current = edited;
        return result;
    }

    /// <summary>Closes the client's gesture as the release of a drag does, if it is the one open; any other id changes nothing.</summary>
    /// <remarks>A late or repeated release, or one for a gesture that never opened, is not an error.</remarks>
    public void EndGesture(ClientId client, GestureId gesture)
    {
        RequireClient(client);
        int index = _open.FindIndex(open => open.Client == client && open.Id == gesture);
        if (index >= 0)
        {
            Close(index);
        }
    }

    /// <summary>Closes whatever gesture the client has open, as when it disconnects.</summary>
    public void ReleaseClient(ClientId client)
    {
        RequireClient(client);
        int index = _open.FindIndex(open => open.Client == client);
        if (index >= 0)
        {
            Close(index);
        }
    }

    /// <summary>Goes back one step, closing the open gestures first; null when there is nothing to undo.</summary>
    public ProjectDocument? Undo()
    {
        CloseAll();
        if (_undo.Count == 0)
        {
            return null;
        }
        HistoryStep step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new HistoryStep(step.Label, Current));
        Current = LiveState.Carry(step.Document, Current);
        return Current;
    }

    /// <summary>Goes forward one step; null when there is nothing to redo. The same document comes back, ids included.</summary>
    public ProjectDocument? Redo()
    {
        CloseAll();
        if (_redo.Count == 0)
        {
            return null;
        }
        HistoryStep step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(new HistoryStep(step.Label, Current));
        Current = LiveState.Carry(step.Document, Current);
        return Current;
    }

    private static void RequireClient(ClientId client)
    {
        if (client.Value.Length == 0)
        {
            throw new ArgumentException("A client id is required.", nameof(client));
        }
    }

    private void Push(HistoryStep step)
    {
        _undo.Add(step);
        if (_undo.Count > _depth)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
    }

    // A gesture that ended where it began is dropped whole, so undo and redo are exactly as they were.
    private void Close(int index)
    {
        OpenGesture open = _open[index];
        _open.RemoveAt(index);
        if (LiveState.Carry(open.Before, Current) != Current)
        {
            Push(new HistoryStep(open.Label, open.Before));
        }
    }

    private void CloseAll()
    {
        while (_open.Count > 0)
        {
            Close(0);
        }
    }
}
