using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Builds the result of a tool call: the answer as JSON, sent once, as the text a client shows and a program parses.</summary>
/// <remarks>The JSON is not repeated as structured content: a client puts the whole result in its context, so it would count twice.</remarks>
public static class McpResults
{
    /// <summary>A successful answer.</summary>
    public static CallToolResult Ok<T>(T value, JsonTypeInfo<T> info)
    {
        return Result(JsonSerializer.Serialize(value, info), isError: false);
    }

    /// <summary>A successful answer that is already a JSON tree.</summary>
    public static CallToolResult Json(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Result(node.ToJsonString(), isError: false);
    }

    /// <summary>The answer to a command, an undo or a redo; an error result when it was refused.</summary>
    public static CallToolResult Reply(CommandReply reply)
    {
        return Result(JsonSerializer.Serialize(reply, HubJsonContext.Default.CommandReply), isError: !reply.Succeeded);
    }

    /// <summary>A refusal with one reason and its class; the revision is 0 when the project was not reached.</summary>
    public static CallToolResult Refused(CommandErrorCode code, string message, long revision = 0)
    {
        return Reply(new CommandReply(false, false, revision, [new HubCommandError(code, message)], []));
    }

    /// <summary>The length in characters of the text of <paramref name="result"/>.</summary>
    public static int Length(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Content.OfType<TextContentBlock>().Sum(block => block.Text.Length);
    }

    private static CallToolResult Result(string json, bool isError)
    {
        return new CallToolResult
        {
            IsError = isError,
            Content = [new TextContentBlock { Text = json }],
        };
    }
}
