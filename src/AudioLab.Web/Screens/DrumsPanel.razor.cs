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
    private IElementFocus Focus { get; set; } = default!;

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
    protected override void OnParametersSet()
    {
        // The drafts follow the pattern only when its swing or step count changes; another client's lane edit leaves typing alone.
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

    private void SwingChanged(string value)
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

    private async Task MoveAsync(BeatLane lane, MoveDirection direction)
    {
        EditResult result = await Actions.Beats.MoveLane(lane.Id, direction);
        if (result.Succeeded)
        {
            _focus = BeatLaneText.MoveId(lane.Id, direction);
        }
    }

    private async Task KeyAsync(BeatLane lane, KeyboardEventArgs key)
    {
        if (!Actions.CanEdit)
        {
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

    private void BeginRename(BeatLane lane)
    {
        _renaming = lane.Id;
        _nameDraft = lane.Name;
        _renameError = null;
        _renameFocus = BeatLaneText.RenameId(lane.Id);
        _dirty = true;
    }

    private void CloseRename()
    {
        _renaming = null;
        _renameError = null;
        _dirty = true;
    }

    // The dialog has closed, so focus goes back to the Rename button of the lane it was opened from.
    private Task RestoreRenameFocusAsync()
    {
        if (_renameFocus is { } id)
        {
            Focus.Focus(id);
            _renameFocus = null;
        }

        return Task.CompletedTask;
    }

    private async Task RenameAsync()
    {
        if (_renaming is not { } lane)
        {
            return;
        }

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

    private async Task RemoveAsync(BeatLane lane)
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
