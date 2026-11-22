using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the level of a send.</summary>
public sealed record SetSendLevel : Command
{
    /// <summary>The track that sends.</summary>
    [Description("The track that sends.")]
    public required TrackId Track { get; init; }

    /// <summary>The bus it sends to.</summary>
    [Description("The bus it sends to.")]
    public required TrackId Bus { get; init; }

    /// <summary>The send level, from 0 to 16.</summary>
    [Description("The send level, from 0 to 16.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Level { get; init; }
}
