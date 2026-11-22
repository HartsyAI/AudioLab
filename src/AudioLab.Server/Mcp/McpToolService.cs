using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Runs a tool call for the user the door resolved, through the same services as the routes and the hub.</summary>
/// <remarks>Every call opens a project as that user, so a project of another user is never found. A refusal is a result, not an exception.</remarks>
public sealed class McpToolService(ProjectService projects, ProjectEditService edits, McpToolCatalog catalog, AudioLabOptions options)
{
    /// <summary>What every client id of an agent starts with, so it can never be a hub connection id.</summary>
    public const string ClientPrefix = "mcp-";

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
        try
        {
            return name switch
            {
                McpToolNames.ListProjects => await ListProjectsAsync(user, arguments, cancellationToken),
                McpToolNames.GetProject => await WithProjectAsync(arguments, project => GetProjectAsync(user, project, cancellationToken)),
                McpToolNames.GetHistory => await WithProjectAsync(arguments, project => GetHistoryAsync(user, project, cancellationToken)),
                McpToolNames.Undo => await WithProjectAsync(arguments, project => ReplyAsync(edits.UndoAsync(user, project, cancellationToken))),
                McpToolNames.Redo => await WithProjectAsync(arguments, project => ReplyAsync(edits.RedoAsync(user, project, cancellationToken))),
                _ => await ApplyAsync(user, name, arguments, cancellationToken),
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
        catch (ProjectFormatException)
        {
            return McpResults.Refused(CommandErrorCode.Invalid, "The project file is not valid.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return McpResults.Refused(CommandErrorCode.Rejected, "The project store is not available right now; call it again.");
        }
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

    private async Task<CallToolResult> GetProjectAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        ProjectSnapshot snapshot = await projects.OpenAsync(user, project, cancellationToken);
        string json = ProjectJson.Write(snapshot.Document);
        if (json.Length > options.Mcp.MaxDocumentChars)
        {
            return McpResults.Refused(CommandErrorCode.Limit, options.Mcp.DocumentTooLarge, snapshot.Revision);
        }
        using JsonDocument document = JsonDocument.Parse(json);
        ProjectDocumentResult result = new(snapshot.Revision, document.RootElement.Clone());
        return McpResults.Ok(result, McpJsonContext.Default.ProjectDocumentResult);
    }

    private async Task<CallToolResult> GetHistoryAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        HistoryState state = await edits.HistoryAsync(user, project, cancellationToken);
        return McpResults.Ok(state, McpJsonContext.Default.HistoryState);
    }

    // The tool's arguments are the command itself: its text goes to the registry as sent, and the project it names is where it is applied.
    private async Task<CallToolResult> ApplyAsync(
        UserId user,
        string name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        if (arguments is null
            || !arguments.TryGetValue("project", out JsonElement project)
            || project.ValueKind != JsonValueKind.String
            || !IdRules.IsValid(project.GetString()))
        {
            return McpResults.Refused(CommandErrorCode.Malformed, "project must be the id of one of your projects.");
        }
        string json = JsonSerializer.Serialize(arguments, McpJsonContext.Default.IDictionaryStringJsonElement);
        ClientId client = ClientId.From(ClientPrefix + Guid.NewGuid().ToString("N"));
        CommandReply reply = await edits.ApplyAsync(user, ProjectId.From(project.GetString()!), client, name, json, null, cancellationToken);
        return McpResults.Reply(reply);
    }

    private static async Task<CallToolResult> ReplyAsync(Task<CommandReply> reply) => McpResults.Reply(await reply);

    private static async Task<CallToolResult> WithProjectAsync(IDictionary<string, JsonElement>? arguments, Func<ProjectId, Task<CallToolResult>> run)
    {
        ProjectArguments? read = McpArguments.Read(arguments, McpJsonContext.Default.ProjectArguments, out CallToolResult? refusal);
        if (read is null)
        {
            return refusal!;
        }
        return await run(read.Project);
    }
}
