using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Routes a track to an aux bus, post-fader; each track sends to a bus once.</summary>
public sealed record AddSend : Command
{
    /// <summary>The track to send from; not a bus.</summary>
    [Description("The track to send from; not a bus.")]
    public required TrackId Track { get; init; }

    /// <summary>The bus track to send to.</summary>
    [Description("The bus track to send to.")]
    public required TrackId Bus { get; init; }

    /// <summary>The send level, from 0 to 16; 0.5 when absent.</summary>
    [Description("The send level, from 0 to 16; 0.5 when absent.")]
    [Range(0, ProjectLimits.MaxGain)]
    public double? Level { get; init; }
}
