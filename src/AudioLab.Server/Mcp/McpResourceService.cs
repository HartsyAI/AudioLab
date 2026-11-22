using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>The user's projects as MCP resources: each one's document at <c>audiolab://projects/{id}</c>.</summary>
public sealed class McpResourceService(ProjectService projects, AudioLabOptions options)
{
    /// <summary>The media type of a document.</summary>
    public const string DocumentMimeType = "application/json";

    private const string UriPrefix = "audiolab://projects/";

    /// <summary>The uri of a project's document.</summary>
    public static string UriOf(ProjectId project) => UriPrefix + project.Value;

    /// <summary>The user's projects, newest first; at most as many as a user may have.</summary>
    public async Task<ListResourcesResult> ListAsync(UserId user, CancellationToken cancellationToken)
    {
        ProjectListing listing = await projects.ListAsync(user, cancellationToken);
        return new ListResourcesResult
        {
            Resources =
            [
                .. listing.Projects.Select(project => new Resource
                {
                    Uri = UriPrefix + project.Id,
                    Name = project.Name,
                    Description = "The project document, as the project file holds it.",
                    MimeType = DocumentMimeType,
                }),
            ],
        };
    }

    /// <summary>The one shape every project's uri has.</summary>
    public static ListResourceTemplatesResult Templates()
    {
        return new ListResourceTemplatesResult
        {
            ResourceTemplates =
            [
                new ResourceTemplate
                {
                    UriTemplate = UriPrefix + "{id}",
                    Name = "project",
                    Description = "The document of one of your projects.",
                    MimeType = DocumentMimeType,
                },
            ],
        };
    }

    /// <summary>Reads a project's document.</summary>
    /// <exception cref="McpProtocolException">The uri is not a project's, the user has no such project, or the document is too long.</exception>
    public async Task<ReadResourceResult> ReadAsync(UserId user, string uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.StartsWith(UriPrefix, StringComparison.Ordinal) || !IdRules.IsValid(uri[UriPrefix.Length..]))
        {
            throw new McpProtocolException("That is not the uri of a project.", McpErrorCode.InvalidParams);
        }
        ProjectSnapshot snapshot;
        try
        {
            snapshot = await projects.OpenAsync(user, ProjectId.From(uri[UriPrefix.Length..]), cancellationToken);
        }
        catch (ProjectNotFoundException exception)
        {
            throw new McpProtocolException(exception.Message, McpErrorCode.ResourceNotFound);
        }
        catch (Exception exception) when (exception is SessionClosedException or ProjectFormatException or IOException or UnauthorizedAccessException)
        {
            throw new McpProtocolException("The project could not be read right now.", McpErrorCode.InternalError);
        }
        string json = ProjectJson.Write(snapshot.Document);
        if (json.Length > options.Mcp.MaxDocumentChars)
        {
            throw new McpProtocolException(options.Mcp.DocumentTooLarge, McpErrorCode.InvalidRequest);
        }
        return new ReadResourceResult { Contents = [new TextResourceContents { Uri = uri, MimeType = DocumentMimeType, Text = json }] };
    }
}
