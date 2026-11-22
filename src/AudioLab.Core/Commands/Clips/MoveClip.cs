using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves an audio or MIDI clip in time, and to another track of its kind if one is given; on another track it lands over the clips there.</summary>
public sealed record MoveClip : Command
{
    /// <summary>The clip to move.</summary>
    [Description("The clip to move.")]
    public required ClipId Clip { get; init; }

    /// <summary>Where it starts, in seconds; a time before the start of the project becomes 0.</summary>
    [Description("Where it starts, in seconds; a time before the start of the project becomes 0.")]
    public required double Start { get; init; }

    /// <summary>The track to move it to, an audio track for an audio clip and an instrument track for a MIDI clip; absent to stay.</summary>
    [Description("The track to move it to, an audio track for an audio clip and an instrument track for a MIDI clip; absent to stay.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<TrackId>))]
    public TrackId? Track { get; init; }
}
