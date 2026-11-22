using System.Net.Http.Json;
using AudioLab.Core.Api;

namespace AudioLab.Web.Services;

/// <summary>Asks the server who it takes the caller to be, through the door the page was reached by.</summary>
public sealed class SessionClient(HttpClient http)
{
    /// <summary>The door and the user the server resolved for this page.</summary>
    /// <exception cref="HttpRequestException">The server did not answer, or refused the request.</exception>
    public async Task<SessionInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        SessionInfo? session = await http.GetFromJsonAsync("api/session", ApiJsonContext.Default.SessionInfo, cancellationToken);
        return session ?? throw new HttpRequestException("The server answered with nothing.");
    }
}
