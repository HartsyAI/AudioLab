using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The score a clip was rendered from. Unknown fields are kept in <see cref="Extra"/>.</summary>
public sealed record ScoreMeta : IJsonOnDeserialized
{
    private ExtraFields _extra = ExtraFields.Empty;

    private Dictionary<string, JsonElement>? _incoming;

    /// <summary>The score in ABC notation.</summary>
    public string? Abc { get; init; }

    /// <summary>Whether the engine cut the score short to fit its budget.</summary>
    public bool? Truncated { get; init; }

    /// <summary>The length the engine was allowed, in seconds.</summary>
    public double? BudgetSeconds { get; init; }

    /// <summary>The style prompt.</summary>
    public string? Style { get; init; }

    /// <summary>The lyrics prompt.</summary>
    public string? Lyrics { get; init; }

    /// <summary>How much planning the engine did before writing the score.</summary>
    public string? Cot { get; init; }

    /// <summary>The seed of the render.</summary>
    public long? Seed { get; init; }

    /// <summary>The model that rendered it.</summary>
    public string? Model { get; init; }

    /// <summary>The engine that rendered it.</summary>
    public string? EngineId { get; init; }

    /// <summary>The version label shown in the score tab.</summary>
    public string? Label { get; init; }

    /// <summary>The clip this score is a variation of, or null for a root version.</summary>
    [JsonConverter(typeof(OptionalIdJsonConverter<ClipId>))]
    public ClipId? Parent { get; init; }

    /// <summary>How the score came to be, such as planned or rendered.</summary>
    public string? Source { get; init; }

    /// <summary>When it was made, in milliseconds since 1970.</summary>
    public long? Created { get; init; }

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
        if (_incoming is null)
        {
            return;
        }
        _extra = ExtraFields.From(_incoming);
        _incoming = null;
    }
}
