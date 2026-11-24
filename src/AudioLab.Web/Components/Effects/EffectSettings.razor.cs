using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.Effects;

/// <summary>The settings of one effect: a knob for each numeric setting, a sync toggle and divisions for a delay, and its kind's presets.</summary>
public sealed partial class EffectSettings
{
    /// <summary>The id of the preset menu, whose button a shortcut moves the focus to.</summary>
    public const string PresetMenuId = "effect-presets";

    private (Effect? Effect, int Index, bool Editable, string? Reason) _shown;

    /// <summary>The effect.</summary>
    [Parameter, EditorRequired]
    public Effect Effect { get; set; } = null!;

    /// <summary>Its place in the chain, from 0.</summary>
    [Parameter]
    public int Index { get; set; }

    /// <summary>Whether the controls act; when not, they are disabled and say why.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why the controls do nothing now; null when they act.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the effect, the setting's name and its new absolute value.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, string Name, double Value)> OnSetting { get; set; }

    /// <summary>Raised with the delay, whether it follows the tempo, and the division when one was chosen.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, bool Sync, string? Division)> OnSync { get; set; }

    /// <summary>Raised with the effect and the name of the preset chosen.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, string Preset)> OnPreset { get; set; }

    /// <summary>Raised when a knob goes away in the middle of a drag, as when the effect it belongs to is gone.</summary>
    [Parameter]
    public EventCallback OnAbandon { get; set; }

    private static IReadOnlyList<SegmentItem> DivisionItems { get; } =
        [.. EffectRules.Divisions.Select(division => new SegmentItem(division, $"Note division {division}", Label: division))];

    private EffectKind Kind => EffectRules.KindOf(Effect);

    private string Subject => $"{EffectText.Name(Kind)} {Index + 1}";

    private DelayEffect? Delay => Effect as DelayEffect;

    private IReadOnlyList<EffectParameter> Settings => EffectRules.Parameters(Kind);

    private IReadOnlyList<MenuItem> PresetItems =>
        [.. EffectPresets.For(Kind).Select(preset => new MenuItem(preset.Name, preset.Name, string.Empty))];

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // The first render does not ask ShouldRender, so what it drew is remembered here; without it the next change would draw again for nothing.
        if (firstRender)
        {
            _shown = Snapshot();
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (Effect?, int, bool, string?) now = Snapshot();
        bool same = _shown == now;
        _shown = now;
        return !same;
    }

    private (Effect?, int, bool, string?) Snapshot() => (Effect, Index, Editable, Reason);

    private string? BlockOf(EffectParameter setting) => EffectSettingRules.Block(Effect, setting, Editable, Reason);

    private Task SetAsync((string Name, double Value) change) => OnSetting.InvokeAsync((Effect.Id, change.Name, change.Value));

    private Task ToggleSyncAsync() => OnSync.InvokeAsync((Effect.Id, Delay is { Sync: false }, null));

    private Task DivisionAsync(string division) => OnSync.InvokeAsync((Effect.Id, true, division));

    private Task PresetAsync(MenuItem item) => OnPreset.InvokeAsync((Effect.Id, item.Id));
}
