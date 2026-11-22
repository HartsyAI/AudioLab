using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The kinds of insert effect a track can hold.</summary>
[JsonConverter(typeof(StrictEnumConverter<EffectKind>))]
public enum EffectKind
{
    /// <summary>A five-band equaliser with high-pass and low-pass filters.</summary>
    [JsonStringEnumMemberName("eq")]
    Eq,

    /// <summary>A compressor.</summary>
    [JsonStringEnumMemberName("compressor")]
    Compressor,

    /// <summary>A reverb.</summary>
    [JsonStringEnumMemberName("reverb")]
    Reverb,

    /// <summary>A delay, free or synced to the tempo.</summary>
    [JsonStringEnumMemberName("delay")]
    Delay,

    /// <summary>A soft saturator.</summary>
    [JsonStringEnumMemberName("saturation")]
    Saturation,
}
