using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel strip: name, effect slots, pan, fader and meter, then M, S and the level. The master is wider.</summary>
public sealed partial class ChannelStrip
{
    private const int VisibleSlots = 2;

    private string? _shown;

    /// <summary>The track's or the master's name.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>The colour of the dot as a style value, already mapped to a track token; the master uses the text colour.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The effect names and whether each is in the signal path, in chain order.</summary>
    [Parameter]
    public IReadOnlyList<(string Name, bool Enabled)> Effects { get; set; } = [];

    /// <summary>The pan from -1 to 1; the master has none and stays at the centre.</summary>
    [Parameter]
    public double Pan { get; set; }

    /// <summary>The linear gain of the fader.</summary>
    [Parameter]
    public double Volume { get; set; } = 1;

    /// <summary>Whether the channel is muted.</summary>
    [Parameter]
    public bool Muted { get; set; }

    /// <summary>Whether the channel is soloed.</summary>
    [Parameter]
    public bool Soloed { get; set; }

    /// <summary>True for the master strip.</summary>
    [Parameter]
    public bool Master { get; set; }

    /// <summary>Whether the strip's track is the selected one.</summary>
    [Parameter]
    public bool Selected { get; set; }

    private double Width => Master ? 7.75 : 6.5;

    private string PanText => TrackText.Pan(Pan);

    private double VolumePosition => FaderScale.Position(Volume);

    private string VolumeText => FaderScale.Text(Volume);

    private IReadOnlyList<(string? Name, bool Enabled)> Slots
    {
        get
        {
            List<(string? Name, bool Enabled)> slots = [.. Effects.Take(VisibleSlots).Select(effect => ((string?)effect.Name, effect.Enabled))];
            if (Effects.Count > VisibleSlots)
            {
                slots[^1] = ($"{Effects.Count - VisibleSlots + 1} more effects", true);
            }
            while (slots.Count < VisibleSlots)
            {
                slots.Add((null, true));
            }
            return slots;
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        string effects = string.Join(',', Effects.Select(effect => $"{effect.Name}:{effect.Enabled}"));
        string now = $"{Name}|{ColorToken}|{effects}|{Pan}|{Volume}|{Muted}|{Soloed}|{Master}|{Selected}";
        bool same = now == _shown;
        _shown = now;
        return !same;
    }
}
