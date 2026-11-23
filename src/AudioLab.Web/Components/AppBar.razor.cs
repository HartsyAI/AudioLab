using AudioLab.Core.Api;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The bar of every screen but the session: the mark, the name and the door the server resolved for this page.</summary>
public sealed partial class AppBar
{
    [Inject]
    private SessionClient Sessions { get; set; } = default!;

    private string Who { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            SessionInfo session = await Sessions.GetAsync();
            Who = $"{session.Door} door";
        }
        catch (HttpRequestException)
        {
            Who = "Server not reachable";
        }
    }
}
