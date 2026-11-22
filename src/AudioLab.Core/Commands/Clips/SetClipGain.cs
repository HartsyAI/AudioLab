using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the gain of an audio clip.</summary>
public sealed record SetClipGain : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The gain, from 0 to 16; 1 is unity.</summary>
    [Description("The gain, from 0 to 16; 1 is unity.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Gain { get; init; }
}
