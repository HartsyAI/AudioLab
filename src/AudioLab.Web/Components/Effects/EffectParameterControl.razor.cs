using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.Effects;

/// <summary>One numeric setting of an effect as a knob with its label: range, label and unit are Core's, the default is a new effect's.</summary>
public sealed partial class EffectParameterControl
{
    private readonly Func<double, string> _format;

    private (Effect? Effect, EffectParameter? Setting, string? Subject, bool Disabled, string? Reason) _shown;

    /// <summary>Creates the control.</summary>
    public EffectParameterControl()
    {
        _format = position => EffectValueText.Of(Setting, Setting.FromPosition((position + 1) / 2));
    }

    /// <summary>The setting, with its range, label, unit and scale.</summary>
    [Parameter, EditorRequired]
    public EffectParameter Setting { get; set; } = null!;

    /// <summary>The effect that holds the setting.</summary>
    [Parameter, EditorRequired]
    public Effect Effect { get; set; } = null!;

    /// <summary>What the knob is a setting of, as a screen reader says it, such as "Reverb 2".</summary>
    [Parameter, EditorRequired]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Whether the knob does nothing now.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why it does nothing now.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the setting's name and its new absolute value.</summary>
    [Parameter]
    public EventCallback<(string Name, double Value)> OnSet { get; set; }

    private double Value => Setting.Get(Effect);

    // The knob's place from -1 to 1.
    private double Position => (Setting.ToPosition(Value) * 2) - 1;

    private double DefaultPosition => (Setting.ToPosition(EffectRules.Default(EffectRules.KindOf(Effect), Setting)) * 2) - 1;

    private string ValueText => EffectValueText.Of(Setting, Value);

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (Effect?, EffectParameter?, string?, bool, string?) now = (Effect, Setting, Subject, Disabled, Reason);
        bool same = _shown == now;
        _shown = now;
        return !same;
    }

    private Task CommitAsync(double position) => OnSet.InvokeAsync((Setting.Name, Setting.FromPosition((position + 1) / 2)));
}
