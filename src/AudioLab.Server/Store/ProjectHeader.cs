using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>The id, owner and name at the start of a project file, read without parsing the rest; this is all the list needs.</summary>
/// <param name="Id">The project's id.</param>
/// <param name="Owner">The owner the file names.</param>
/// <param name="Name">The project's name.</param>
public sealed record ProjectHeader(ProjectId Id, UserId Owner, string Name)
{
    /// <summary>How many bytes from the start of a file are looked at; the name is at most 200 characters, so the header is far shorter.</summary>
    public const int PrefixBytes = 16 * 1024;

    /// <summary>Reads the header from the first bytes of a file; false when they do not hold a well-formed id, owner and name.</summary>
    public static bool TryRead(ReadOnlySpan<byte> prefix, out ProjectHeader? header)
    {
        header = null;
        string? id = null;
        string? owner = null;
        string? name = null;
        try
        {
            Utf8JsonReader reader = new(prefix, isFinalBlock: false, default);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }
            while ((id is null || owner is null || name is null) && reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("id"u8))
                {
                    id = ReadString(ref reader);
                }
                else if (reader.ValueTextEquals("owner"u8))
                {
                    owner = ReadString(ref reader);
                }
                else if (reader.ValueTextEquals("name"u8))
                {
                    name = ReadString(ref reader);
                }
                else if (!reader.Read() || (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && !reader.TrySkip()))
                {
                    return false;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
        if (!IdRules.IsValid(id) || !IdRules.IsValid(owner) || name is null || name.Length > ProjectLimits.MaxName)
        {
            return false;
        }
        header = new ProjectHeader(ProjectId.From(id!), UserId.From(owner!), name);
        return true;
    }

    private static string? ReadString(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            return null;
        }
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            // A nested value is never the id, owner or name; its own members must not be read as if they were.
            reader.TrySkip();
            return null;
        }
        return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
    }
}
