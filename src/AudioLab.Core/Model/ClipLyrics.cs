using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The lyrics a clip carries and how they were timed; unknown fields are kept in <see cref="Extra"/>.</summary>
public sealed record ClipLyrics : IJsonOnDeserialized
{
    private ExtraFields _extra = ExtraFields.Empty;

    private Dictionary<string, JsonElement>? _incoming;

    /// <summary>The text the user sees and edits; section tags such as [Verse 1] are allowed.</summary>
    public required string Text { get; init; }

    /// <summary>The timed words, in order.</summary>
    [JsonConverter(typeof(ValueListJsonConverter<LyricWord>))]
    public required ValueList<LyricWord> Words { get; init; }

    /// <summary>Where the lyrics came from, such as manual, generated, estimated or transcribed.</summary>
    public string? Source { get; init; }

    /// <summary>The model that generated or aligned them.</summary>
    public string? Model { get; init; }

    /// <summary>Every other field, kept as it was written.</summary>
    [JsonIgnore]
    public ExtraFields Extra
    {
        get => _extra;
        init => _extra = value ?? throw new ArgumentNullException(nameof(value));
    }

    // System.Text.Json fills this dictionary while it reads; it is turned into Extra once the object is complete.
    [JsonInclude]
    [JsonExtensionData]
    internal Dictionary<string, JsonElement>? ExtensionData
    {
        get => _incoming ?? (_extra.IsEmpty ? null : _extra.ToDictionary());
        set => _incoming = value;
    }

    void IJsonOnDeserialized.OnDeserialized()
    {
        _extra = ExtraFields.From(_incoming);
        _incoming = null;
    }
}
