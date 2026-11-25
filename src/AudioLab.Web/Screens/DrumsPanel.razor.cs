using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The drums tab: the pattern's swing and step count, and its lanes in order, with add, move, rename and remove.</summary>
public sealed partial class DrumsPanel
{
    private const string PanelId = "drums-panel";

    private bool _seen;

    private double _seenSwing;

    private int _seenSteps;

    private string _swingDraft = string.Empty;

    private string _stepsDraft = string.Empty;

    private string _nameDraft = string.Empty;

    private BeatLaneId? _renaming;

    private string? _renameError;

    private string? _focus;

    private string? _renameFocus;

    private string? _announcement;

    private bool _busy;

    private bool _dirty = true;

    private BeatPattern? _shown;

    private SessionActions? _shownActions;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    internal IElementFocus Focus { get; set; } = default!;

    /// <summary>The element that takes the focus after the next render, or null.</summary>
    internal string? PendingFocus => _focus;

    /// <summary>What the status line says, such as why a lane key does nothing.</summary>
    internal string? Announcement => _announcement;

    /// <summary>The swing as typed, for the field.</summary>
    internal string SwingDraft => _swingDraft;

    /// <summary>The refusal the rename dialog shows, or null.</summary>
    internal string? RenameError => _renameError;

    /// <summary>Whether the rename dialog is open.</summary>
    internal bool RenameOpen => _renaming is not null;

    private BeatPattern Pattern => Document.BeatPattern;

    private string? Block => Actions.CanEdit ? null : Actions.Reason;

    private string SwingText => BeatDraft.FormatSwing(Pattern.Swing);

    private bool SwingValid => BeatDraft.TryParseSwing(_swingDraft, out _);

    private bool StepsValid => BeatDraft.TryParseSteps(_stepsDraft, out _);

    private bool CanApplySwing => Actions.CanEdit && BeatDraft.CanApplySwing(_swingDraft, Pattern.Swing);

    private bool CanApplySteps => Actions.CanEdit && BeatDraft.CanApplySteps(_stepsDraft, Pattern.Steps);

    private string? SwingBlock => Block ?? SwingReason;

    private string? StepsBlock => Block ?? StepsReason;

    private string? SwingReason => !SwingValid ? BeatDraft.SwingReason(_swingDraft) : CanApplySwing ? null : "The pattern already has this swing.";

    private string? StepsReason => !StepsValid ? BeatDraft.StepsReason() : CanApplySteps ? null : "The pattern already has this step count.";

    private string? RenameBlock => string.IsNullOrWhiteSpace(_nameDraft) ? "Enter a name for the lane." : null;

    /// <inheritdoc />
    protected override void OnParametersSet() => FollowPattern();

    /// <summary>Takes a document and what the screen can do, as a render with new parameters does, and follows the pattern.</summary>
    internal void Take(ProjectDocument document, SessionActions actions)
    {
        Document = document;
        Actions = actions;
        FollowPattern();
    }

    /// <summary>Writes the drafts from the pattern when its swing or step count changed; a lane edit by another client leaves typing alone.</summary>
    internal void FollowPattern()
    {
        if (_seen && !BeatDraft.ChangedSince(Pattern, _seenSwing, _seenSteps))
        {
            return;
        }

        _seen = true;
        _seenSwing = Pattern.Swing;
        _seenSteps = Pattern.Steps;
        _swingDraft = BeatDraft.FormatSwing(Pattern.Swing);
        _stepsDraft = Pattern.Steps.ToString(CultureInfo.InvariantCulture);
        _dirty = true;
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _dirty || !ReferenceEquals(Actions, _shownActions) || !Pattern.Equals(_shown);

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        _shown = Pattern;
        _shownActions = Actions;
        _dirty = false;
        if (_focus is not null)
        {
            Focus.Focus(_focus);
            _focus = null;
        }
    }

    private static Dictionary<string, object> IdAttribute(string id) => new() { ["id"] = id };

    internal void SwingChanged(string value)
    {
        _swingDraft = value;
        _dirty = true;
    }

    private void StepsChanged(string value)
    {
        _stepsDraft = value;
        _dirty = true;
    }

    private void NameChanged(string value)
    {
        _nameDraft = value;
        _dirty = true;
    }

    private async Task ApplySwingAsync()
    {
        if (BeatDraft.TryParseSwing(_swingDraft, out double swing))
        {
            await Actions.Beats.SetSwing(swing);
        }
    }

    private async Task ApplyStepsAsync()
    {
        if (BeatDraft.TryParseSteps(_stepsDraft, out int steps))
        {
            await Actions.Beats.SetSteps(steps);
        }
    }

    private async Task AddLaneAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            EditResult result = await Actions.Beats.AddLane(null);
            if (result.Succeeded && result.Created.Count > 0)
            {
                _focus = BeatLaneText.RowId(BeatLaneId.From(result.Created[0]));
            }
        }
        finally
        {
            _busy = false;
        }
    }

    internal async Task MoveAsync(BeatLane lane, MoveDirection direction)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            EditResult result = await Actions.Beats.MoveLane(lane.Id, direction);
            if (result.Succeeded)
            {
                _focus = BeatLaneText.MoveId(lane.Id, direction);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    // A held key does not repeat a move, a rename or a removal; a read-only project says why in the status line.
    internal async Task KeyAsync(BeatLane lane, KeyboardEventArgs key)
    {
        if (key.Repeat || !Shortcuts.DrumLane.Any(shortcut => shortcut.Matches(key)))
        {
            return;
        }

        if (!Actions.CanEdit)
        {
            _announcement = Actions.Reason;
            _dirty = true;
            return;
        }

        if (Shortcuts.DrumEarlier.Matches(key))
        {
            await MoveAsync(lane, MoveDirection.Up);
        }
        else if (Shortcuts.DrumLater.Matches(key))
        {
            await MoveAsync(lane, MoveDirection.Down);
        }
        else if (Shortcuts.DrumRename.Matches(key))
        {
            BeginRename(lane);
        }
        else if (Shortcuts.DrumRemove.Matches(key))
        {
            await RemoveAsync(lane);
        }
    }

    internal void BeginRename(BeatLane lane)
    {
        _renaming = lane.Id;
        _nameDraft = lane.Name;
        _renameError = null;
        _renameFocus = BeatLaneText.RenameId(lane.Id);
        _dirty = true;
    }

    internal void CloseRename()
    {
        _renaming = null;
        _renameError = null;
        _dirty = true;
    }

    // The dialog has closed, so focus goes back to the Rename button of the lane it was opened from.
    internal Task RestoreRenameFocusAsync()
    {
        if (_renameFocus is { } id)
        {
            Focus.Focus(id);
            _renameFocus = null;
        }

        return Task.CompletedTask;
    }

    internal async Task RenameAsync()
    {
        if (_renaming is not { } lane || _busy)
        {
            return;
        }

        _busy = true;
        try
        {
            EditResult result = await Actions.Beats.RenameLane(lane, _nameDraft.Trim());
            _dirty = true;
            if (result.Succeeded)
            {
                _renaming = null;
                _renameError = null;
            }
            else
            {
                _renameError = result.Refusal;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    internal async Task RemoveAsync(BeatLane lane)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            EditResult result = await Actions.Beats.RemoveLane(lane.Id);
            if (result.Succeeded)
            {
                _focus = PanelId;
            }
        }
        finally
        {
            _busy = false;
        }
    }
}
