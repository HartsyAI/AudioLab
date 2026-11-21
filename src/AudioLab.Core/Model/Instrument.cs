using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The sound source of an instrument track: a synth or a sampler.</summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SynthInstrument), "synth")]
[JsonDerivedType(typeof(SamplerInstrument), "sampler")]
public abstract record Instrument;
