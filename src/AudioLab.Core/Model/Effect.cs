using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>One insert effect of a track. Inserts run before the fader, in list order.</summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(EqEffect), "eq")]
[JsonDerivedType(typeof(CompressorEffect), "compressor")]
[JsonDerivedType(typeof(ReverbEffect), "reverb")]
[JsonDerivedType(typeof(DelayEffect), "delay")]
[JsonDerivedType(typeof(SaturationEffect), "saturation")]
public abstract record Effect
{
    private protected Effect()
    {
    }

    /// <summary>The effect's id.</summary>
    public required EffectId Id { get; init; }

    /// <summary>Whether the effect is in the signal path.</summary>
    public required bool Enabled { get; init; }
}
