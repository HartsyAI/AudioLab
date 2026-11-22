using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using AudioLab.Server.Realtime;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Builds the result of a tool call: the answer as JSON, in the text a client shows and in the structured content a program reads.</summary>
public static class McpResults
{
    /// <summary>A successful answer.</summary>
    public static CallToolResult Ok<T>(T value, JsonTypeInfo<T> info)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, info);
        return Result(element, isError: false);
    }

    /// <summary>The answer to a command, an undo or a redo; an error result when it was refused.</summary>
    public static CallToolResult Reply(CommandReply reply)
    {
        JsonElement element = JsonSerializer.SerializeToElement(reply, HubJsonContext.Default.CommandReply);
        return Result(element, isError: !reply.Succeeded);
    }

    /// <summary>A refusal with one reason and its class; the revision is 0 when the project was not reached.</summary>
    public static CallToolResult Refused(CommandErrorCode code, string message, long revision = 0)
    {
        return Reply(new CommandReply(false, false, revision, [new HubCommandError(code, message)], []));
    }

    private static CallToolResult Result(JsonElement element, bool isError)
    {
        return new CallToolResult
        {
            IsError = isError,
            Content = [new TextContentBlock { Text = element.GetRawText() }],
            StructuredContent = element,
        };
    }
}
