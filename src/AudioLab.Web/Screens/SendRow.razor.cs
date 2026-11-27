using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>One send of a track: its bus, its level as a knob, the place against the fader, the mute and a remove.</summary>
/// <remarks>It redraws only when its own send changed.</remarks>
public sealed partial class SendRow
{
    private string? _shown;

    /// <summary>The track that sends.</summary>
    [Parameter, EditorRequired]
    public TrackId Track { get; set; }

    /// <summary>The track's name, for the labels.</summary>
    [Parameter, EditorRequired]
    public string TrackName { get; set; } = string.Empty;

    /// <summary>The send.</summary>
    [Parameter, EditorRequired]
    public Send Send { get; set; }

    /// <summary>The bus's name, for the labels.</summary>
    [Parameter, EditorRequired]
    public string BusName { get; set; } = string.Empty;

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    private static string ResetKey => Shortcuts.ControlReset.Display;

    private double KnobPosition => PositionOf(Send.Level);

    private string LevelText => LevelOf(Send.Level);

    // The reset returns the knob to the server's starting level, the same value the handler gives a new send.
    private static double ResetPosition => PositionOf(MixRules.DefaultSendLevel);

    private static double PositionOf(double gain) => (FaderScale.Position(gain) * 2) - 1;

    private static double GainOf(double knob) => FaderScale.Gain((Math.Clamp(knob, -1, 1) + 1) / 2);

    private static string LevelOf(double gain) => FaderScale.Readout(gain);

    private static string FormatLevel(double knob) => LevelOf(GainOf(knob));

    private Task RemoveAsync() => Actions.Sends.Remove(Track, Send.Bus);

    private Task SetLevelAsync(double knob) => Actions.Sends.SetLevel(Track, Send.Bus, GainOf(knob));

    private Task SetMutedAsync(bool muted) => Actions.Sends.SetMuted(Track, Send.Bus, muted);

    private Task SetPreAsync(bool pre) => Actions.Sends.SetPre(Track, Send.Bus, pre);

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // The first render does not ask ShouldRender, so what it drew is remembered here; without it the next change would draw again for nothing.
        if (firstRender)
        {
            _shown = Key();
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        string now = Key();
        bool same = now == _shown;
        _shown = now;
        return !same;
    }

    private string Key()
    {
        return $"{Track.Value}|{TrackName}|{Send.Bus.Value}|{BusName}|{Send.Level}|{Send.Pre}|{Send.Muted}|{Actions.CanEdit}|{Actions.Reason}";
    }
}
