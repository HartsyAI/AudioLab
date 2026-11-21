using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Reads an optional id: null means none, and an empty string is not an id.</summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public sealed class OptionalIdJsonConverter<TId> : JsonConverter<TId?>
    where TId : struct, IId<TId>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override TId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"An id of type {typeof(TId).Name} must be a string or null.");
        }
        string? value = reader.GetString();
        return value is not null && IdRules.IsValid(value)
            ? TId.From(value)
            : throw new JsonException($"An id of type {typeof(TId).Name}: {IdRules.Description}");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TId? value, JsonSerializerOptions options)
    {
        if (value is { } id)
        {
            writer.WriteStringValue(id.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
