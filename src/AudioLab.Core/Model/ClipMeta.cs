using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Free-form clip data. Fields this version does not know are kept in <see cref="Extra"/>.</summary>
public sealed record ClipMeta : IJsonOnDeserialized
{
    private ExtraFields _extra = ExtraFields.Empty;

    private Dictionary<string, JsonElement>? _incoming;

    /// <summary>The score that produced the clip.</summary>
    public ScoreMeta? Score { get; init; }

    /// <summary>The lyrics of the clip.</summary>
    public ClipLyrics? Lyrics { get; init; }

    /// <summary>The clip this one is a separated stem of.</summary>
    public StemOf? StemOf { get; init; }

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
