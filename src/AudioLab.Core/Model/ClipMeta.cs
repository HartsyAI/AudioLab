using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Free-form clip data. Fields this version does not know are kept in <see cref="Extra"/>.</summary>
public sealed record ClipMeta
{
    /// <summary>The score that produced the clip.</summary>
    public ScoreMeta? Score { get; init; }

    /// <summary>The lyrics of the clip.</summary>
    public ClipLyrics? Lyrics { get; init; }

    /// <summary>The clip this one is a separated stem of.</summary>
    public StemOf? StemOf { get; init; }

    /// <summary>Every other field, kept as it was written.</summary>
    // System.Text.Json cannot fill extension data through an init-only property.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
