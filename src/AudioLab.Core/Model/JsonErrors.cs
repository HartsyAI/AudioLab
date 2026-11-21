using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Turns a JSON error into one line that says where it happened.</summary>
internal static class JsonErrors
{
    /// <summary>The error message, led by the JSON path when the message does not already carry it.</summary>
    public static string Describe(JsonException exception)
    {
        bool hasPath = exception.Message.Contains("Path:", StringComparison.Ordinal);
        return exception.Path is { } path && !hasPath ? $"{path}: {exception.Message}" : exception.Message;
    }
}
