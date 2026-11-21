using System.Text.Json;
using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>Turns a JSON error into one line that says where it happened.</summary>
internal static partial class JsonErrors
{
    /// <summary>Where inside a list the error happened; list converters add the index of the item they read.</summary>
    public const string ItemKey = "AudioLab.ItemPath";

    /// <summary>The path, a colon and the message, cut to a readable length.</summary>
    public static string Describe(JsonException exception)
    {
        string path = (exception.Path ?? "$") + (exception.Data[ItemKey] as string);
        string message = BaseMessage(exception);
        if (message.Contains("'extensionData'", StringComparison.Ordinal))
        {
            string name = LastName(exception.Path, exception.Data[ItemKey] as string);
            message = message.Replace("'extensionData'", $"'{name}'", StringComparison.Ordinal);
        }
        return Clean($"{path}: {message}");
    }

    /// <summary>A message without the .NET type names the serializer puts in it, cut to a readable length.</summary>
    public static string Clean(string message)
    {
        string plain = Regex.Replace(message, @"\bAudioLab\.Core\.Model\.", string.Empty);
        plain = MissingType().Replace(plain, "a 'type' member is required.");
        plain = NonStringType().Replace(plain, "'type' must be a string.");
        plain = NullNotAllowed().Replace(plain, "must not be null.");
        plain = Unmapped().Replace(plain, "is not a member this version knows");
        plain = Unconverted().Replace(plain, "is not of the expected kind");
        plain = TypeSuffix().Replace(plain, string.Empty);
        return ProjectLimits.Shorten(plain);
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

    /// <summary>A serializer error that names no member in its type, as one that does: its message and its path.</summary>
    public static JsonException FromNotSupported(NotSupportedException exception)
    {
        Match path = PathInMessage().Match(exception.Message);
        string where = path.Success ? path.Groups[1].Value : "$";
        int at = exception.Message.IndexOf(" Path: ", StringComparison.Ordinal);
        string message = at < 0 ? exception.Message : exception.Message[..at];
        return new JsonException(message, where, null, null, exception);
    }

    [GeneratedRegex(@"The JSON payload for polymorphic interface or abstract type '[^']*' must specify a type discriminator\.?")]
    private static partial Regex MissingType();

    [GeneratedRegex(@"The '\$id', '\$ref' or '\$type' metadata properties must be JSON strings\.[^.]*\.?")]
    private static partial Regex NonStringType();

    [GeneratedRegex(@"The (?:constructor parameter|property or field) '[^']*'(?: on type '[^']*')? doesn't allow (?:setting )?null values\.(?: Consider updating its nullability annotation\.)?")]
    private static partial Regex NullNotAllowed();

    [GeneratedRegex(@"could not be mapped to any \.NET member contained in type '[^']*'")]
    private static partial Regex Unmapped();

    [GeneratedRegex(@"could not be converted to [\w.`\[\],<>+]+")]
    private static partial Regex Unconverted();

    [GeneratedRegex(@" (?:of|for|on) type '[^']*'")]
    private static partial Regex TypeSuffix();

    [GeneratedRegex(@"Path: (\$[^ |]*)")]
    private static partial Regex PathInMessage();

    // The name of the member an error is about: the last step of the path.
    private static string LastName(string? path, string? inItem)
    {
        string full = (path ?? "$") + inItem;
        Match last = LastStep().Match(full);
        return !last.Success ? full : last.Groups[1].Success ? last.Groups[1].Value : last.Groups[2].Value;
    }

    [GeneratedRegex(@"(?:\.([^.\[\]]+)|\['([^']+)'\])$")]
    private static partial Regex LastStep();

    // The serializer appends " Path: ... | LineNumber: ..." to a message and the reader " LineNumber: ...";
    // the path is shown separately and a line or byte position means nothing to a person with a project file.
    private static string BaseMessage(JsonException exception)
    {
        string message = exception.Message;
        int path = message.IndexOf(" Path: ", StringComparison.Ordinal);
        int line = message.IndexOf(" LineNumber: ", StringComparison.Ordinal);
        int at = path < 0 ? line : line < 0 ? path : Math.Min(path, line);
        return at < 0 ? message : message[..at];
    }
}
