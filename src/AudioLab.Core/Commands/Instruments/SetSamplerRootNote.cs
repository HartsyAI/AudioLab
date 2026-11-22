using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Sets the MIDI note at which a sampler plays its sample at its own pitch.</summary>
public sealed record SetSamplerRootNote : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The MIDI note, 0 to 127; 60 is middle C.</summary>
    [Description("The MIDI note, 0 to 127; 60 is middle C.")]
    [Range(InstrumentRules.LowestNote, InstrumentRules.HighestNote)]
    public required int Note { get; init; }
}
