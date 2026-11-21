using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Writes a <see cref="NoteId"/> as a JSON number and refuses anything below 1.</summary>
public sealed class NoteIdJsonConverter : JsonConverter<NoteId>
{
    /// <inheritdoc />
    public override NoteId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int value) || value < 1)
        {
            throw new JsonException("A note id must be a whole number of 1 or more.");
        }
        return new NoteId(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, NoteId value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
