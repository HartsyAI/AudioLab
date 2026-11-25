using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>A track group in the mixer: its fold, tint, colour, volume, mute and solo. It redraws only when its own state changed.</summary>
public sealed partial class GroupStrip
{
    private string? _shown;

    /// <summary>The group.</summary>
    [Parameter, EditorRequired]
    public TrackGroup Group { get; set; } = null!;

    /// <summary>How many tracks the group holds.</summary>
    [Parameter]
    public int Members { get; set; }

    /// <summary>The group's colour as a style value, already mapped to a track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The twelve track colours, in order: the choices for the group's colour.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Palette { get; set; } = [];

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    private string VolumeText => FaderScale.Readout(Group.Volume);

    private string SwatchTitle => Actions.CanEdit ? string.Empty : Actions.Reason ?? string.Empty;

    // The fader travels over the decibel scale, the same as a track's, so its marks and its readout agree.
    private static double PositionOf(double gain) => FaderScale.Position(gain);

    private static string FormatVolume(double position) => FaderScale.Readout(FaderScale.Gain(position));

    private Task SetVolumeAsync(double position) => Actions.Groups.SetVolume(Group.Id, FaderScale.Gain(position));

    private Task SetColourAsync(int index)
    {
        return index >= 0 && index < Palette.Count ? Actions.Groups.SetColor(Group.Id, Palette[index]) : Task.CompletedTask;
    }

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
        return $"{Group.Id.Value}|{Group.Name}|{Group.Color}|{Group.Collapsed}|{Group.Tint}|{Group.Muted}|{Group.Soloed}|{Group.Volume}|" +
            $"{Members}|{ColorToken}|{Actions.CanEdit}|{Actions.Reason}";
    }
}
