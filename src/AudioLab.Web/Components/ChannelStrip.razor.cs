using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel strip: name, effect slots, pan, fader and meter, then M, S and the level. The master is wider.</summary>
public sealed partial class ChannelStrip
{
    private const int VisibleSlots = 2;

    private string? _shown;

    /// <summary>The track this strip belongs to, or null for the master.</summary>
    [Parameter]
    public TrackId? Track { get; set; }

    /// <summary>Whether mute and solo act; when not, they are disabled.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Raised with the track when mute is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnMute { get; set; }

    /// <summary>Raised with the track when solo is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnSolo { get; set; }

    /// <summary>Why the toggles do nothing now, for their titles; null when they act.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The track's or the master's name.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>The colour of the dot as a style value, already mapped to a track token; the master uses the text colour.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The effects in chain order, each with its name and whether it is in the signal path.</summary>
    [Parameter]
    public IReadOnlyList<StripEffect> Effects { get; set; } = [];

    /// <summary>Raised with the track and the effect (or null for the chain as it is) when a slot is pressed; the master has none to open.</summary>
    [Parameter]
    public EventCallback<(TrackId Track, EffectId? Effect)> OnOpenEffects { get; set; }

    /// <summary>Why the track is silent in the mix, in words, or null when it is heard; the caption says it, not the colour alone.</summary>
    [Parameter]
    public string? Silence { get; set; }

    /// <summary>Raised with the gain when the fader is dragged or keyed.</summary>
    /// <remarks>Without it the fader is shown and cannot be changed, as a track fader is for now.</remarks>
    [Parameter]
    public EventCallback<double> OnVolume { get; set; }

    /// <summary>Raised with the wanted state when the master limiter's toggle is pressed.</summary>
    [Parameter]
    public EventCallback<bool> OnLimiter { get; set; }

    /// <summary>Whether the master limiter is on, for its toggle.</summary>
    [Parameter]
    public bool LimiterOn { get; set; }

    /// <summary>The note under the master's slot: it is a project setting, and the master has no effect chain.</summary>
    [Parameter]
    public string? MasterNote { get; set; }

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

    private static double VolumePositionOfUnity => FaderScale.Position(1);

    private static string FormatVolume(double position) => FaderScale.Readout(FaderScale.Gain(position));

    // A fader is interactive only where a handler for its gain is given: the master's, not a track's.
    private EventCallback<double> VolumeCommit => OnVolume.HasDelegate ? EventCallback.Factory.Create<double>(this, VolumeCommitAsync) : default;

    private Task VolumeCommitAsync(double position) => OnVolume.InvokeAsync(FaderScale.Gain(position));

    private Task ToggleLimiterAsync() => OnLimiter.InvokeAsync(!LimiterOn);

    private IReadOnlyList<StripSlot> Slots
    {
        get
        {
            List<StripSlot> slots = [.. Effects.Take(VisibleSlots).Select(Shown)];
            if (Effects.Count > VisibleSlots)
            {
                // The last slot stands for the effects that do not fit, and opens the first of them.
                IEnumerable<StripEffect> rest = Effects.Skip(VisibleSlots - 1);
                string text = $"{Effects.Count - VisibleSlots + 1} more effects";
                slots[^1] = new StripSlot(text, rest.Any(effect => effect.Enabled), Effects[VisibleSlots - 1].Id, Opens, Overflow: true);
            }
            while (slots.Count < VisibleSlots)
            {
                slots.Add(new StripSlot(null, true, null, Opens));
            }
            return slots;
        }
    }

    // Only a track has a chain to open; the master's limiter is a project setting.
    private bool Opens => Track is not null && OnOpenEffects.HasDelegate;

    private StripSlot Shown(StripEffect effect) => new(effect.Name, effect.Enabled, effect.Id, Opens && effect.Id is not null);

    private string Describe(StripSlot slot)
    {
        if (slot.Text is null)
        {
            return $"Add an effect to {Name}";
        }
        if (slot.Overflow)
        {
            return slot.Enabled ? slot.Text : $"{slot.Text}, all off";
        }
        return slot.Effect is null ? slot.Text : $"{slot.Text}, {(slot.Enabled ? "on" : "off")}";
    }

    private string HintOf(StripSlot slot)
    {
        if (!slot.Opens)
        {
            return Master ? MasterNote ?? string.Empty : Describe(slot);
        }
        return $"{Describe(slot)}. Open in the Effects tab ({Shortcuts.OpenEffects.Display})";
    }

    private Task OpenAsync(StripSlot slot) => Track is { } track ? OnOpenEffects.InvokeAsync((track, slot.Effect)) : Task.CompletedTask;

    private Task MuteAsync() => Track is { } track ? OnMute.InvokeAsync(track) : Task.CompletedTask;

    private Task SoloAsync() => Track is { } track ? OnSolo.InvokeAsync(track) : Task.CompletedTask;

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
        string effects = string.Join(',', Effects.Select(effect => $"{effect.Id?.Value}:{effect.Name}:{effect.Enabled}"));
        return $"{Name}|{ColorToken}|{effects}|{Pan}|{Volume}|{Muted}|{Soloed}|{Master}|{Selected}|{Editable}|{Reason}|{Silence}" +
            $"|{LimiterOn}|{OnVolume.HasDelegate}|{OnLimiter.HasDelegate}";
    }
}
