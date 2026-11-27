using AudioLab.Core.Model;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The strip beside an automation lane (D70): parameter, mode and hide, each with its reason when unusable.</summary>
public sealed partial class AutomationHeader
{
    private static readonly (AutomationMode Mode, string Label)[] _modes =
    [
        (AutomationMode.Off, "Off"),
        (AutomationMode.Read, "Read"),
        (AutomationMode.Touch, "Touch"),
        (AutomationMode.Latch, "Latch"),
        (AutomationMode.Write, "Write"),
    ];

    /// <summary>The track whose lane this is.</summary>
    [Parameter, EditorRequired]
    public Track Track { get; set; } = null!;

    /// <summary>Whether the lane may be changed now.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why it may not; shown on every control that would change the lane.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>What the screen can do to the lane.</summary>
    [Parameter, EditorRequired]
    public AutomationActions Actions { get; set; } = null!;

    private string Name => $"Automation of {Track.Name}";

    private string ParameterId => Track.View.AutomationParameter == AutomationParameter.Pan ? "pan" : "volume";

    private string ModeId => Track.Automation.Mode.ToString().ToLowerInvariant();

    private IReadOnlyList<SegmentItem> ParameterItems =>
    [
        new SegmentItem("volume", "Volume lane", Label: "Volume", Reason: Reason),
        new SegmentItem("pan", "Pan lane", Label: "Pan", Reason: Reason),
    ];

    private IReadOnlyList<SegmentItem> ModeItems =>
    [
        .. _modes.Select(mode => new SegmentItem(
            Id(mode.Mode),
            $"Automation {mode.Label}",
            Label: mode.Label,
            Reason: Reason ?? AutomationModes.UnavailableReason(mode.Mode, Track.View.AutomationParameter))),
    ];

    private static string Id(AutomationMode mode) => mode.ToString().ToLowerInvariant();

    private Task ChooseParameterAsync(string id)
    {
        AutomationParameter parameter = id == "pan" ? AutomationParameter.Pan : AutomationParameter.Volume;
        return Editable && parameter != Track.View.AutomationParameter ? Actions.SetView(Track.Id, null, parameter) : Task.CompletedTask;
    }

    private Task ChooseModeAsync(string id)
    {
        AutomationMode? mode = _modes.Where(choice => Id(choice.Mode) == id).Select(choice => (AutomationMode?)choice.Mode).FirstOrDefault();
        bool allowed = Editable && mode is { } chosen && chosen != Track.Automation.Mode;
        return allowed && AutomationModes.UnavailableReason(mode!.Value, Track.View.AutomationParameter) is null ? Actions.SetMode(Track.Id, mode.Value) : Task.CompletedTask;
    }

    private Task HideAsync() => Editable ? Actions.SetView(Track.Id, false, null) : Task.CompletedTask;
}
