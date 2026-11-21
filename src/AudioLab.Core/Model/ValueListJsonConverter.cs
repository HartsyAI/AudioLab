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
            throw new JsonException($"A list must be a JSON array.");
        }
        JsonTypeInfo<T> info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        List<T> items = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            items.Add(ReadItem(ref reader, info, items.Count));
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

    // The item is read by a new serializer call, which starts its path at the item; the index is put back in front.
    private static T ReadItem(ref Utf8JsonReader reader, JsonTypeInfo<T> info, int index)
    {
        T? item;
        try
        {
            item = JsonSerializer.Deserialize(ref reader, info);
        }
        catch (JsonException exception)
        {
            throw JsonErrors.InItem(exception, index);
        }
        catch (NotSupportedException exception)
        {
            throw JsonErrors.InItem(JsonErrors.FromNotSupported(exception), index);
        }
        JsonException none = new($"A list cannot hold null.");
        return item ?? throw JsonErrors.InItem(none, index);
    }
}
