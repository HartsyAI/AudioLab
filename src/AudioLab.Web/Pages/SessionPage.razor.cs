using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Pages;

/// <summary>The route of one open project: it opens the project and shows the session screen.</summary>
public sealed partial class SessionPage
{
    private const string ReadOnly = "Editing arrives with the live document.";

    private ProjectDocument? _document;

    private long _revision;

    private string? _failure;

    private string? _loaded;

    /// <summary>The project's id, from the address.</summary>
    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Inject]
    private ProjectApi Api { get; set; } = default!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_loaded != Id)
        {
            _loaded = Id;
            _document = null;
            Selection.Clear();
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _failure = null;
        try
        {
            OpenedProject opened = await Api.OpenAsync(Id);
            _document = opened.Document;
            _revision = opened.Revision;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            _failure = exception is HttpRequestException ? "The server did not answer. Check that AudioLab is still running." : exception.Message;
        }
    }
}
