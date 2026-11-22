using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Writes a typed id as a plain JSON string and refuses anything that is not a well-formed id.</summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public sealed class IdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IId<TId>
{
    /// <inheritdoc />
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"An id must be a string.");
        }
        string? value = reader.GetString();
        if (!IdRules.IsValid(value))
        {
            throw new JsonException(IdRules.Description);
        }
        return TId.From(value!);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
