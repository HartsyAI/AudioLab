using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AudioLab.Core;
using AudioLab.Core.Api;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the project routes: list, create, delete. A refusal is an <see cref="ApiException"/>.</summary>
public sealed class ProjectApi(HttpClient http)
{
    private const string Projects = "api/projects";

    /// <summary>The user's projects, newest first, and how many stored files the server left out because they are not valid.</summary>
    public async Task<ProjectListing> ListAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync(Projects, cancellationToken);
        await EnsureAsync(response, cancellationToken);
        return await ReadAsync(response, ApiJsonContext.Default.ProjectListing, cancellationToken);
    }

    /// <summary>Opens the project and returns its whole document and revision.</summary>
    public async Task<OpenedProject> OpenAsync(string project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        using HttpResponseMessage response = await http.GetAsync($"{Projects}/{Uri.EscapeDataString(project)}", cancellationToken);
        await EnsureAsync(response, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        bool hasRevision = response.Headers.TryGetValues(AudioLabApi.RevisionHeader, out IEnumerable<string>? values);
        if (!hasRevision || !long.TryParse(values!.Single(), NumberStyles.None, CultureInfo.InvariantCulture, out long revision))
        {
            throw new ApiException(response.StatusCode, "The server did not say which revision of the project this is.");
        }
        try
        {
            return new OpenedProject(ProjectJson.Read(text), revision);
        }
        catch (ProjectFormatException exception)
        {
            throw new ApiException(response.StatusCode, $"This version of AudioLab cannot read the project the server sent. {exception.Message}");
        }
    }

    /// <summary>Creates an empty project with the given name.</summary>
    public async Task<ProjectSummary> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using HttpResponseMessage response = await http.PostAsJsonAsync(
            Projects,
            new CreateProjectRequest(name),
            ApiJsonContext.Default.CreateProjectRequest,
            cancellationToken);
        await EnsureAsync(response, cancellationToken);
        return await ReadAsync(response, ApiJsonContext.Default.ProjectSummary, cancellationToken);
    }

    /// <summary>Deletes the project for good, and closes it for everyone who has it open.</summary>
    public async Task DeleteAsync(string project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        using HttpResponseMessage response = await http.DeleteAsync($"{Projects}/{Uri.EscapeDataString(project)}", cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        CancellationToken cancellationToken)
    {
        T? value = await response.Content.ReadFromJsonAsync(type, cancellationToken);
        return value ?? throw new ApiException(response.StatusCode, "The server answered with nothing.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        throw new ApiException(response.StatusCode, await DescribeAsync(response, cancellationToken));
    }

    private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ApiProblem? problem = await response.Content.ReadFromJsonAsync(WebJsonContext.Default.ApiProblem, cancellationToken);
            if (problem is not null)
            {
                return problem.Message;
            }
        }
        catch (JsonException)
        {
            // A proxy or a gateway may answer with something that is not problem details; the status says enough.
        }
        return response.StatusCode == HttpStatusCode.Forbidden
            ? "The server did not accept this page's request."
            : $"The server refused the request ({(int)response.StatusCode}).";
    }
}
