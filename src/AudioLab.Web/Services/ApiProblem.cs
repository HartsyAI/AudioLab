using System.Text.Json.Serialization;

namespace AudioLab.Web.Services;

/// <summary>The body the server answers a refused route with, as problem details: a title, and for an invalid file the problems found.</summary>
/// <param name="Title">What went wrong, in one sentence.</param>
/// <param name="Problems">What is wrong with a stored file, when it is not valid.</param>
public sealed record ApiProblem(string? Title = null, string[]? Problems = null)
{
    /// <summary>The problem as one sentence for a person: the title, then the first problem when the server listed any.</summary>
    [JsonIgnore]
    public string Message
    {
        get
        {
            string title = string.IsNullOrWhiteSpace(Title) ? "The server refused the request." : Title;
            return Problems is { Length: > 0 } ? $"{title} {Problems[0]}" : title;
        }
    }
}
