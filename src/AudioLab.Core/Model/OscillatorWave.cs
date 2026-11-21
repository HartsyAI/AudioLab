using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The shape of a synth oscillator.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OscillatorWave>))]
public enum OscillatorWave
{
    /// <summary>A sine wave.</summary>
    [JsonStringEnumMemberName("sine")]
    Sine,

    /// <summary>A triangle wave.</summary>
    [JsonStringEnumMemberName("triangle")]
    Triangle,

    /// <summary>A square wave.</summary>
    [JsonStringEnumMemberName("square")]
    Square,

    /// <summary>A sawtooth wave.</summary>
    [JsonStringEnumMemberName("sawtooth")]
    Sawtooth,
}
