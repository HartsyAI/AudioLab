using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes an enum as exactly the name its <see cref="JsonStringEnumMemberNameAttribute"/> gives.</summary>
/// <typeparam name="TEnum">The enum type; every member carries a member name attribute.</typeparam>
public sealed class StrictEnumConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    : JsonConverter<TEnum>, INamedEnumConverter
    where TEnum : struct, Enum
{
    private readonly Dictionary<string, TEnum> _byName = new(StringComparer.Ordinal);

    private readonly Dictionary<TEnum, string> _byValue = [];

    private readonly List<string> _names = [];

    /// <summary>Reads the member names of <typeparamref name="TEnum"/> once.</summary>
    public StrictEnumConverter()
    {
        foreach (FieldInfo field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            string name = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name;
            TEnum value = (TEnum)field.GetValue(null)!;
            _byName[name] = value;
            _byValue[value] = name;
            _names.Add(name);
        }
    }

    /// <inheritdoc />
    IReadOnlyList<string> INamedEnumConverter.Names => _names;

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is not null && _byName.TryGetValue(text, out TEnum value))
        {
            return value;
        }
        throw new JsonException($"The value is one of: {string.Join(", ", _byName.Keys)}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!_byValue.TryGetValue(value, out string? name))
        {
            throw new JsonException($"{value} is not a known value.");
        }
        writer.WriteStringValue(name);
    }
}
