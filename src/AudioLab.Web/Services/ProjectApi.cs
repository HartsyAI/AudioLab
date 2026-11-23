using System.Net.Http.Json;
using AudioLab.Core.Api;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the project routes: list, create, delete. A refusal is an <see cref="ApiException"/>.</summary>
public sealed class ProjectApi(HttpClient http)
{
    private const string Projects = "api/projects";

    /// <summary>The user's projects, newest first, and how many stored files the server left out because they are not valid.</summary>
    public async Task<ProjectListing> ListAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync(Projects, cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, ApiJsonContext.Default.ProjectListing, cancellationToken);
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
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, ApiJsonContext.Default.ProjectSummary, cancellationToken);
    }

    /// <summary>Deletes the project for good, and closes it for everyone who has it open.</summary>
    public async Task DeleteAsync(string project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        using HttpResponseMessage response = await http.DeleteAsync($"{Projects}/{Uri.EscapeDataString(project)}", cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
    }
}
