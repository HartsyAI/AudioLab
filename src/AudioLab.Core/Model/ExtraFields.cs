using System.Collections.Immutable;
using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>JSON fields this version does not know, kept as written; equal when they hold the same JSON.</summary>
public sealed class ExtraFields : IEquatable<ExtraFields>
{
    private static readonly ImmutableSortedDictionary<string, string> _empty =
        ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);

    private readonly ImmutableSortedDictionary<string, string> _fields;

    private ExtraFields(ImmutableSortedDictionary<string, string> fields) => _fields = fields;

    /// <summary>No extra fields.</summary>
    public static ExtraFields Empty { get; } = new(_empty);

    /// <summary>How many fields are kept.</summary>
    public int Count => _fields.Count;

    /// <summary>Whether no field is kept.</summary>
    public bool IsEmpty => _fields.IsEmpty;

    /// <summary>The names of the kept fields, in order.</summary>
    public IEnumerable<string> Names => _fields.Keys;

    /// <summary>A copy of the field's value; a new element on every call.</summary>
    public JsonElement this[string name] => Parse(_fields[name]);

    /// <summary>Keeps a copy of each field given, or returns <see cref="Empty"/> for none.</summary>
    public static ExtraFields From(IEnumerable<KeyValuePair<string, JsonElement>>? fields)
    {
        if (fields is null)
        {
            return Empty;
        }
        ImmutableSortedDictionary<string, string> kept = _empty.AddRange(
            fields.Select(field => KeyValuePair.Create(field.Key, field.Value.GetRawText())));
        return kept.IsEmpty ? Empty : new ExtraFields(kept);
    }

    /// <summary>Keeps fields given as JSON text, such as <c>("seedNote", "\"x\"")</c>.</summary>
    public static ExtraFields FromJson(params (string Name, string Json)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return From(fields.Select(field => KeyValuePair.Create(field.Name, Parse(field.Json))));
    }

    /// <summary>Whether a field with this name is kept.</summary>
    public bool Contains(string name) => _fields.ContainsKey(name);

    /// <inheritdoc />
    public bool Equals(ExtraFields? other)
    {
        return other is not null && _fields.Count == other._fields.Count
            && _fields.All(field => other._fields.TryGetValue(field.Key, out string? json) && json == field.Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ExtraFields other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (KeyValuePair<string, string> field in _fields)
        {
            hash.Add(field.Key);
            hash.Add(field.Value);
        }
        return hash.ToHashCode();
    }

    internal Dictionary<string, JsonElement> ToDictionary()
    {
        return _fields.ToDictionary(field => field.Key, field => Parse(field.Value), StringComparer.Ordinal);
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
