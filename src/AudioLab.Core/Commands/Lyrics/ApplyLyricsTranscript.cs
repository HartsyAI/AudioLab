using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives a clip lyrics written from a transcript, split into lines by pauses and length.</summary>
public sealed record ApplyLyricsTranscript : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The words as the model heard them, from 1 to 1000000.</summary>
    [Description("The words as the model heard them, from 1 to 1000000.")]
    [JsonConverter(typeof(ValueListJsonConverter<HeardWord>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<HeardWord> Words { get; init; }

    /// <summary>Where the audio that was sent began in the clip's source, in seconds; the clip's own offset when absent.</summary>
    [Description(
        "Where the audio that was sent began in the clip's source, in seconds; the clip's own offset when " +
        "absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? Offset { get; init; }

    /// <summary>The model that transcribed it, up to 128 characters.</summary>
    [Description("The model that transcribed it, up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? Model { get; init; }

    /// <summary>Where the words came from, up to 128 characters; stt when absent.</summary>
    [Description("Where the words came from, up to 128 characters; stt when absent.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? Source { get; init; }
}
