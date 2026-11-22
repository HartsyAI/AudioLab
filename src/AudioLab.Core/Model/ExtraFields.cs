using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>JSON fields this version does not know, kept in canonical form; equal when their JSON values are.</summary>
/// <remarks>Canonical: keys in ordinal order and numbers as exact decimals without trailing zeros, so 1 and 1.0 are the same.</remarks>
public sealed class ExtraFields : IEquatable<ExtraFields>
{
    private static readonly ImmutableSortedDictionary<string, string> _empty =
        ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);

    // The largest power of ten kept exactly, and the most zeros written out in plain notation.
    private const long MaxExponent = 100_000;

    private const long MaxPlainZeros = 30;

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
        ImmutableSortedDictionary<string, string> kept = _empty.AddRange(fields.Select(Keep));
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

    private static KeyValuePair<string, string> Keep(KeyValuePair<string, JsonElement> field)
    {
        if (!TextRules.IsWellFormed(field.Key))
        {
            throw new ArgumentException("The name of an unknown field holds an unpaired surrogate.", nameof(field));
        }
        return KeyValuePair.Create(field.Key, Canonical(field.Value));
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Canonical(JsonElement element)
    {
        try
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream))
            {
                Write(writer, element);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (InvalidOperationException exception)
        {
            // The writer refuses text that is not valid UTF-16, such as an escaped surrogate with no partner.
            throw new ArgumentException("An unknown field holds text with an unpaired surrogate.", nameof(element), exception);
        }
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

    // The number as an exact decimal: digits without leading or trailing zeros and one power of ten, never rounded.
    private static void WriteNumber(Utf8JsonWriter writer, JsonElement number)
    {
        string raw = number.GetRawText();
        bool negative = raw[0] == '-';
        string unsigned = negative ? raw[1..] : raw;
        int exponentAt = unsigned.IndexOfAny(['e', 'E']);
        string mantissa = exponentAt < 0 ? unsigned : unsigned[..exponentAt];
        long written = 0;
        bool parsed = exponentAt < 0 || long.TryParse(unsigned[(exponentAt + 1)..], CultureInfo.InvariantCulture, out written);
        if (!parsed || written is < -MaxExponent or > MaxExponent)
        {
            throw new ArgumentException("An unknown field holds a number too large or too small to keep exactly.", nameof(number));
        }
        int point = mantissa.IndexOf('.', StringComparison.Ordinal);
        string fraction = point < 0 ? string.Empty : mantissa[(point + 1)..];
        string digits = (point < 0 ? mantissa : mantissa[..point]) + fraction;
        long exponent = written - fraction.Length;
        digits = digits.TrimStart('0');
        string trimmed = digits.TrimEnd('0');
        exponent += digits.Length - trimmed.Length;
        writer.WriteRawValue(trimmed.Length == 0 ? "0" : (negative ? "-" : string.Empty) + Plain(trimmed, exponent));
    }

    // Plain notation while it stays short, scientific beyond that; both read back as the same number.
    private static string Plain(string digits, long exponent)
    {
        if (exponent >= 0 && exponent <= MaxPlainZeros)
        {
            return digits + new string('0', (int)exponent);
        }
        long afterPoint = -exponent;
        if (exponent < 0 && afterPoint < digits.Length)
        {
            return digits[..(digits.Length - (int)afterPoint)] + "." + digits[(digits.Length - (int)afterPoint)..];
        }
        if (exponent < 0 && afterPoint - digits.Length <= MaxPlainZeros)
        {
            return "0." + new string('0', (int)(afterPoint - digits.Length)) + digits;
        }
        string rest = digits.Length > 1 ? "." + digits[1..] : string.Empty;
        return digits[0] + rest + "e" + (exponent + digits.Length - 1).ToString(CultureInfo.InvariantCulture);
    }
}
