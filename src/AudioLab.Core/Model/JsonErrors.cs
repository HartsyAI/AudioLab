using System.Text.Json;
using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>Turns a JSON error into one line that says where it happened.</summary>
internal static class JsonErrors
{
    /// <summary>Where inside a list the error happened; list converters add the index of the item they read.</summary>
    public const string ItemKey = "AudioLab.ItemPath";

    /// <summary>The path, a colon and the message, cut to a readable length.</summary>
    public static string Describe(JsonException exception)
    {
        string path = (exception.Path ?? "$") + (exception.Data[ItemKey] as string);
        // The serializer names the .NET type it failed on; the reader of an error has no use for the namespace.
        string message = Regex.Replace(BaseMessage(exception), @"\bAudioLab\.Core\.(?:Model|Migration)\.(?:V3)?", string.Empty);
        return ProjectLimits.Shorten($"{path}: {message}");
    }

    /// <summary>The exception for a list item, which carries the item's index in front of the path inside it.</summary>
    public static JsonException InItem(JsonException exception, int index)
    {
        string inside = exception.Path is { Length: > 1 } path ? path[1..] : string.Empty;
        string inner = inside + (exception.Data[ItemKey] as string);
        JsonException wrapped = new(BaseMessage(exception), exception);
        wrapped.Data[ItemKey] = $"[{index}]{inner}";
        return wrapped;
    }

    // The serializer appends " Path: ... | LineNumber: ..." to a message; the path is shown separately.
    private static string BaseMessage(JsonException exception)
    {
        int at = exception.Message.IndexOf(" Path: ", StringComparison.Ordinal);
        return at < 0 ? exception.Message : exception.Message[..at];
    }
}
