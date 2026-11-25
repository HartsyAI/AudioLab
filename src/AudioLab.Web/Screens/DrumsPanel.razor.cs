using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The drums tab: the pattern's swing and step count, its lanes in order with add, move, rename and remove, and each lane's gain and steps.</summary>
public sealed partial class DrumsPanel : IDisposable
{
    private const string PanelId = "drums-panel";

    private bool _seen;

    private double _seenSwing;

    private int _seenSteps;

    private string _swingDraft = string.Empty;

    private string _stepsDraft = string.Empty;

    private string _nameDraft = string.Empty;

    private readonly Dictionary<BeatLaneId, string> _gainDrafts = [];

    private readonly Dictionary<BeatLaneId, double> _seenGains = [];

    private BeatLaneId? _renaming;

    private string? _renameError;

    private string? _focus;

    private string? _renameFocus;

    private string? _announcement;

    private BeatLaneId? _cursorLane;

    private int _cursorStep;

    private bool _inGrid;

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

    [Inject]
    internal DrumKeys? GridKeys { get; set; }

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

    /// <summary>How many times the panel has rendered; a pointer over a step does not add one.</summary>
    internal int Renders { get; private set; }

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
    protected override void OnInitialized() => Claim();

    /// <summary>Hands the page the keys the grid takes first, while this tab is open.</summary>
    internal void Claim()
    {
        if (GridKeys is not null)
        {
            GridKeys.Handler = ClaimsGridKey;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => FollowPattern();

    /// <inheritdoc />
    public void Dispose()
    {
        if (GridKeys is not null)
        {
            GridKeys.Handler = null;
        }
    }

    /// <summary>Takes a document and what the screen can do, as a render with new parameters does, and follows the pattern.</summary>
    internal void Take(ProjectDocument document, SessionActions actions)
    {
        Document = document;
        Actions = actions;
        FollowPattern();
    }

    /// <summary>Follows the pattern: the swing and step drafts when those change, and a lane's gain draft when that lane's gain changes.</summary>
    internal void FollowPattern()
    {
        foreach (BeatLane lane in Pattern.Lanes)
        {
            if (!_seenGains.TryGetValue(lane.Id, out double seenGain) || seenGain != lane.Gain)
            {
                _seenGains[lane.Id] = lane.Gain;
                _gainDrafts[lane.Id] = BeatDraft.FormatGain(lane.Gain);
                _dirty = true;
            }
        }

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
        Renders++;
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

    // A lane's gain: the draft a person types, which a change of that gain from another client replaces.
    private string GainValue(BeatLane lane) => _gainDrafts.TryGetValue(lane.Id, out string? draft) ? draft : BeatDraft.FormatGain(lane.Gain);

    private bool GainValid(BeatLane lane) => BeatDraft.TryParseGain(GainValue(lane), out _);

    private bool CanApplyGain(BeatLane lane) => Actions.CanEdit && BeatDraft.CanApplyGain(GainValue(lane), lane.Gain);

    private string? GainBlock(BeatLane lane)
    {
        if (Block is { } reason)
        {
            return reason;
        }

        if (!GainValid(lane))
        {
            return BeatDraft.GainReason(GainValue(lane));
        }

        return CanApplyGain(lane) ? null : "The lane already has this gain.";
    }

    internal void GainChanged(BeatLaneId lane, string value)
    {
        _gainDrafts[lane] = value;
        _dirty = true;
    }

    internal async Task SetGainAsync(BeatLane lane)
    {
        if (BeatDraft.TryParseGain(GainValue(lane), out double gain))
        {
            EditResult result = await Actions.Beats.SetLaneGain(lane.Id, gain);
            _announcement = result.Succeeded ? null : result.Refusal;
            _dirty = true;
        }
    }

    // A paint drag over a lane's steps: the stroke holds the drag's state, the gesture and the commands; the grid draws it.
    internal StepStroke StartStroke(BeatLane lane, bool on)
    {
        string gesture = Actions.Beats.BeginStroke();
        BeatLaneId id = lane.Id;
        SessionActions actions = Actions;
        return new StepStroke(on, gesture, (steps, state) => actions.Beats.SetLaneSteps(id, steps, state, gesture));
    }

    // The drag has ended and its commands have been answered: the gesture closes, and a refusal is said in the status line.
    internal async Task StrokeEndedAsync(StepStroke stroke)
    {
        await stroke.Done;
        await Actions.Beats.EndStroke(stroke.Gesture);
        if (stroke.Refusal is { } refusal)
        {
            _announcement = refusal;
        }
    }

    internal Task PressAsync(BeatLane lane, int step)
    {
        _cursorLane = lane.Id;
        _cursorStep = step;
        if (!Actions.CanEdit)
        {
            _announcement = Actions.Reason;
        }

        return Task.CompletedTask;
    }

    internal Task GridFocusAsync(bool inside)
    {
        _inGrid = inside;
        return Task.CompletedTask;
    }

    // The tab stop of the grids: the step the focus last reached, in the first lane when none has been reached.
    internal int CursorFor(BeatLane lane)
    {
        BeatLane? active = Pattern.Lanes.FirstOrDefault(candidate => candidate.Id == _cursorLane) ?? Pattern.Lanes.FirstOrDefault();
        if (active is null || active.Id != lane.Id)
        {
            return -1;
        }

        return Math.Clamp(_cursorStep, 0, Math.Max(lane.Steps.Count - 1, 0));
    }

    // Space, Home and End belong to the grid while its focus is in it, so the session does not start the transport or move the playhead.
    private bool ClaimsGridKey(KeyChord chord) => _inGrid && Shortcuts.DrumStepClaims.Any(shortcut => shortcut.Matches(chord));

    // Arrows move the focus; Space or Enter switches the focused step, as one command, and a held key does not repeat a switch.
    internal async Task GridKeyAsync(BeatLane lane, int step, KeyboardEventArgs key)
    {
        if (Shortcuts.DrumStepToggle.Matches(key) || Shortcuts.DrumStepToggleEnter.Matches(key))
        {
            if (!key.Repeat)
            {
                await ToggleAsync(lane, step);
            }

            return;
        }

        if (!Shortcuts.DrumStep.Any(shortcut => shortcut.Matches(key)) || Destination(lane, step, key) is not { } place)
        {
            return;
        }

        _cursorLane = place.Lane.Id;
        _cursorStep = place.Step;
        _focus = BeatLaneText.CellId(place.Lane.Id, place.Step);
    }

    // Where a movement key takes the focus; null when it has nowhere to go.
    private (BeatLane Lane, int Step)? Destination(BeatLane lane, int step, KeyboardEventArgs key)
    {
        int last = lane.Steps.Count - 1;
        if (last < 0)
        {
            return null;
        }

        if (Shortcuts.DrumStepEarlier.Matches(key))
        {
            return (lane, Math.Max(step - 1, 0));
        }

        if (Shortcuts.DrumStepLater.Matches(key))
        {
            return (lane, Math.Min(step + 1, last));
        }

        if (Shortcuts.DrumStepFirst.Matches(key))
        {
            return (lane, 0);
        }

        if (Shortcuts.DrumStepLast.Matches(key))
        {
            return (lane, last);
        }

        bool up = Shortcuts.DrumStepAbove.Matches(key);
        if (!up && !Shortcuts.DrumStepBelow.Matches(key))
        {
            return null;
        }

        int index = Pattern.Lanes.ToList().FindIndex(candidate => candidate.Id == lane.Id);
        int other = up ? index - 1 : index + 1;
        if (index < 0 || other < 0 || other >= Pattern.Lanes.Count)
        {
            return null;
        }

        BeatLane neighbour = Pattern.Lanes[other];
        return (neighbour, Math.Min(step, Math.Max(neighbour.Steps.Count - 1, 0)));
    }

    // One step switches to the state it does not have now, as one command; a read-only project says why.
    private async Task ToggleAsync(BeatLane lane, int step)
    {
        _cursorLane = lane.Id;
        _cursorStep = step;
        _focus = BeatLaneText.CellId(lane.Id, step);
        if (!Actions.CanEdit)
        {
            _announcement = Actions.Reason;
            return;
        }

        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            bool on = lane.Steps[step];
            EditResult result = await Actions.Beats.SetLaneSteps(lane.Id, [step], !on, null);
            _announcement = result.Succeeded ? null : result.Refusal;
        }
        finally
        {
            _busy = false;
        }
    }
}
