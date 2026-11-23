using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a time range from the clips of some tracks; in shuffle edit mode the later clips close the gap.</summary>
public sealed record DeleteRange : Command
{
    /// <summary>Where the range starts, in seconds.</summary>
    [Description("Where the range starts, in seconds.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Start { get; init; }

    /// <summary>Where the range ends, in seconds; at least 0.001 after the start.</summary>
    [Description("Where the range ends, in seconds; at least 0.001 after the start.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double End { get; init; }

    /// <summary>The tracks the range applies to, from 1 to 1024.</summary>
    [Description("The tracks the range applies to, from 1 to 1024.")]
    [JsonConverter(typeof(ValueListJsonConverter<TrackId>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxTracks)]
    public required ValueList<TrackId> Tracks { get; init; }
}
