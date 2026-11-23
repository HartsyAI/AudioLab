using System.Text.Json;
using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Configuration;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Runs a tool call for the user the door resolved, through the same services as the routes and the hub.</summary>
/// <remarks>Every call opens a project as that user, so a project of another user is never found. A refusal is a result, not an exception.</remarks>
public sealed class McpToolService(
    ProjectService projects,
    ProjectEditService edits,
    McpToolCatalog catalog,
    AudioLabOptions options,
    ILogger<McpToolService> logger)
{
    /// <summary>Runs the tool called <paramref name="name"/>.</summary>
    /// <param name="user">The user the door resolved; the only user whose projects the call can touch.</param>
    /// <param name="name">The tool's name.</param>
    /// <param name="arguments">The arguments the client sent, or null.</param>
    /// <param name="cancellationToken">Cancels the call before a change is made.</param>
    /// <exception cref="McpProtocolException">The server has no tool of that name.</exception>
    public async Task<CallToolResult> CallAsync(
        UserId user,
        string name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!catalog.Contains(name))
        {
            throw new McpProtocolException($"The server has no tool named {name}.", McpErrorCode.InvalidParams);
        }
        CallToolResult result = await RunAsync(user, name, arguments, cancellationToken);

        // Only a read is replaced: the answer to a command, an undo or a redo is a change already made, which a refusal would deny.
        return IsRead(name) && McpResults.Length(result) > options.Mcp.MaxResultChars
            ? McpResults.Refused(CommandErrorCode.Limit, TooLong(name), ReplyRevision(result))
            : result;
    }

    private static bool IsRead(string name) => name is McpToolNames.ListProjects or McpToolNames.GetProject or McpToolNames.GetHistory;

    private async Task<CallToolResult> RunAsync(
        UserId user,
        string name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return name switch
            {
                McpToolNames.ListProjects => await ListProjectsAsync(user, arguments, cancellationToken),
                McpToolNames.GetProject => await GetProjectAsync(user, arguments, cancellationToken),
                McpToolNames.GetHistory => await GetHistoryAsync(user, arguments, cancellationToken),
                McpToolNames.Undo => await StepAsync(arguments, (project, expected) => edits.UndoAsync(user, project, cancellationToken, expected)),
                McpToolNames.Redo => await StepAsync(arguments, (project, expected) => edits.RedoAsync(user, project, cancellationToken, expected)),
                _ => throw new McpProtocolException($"The server has no tool named {name}.", McpErrorCode.InvalidParams),
            };
        }
        catch (ProjectNotFoundException exception)
        {
            return McpResults.Refused(CommandErrorCode.NotFound, exception.Message);
        }
        catch (SessionClosedException)
        {
            return McpResults.Refused(CommandErrorCode.Rejected, "The project was closed or deleted while the call ran; call it again.");
        }
        catch (ProjectFormatException exception)
        {
            McpLog.ProjectNotValid(logger, exception);
            return McpResults.Refused(CommandErrorCode.Rejected, "The project file is not valid, so the project cannot be opened.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            McpLog.StoreUnavailable(logger, exception);
            return McpResults.Refused(CommandErrorCode.Rejected, "The project store is not available right now; call it again.");
        }
    }

    private string TooLong(string name)
    {
        string limit = $"The result is longer than the {options.Mcp.MaxResultChars} characters this server returns.";
        return name == McpToolNames.GetProject
            ? $"{limit} Ask for less: a smaller section, or fewer items with count and offset, or one track or clip."
            : limit;
    }

    private static long ReplyRevision(CallToolResult result)
    {
        using JsonDocument document = JsonDocument.Parse(((TextContentBlock)result.Content[0]).Text);
        return document.RootElement.TryGetProperty("revision", out JsonElement revision) && revision.TryGetInt64(out long value) ? value : 0;
    }

    private async Task<CallToolResult> ListProjectsAsync(
        UserId user,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        if (McpArguments.Read(arguments, McpJsonContext.Default.NoArguments, out CallToolResult? refusal) is null)
        {
            return refusal!;
        }
        ProjectListing listing = await projects.ListAsync(user, cancellationToken);
        return McpResults.Ok(listing, McpJsonContext.Default.ProjectListing);
    }

    private async Task<CallToolResult> GetProjectAsync(UserId user, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        if (McpArguments.Read(arguments, McpJsonContext.Default.GetProjectArguments, out CallToolResult? refusal) is not { } read)
        {
            return refusal!;
        }
        ProjectSnapshot snapshot = await projects.OpenAsync(user, read.Project, cancellationToken);
        return McpProjectReader.Read(snapshot, read);
    }

    private async Task<CallToolResult> GetHistoryAsync(UserId user, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        if (McpArguments.Read(arguments, McpJsonContext.Default.ProjectArguments, out CallToolResult? refusal) is not { } read)
        {
            return refusal!;
        }
        HistoryState state = await edits.HistoryAsync(user, read.Project, cancellationToken);
        return McpResults.Ok(state, McpJsonContext.Default.HistoryState);
    }

    private static async Task<CallToolResult> StepAsync(IDictionary<string, JsonElement>? arguments, Func<ProjectId, long, Task<CommandReply>> step)
    {
        if (McpArguments.Read(arguments, McpJsonContext.Default.StepArguments, out CallToolResult? refusal) is not { } read)
        {
            return refusal!;
        }
        return McpResults.Reply(await step(read.Project, read.ExpectedRevision));
    }
}
