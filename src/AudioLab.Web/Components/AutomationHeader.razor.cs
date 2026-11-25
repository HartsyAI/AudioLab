using AudioLab.Core.Model;
using AudioLab.Web.Screens;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The strip beside an automation lane (D69): its parameter, its mode and a hide control, each with its reason when it cannot be used.</summary>
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

    private IReadOnlyList<SegmentItem> ModeItems => [.. _modes.Select(mode => new SegmentItem(mode.Mode.ToString().ToLowerInvariant(), $"Automation {mode.Label}", Label: mode.Label, Reason: Reason))];

    private Task ChooseParameterAsync(string id)
    {
        AutomationParameter parameter = id == "pan" ? AutomationParameter.Pan : AutomationParameter.Volume;
        return Editable && parameter != Track.View.AutomationParameter ? Actions.SetView(Track.Id, null, parameter) : Task.CompletedTask;
    }

    private Task ChooseModeAsync(string id)
    {
        AutomationMode? mode = _modes.Where(choice => choice.Mode.ToString().ToLowerInvariant() == id).Select(choice => (AutomationMode?)choice.Mode).FirstOrDefault();
        return Editable && mode is { } chosen && chosen != Track.Automation.Mode ? Actions.SetMode(Track.Id, chosen) : Task.CompletedTask;
    }

    private Task HideAsync() => Editable ? Actions.SetView(Track.Id, false, null) : Task.CompletedTask;
}
