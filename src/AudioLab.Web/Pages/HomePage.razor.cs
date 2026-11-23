using AudioLab.Core.Api;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Pages;

/// <summary>The first screen: the shell, with the door and the user the server resolved for this page.</summary>
public sealed partial class HomePage
{
    [Inject]
    private SessionClient Sessions { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    private string Who { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        return CheckAsync();
    }

    private async Task CheckAsync()
    {
        try
        {
            SessionInfo session = await Sessions.GetAsync();
            Who = $"{session.Door} door";
        }
        catch (HttpRequestException exception)
        {
            Who = string.Empty;
            Toasts.Show(ToastKind.Error, $"The server did not answer: {exception.Message}");
        }
    }
}
