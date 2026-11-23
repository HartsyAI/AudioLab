using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The session screen: top bar, arrangement, bottom panel and status bar, drawn from one document.</summary>
public sealed partial class SessionScreen : IDisposable
{
    /// <summary>The document to draw.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The revision of <see cref="Document"/>.</summary>
    [Parameter]
    public long Revision { get; set; }

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    /// <summary>The colour of the connection state dot.</summary>
    [Parameter]
    public StatusDot Dot { get; set; } = StatusDot.Caution;

    /// <summary>The connection state in words.</summary>
    [Parameter]
    public string State { get; set; } = string.Empty;

    private bool _dropping;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    /// <inheritdoc />
    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
    }

    // Only a drag that carries files opens the drop target; text and links dragged across the page leave it alone.
    private void DragEnter(DragEventArgs drag)
    {
        _dropping = drag.DataTransfer.Types.Contains("Files");
    }

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);
}
