using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Effects;

/// <summary>The words an effect kind is shown with, one place so the slot, the chain and the menus agree.</summary>
public static class EffectText
{
    /// <summary>Every kind of effect, in the order a menu lists them.</summary>
    public static IReadOnlyList<EffectKind> Kinds { get; } = Enum.GetValues<EffectKind>();

    /// <summary>The name of a kind, in sentence case except for the letters of an acronym.</summary>
    public static string Name(EffectKind kind)
    {
        return kind switch
        {
            EffectKind.Eq => "EQ",
            EffectKind.Compressor => "Compressor",
            EffectKind.Reverb => "Reverb",
            EffectKind.Delay => "Delay",
            EffectKind.Saturation => "Saturation",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "That kind of effect has no name here."),
        };
    }
}
