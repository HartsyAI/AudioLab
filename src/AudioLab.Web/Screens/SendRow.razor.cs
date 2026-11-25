using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>One send of a track: its bus, its level as a knob, the place against the fader, the mute and a remove. It redraws only when its own send changed.</summary>
public sealed partial class SendRow
{
    // A new send starts at the server's default, which is -6 dB, so the knob's reset returns to it.
    private const double DefaultLevel = 0.5;

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

    private static double PositionOf(double gain) => (FaderScale.Position(gain) * 2) - 1;

    private static double GainOf(double knob) => FaderScale.Gain((Math.Clamp(knob, -1, 1) + 1) / 2);

    private static string LevelOf(double gain)
    {
        string text = FaderScale.Text(gain);
        return text == "-inf" ? text : $"{text} dB";
    }

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
