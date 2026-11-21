using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>JSON fields this version does not know, kept in canonical form; equal when their JSON values are.</summary>
/// <remarks>Canonical: keys in ordinal order and numbers without trailing zeros, so 1 and 1.0 are the same.</remarks>
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

    /// <summary>How many characters the names and the JSON of all kept fields take.</summary>
    public long TotalLength => _fields.Sum(pair => (long)pair.Key.Length + pair.Value.Length);

    /// <summary>A copy of the field's value; a new element on every call.</summary>
    public JsonElement this[string name] => Parse(_fields[name]);

    /// <summary>Keeps a canonical copy of each field given, or returns <see cref="Empty"/> for none.</summary>
    public static ExtraFields From(IEnumerable<KeyValuePair<string, JsonElement>>? fields)
    {
        if (fields is null)
        {
            return Empty;
        }
        ImmutableSortedDictionary<string, string> kept = _empty.AddRange(
            fields.Select(field => KeyValuePair.Create(field.Key, Canonical(field.Value))));
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

    private static string Canonical(JsonElement element)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            Write(writer, element);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                SortedDictionary<string, JsonElement> members = new(StringComparer.Ordinal);
                foreach (JsonProperty member in element.EnumerateObject())
                {
                    members[member.Name] = member.Value;
                }
                foreach (KeyValuePair<string, JsonElement> member in members)
                {
                    writer.WritePropertyName(member.Key);
                    Write(writer, member.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Write(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                WriteNumber(writer, element);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }

    // Dividing by a one with many zeros drops trailing zeros, so 1.0 and 1 are written alike.
    private static void WriteNumber(Utf8JsonWriter writer, JsonElement number)
    {
        if (number.TryGetDecimal(out decimal exact))
        {
            writer.WriteNumberValue(exact / 1.000000000000000000000000000000000m);
        }
        else if (number.TryGetDouble(out double approximate) && double.IsFinite(approximate))
        {
            writer.WriteNumberValue(approximate);
        }
        else
        {
            writer.WriteRawValue(number.GetRawText());
        }
    }
}
