using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the wave of one of a synth's two oscillators.</summary>
public sealed record SetOscillatorWave : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>Which oscillator, 1 or 2.</summary>
    [Description("Which oscillator, 1 or 2.")]
    [AllowedValues(1, 2)]
    public required int Oscillator { get; init; }

    /// <summary>sine, triangle, square or sawtooth.</summary>
    [Description("sine, triangle, square or sawtooth.")]
    public required OscillatorWave Wave { get; init; }
}
