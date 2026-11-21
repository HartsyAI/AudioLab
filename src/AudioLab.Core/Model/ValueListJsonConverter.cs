using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes a <see cref="ValueList{T}"/> as a JSON array; the context lists the item type.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class ValueListJsonConverter<T> : JsonConverter<ValueList<T>>
{
    /// <inheritdoc />
    public override ValueList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"A list of {typeof(T).Name} must be a JSON array.");
        }
        JsonTypeInfo<T> info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        List<T> items = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            T item = JsonSerializer.Deserialize(ref reader, info)
                ?? throw new JsonException($"A list of {typeof(T).Name} cannot hold null.");
            items.Add(item);
        }
        return new ValueList<T>(items);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ValueList<T> value, JsonSerializerOptions options)
    {
        JsonTypeInfo<T> info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        writer.WriteStartArray();
        foreach (T item in value)
        {
            JsonSerializer.Serialize(writer, item, info);
        }
        writer.WriteEndArray();
    }
}
