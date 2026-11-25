using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The drums tab: the pattern's swing and step count, and its lanes in order, with add, move, rename and remove. Each change is one command, and a refusal is shown in the server's words.</summary>
public sealed partial class DrumsPanel
{
    private const string PanelId = "drums-panel";

    private BeatPattern? _seen;

    private string _swingDraft = string.Empty;

    private string _stepsDraft = string.Empty;

    private string _nameDraft = string.Empty;

    private BeatLaneId? _renaming;

    private string? _renameError;

    private string? _focus;

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

    private string SwingText => Pattern.Swing.ToString("0.00", CultureInfo.InvariantCulture);

    private bool SwingValid => BeatDraft.TryParseSwing(_swingDraft, out _);

    private bool StepsValid => BeatDraft.TryParseSteps(_stepsDraft, out _);

    private bool CanApplySwing => Actions.CanEdit && SwingValid;

    private bool CanApplySteps => Actions.CanEdit && StepsValid;

    private string? SwingBlock => Block ?? (SwingValid ? null : "Enter a number from 0 to 1.");

    private string? StepsBlock => Block ?? (StepsValid ? null : $"Enter a whole number from 1 to {ProjectLimits.MaxBeatSteps}.");

    private static string RowId(BeatLaneId lane) => $"drum-lane-{lane.Value}";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A pattern another client changed shows its values; a draft typed here is replaced by the change.
        if (ReferenceEquals(_seen, Pattern))
        {
            return;
        }

        _seen = Pattern;
        _swingDraft = SwingText;
        _stepsDraft = Pattern.Steps.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_focus is not null)
        {
            Focus.Focus(_focus);
            _focus = null;
        }
    }

    private void SwingChanged(string value) => _swingDraft = value;

    private void StepsChanged(string value) => _stepsDraft = value;

    private void NameChanged(string value) => _nameDraft = value;

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
        EditResult result = await Actions.Beats.AddLane(null);
        if (result.Succeeded && result.Created.Count > 0)
        {
            _focus = RowId(BeatLaneId.From(result.Created[0]));
        }
    }

    private async Task MoveAsync(BeatLaneId lane, MoveDirection direction)
    {
        EditResult result = await Actions.Beats.MoveLane(lane, direction);
        if (result.Succeeded)
        {
            _focus = RowId(lane);
        }
    }

    private void BeginRename(BeatLane lane)
    {
        _renaming = lane.Id;
        _nameDraft = lane.Name;
        _renameError = null;
    }

    private void CloseRename()
    {
        if (_renaming is { } lane)
        {
            _focus = RowId(lane);
        }

        _renaming = null;
        _renameError = null;
    }

    private async Task RenameAsync()
    {
        if (_renaming is not { } lane)
        {
            return;
        }

        EditResult result = await Actions.Beats.RenameLane(lane, _nameDraft.Trim());
        if (result.Succeeded)
        {
            _renaming = null;
            _renameError = null;
            _focus = RowId(lane);
        }
        else
        {
            _renameError = result.Refusal;
        }
    }

    private async Task RemoveAsync(BeatLaneId lane)
    {
        EditResult result = await Actions.Beats.RemoveLane(lane);
        if (result.Succeeded)
        {
            _focus = PanelId;
        }
    }
}
