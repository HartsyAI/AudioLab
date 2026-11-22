using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

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

    /// <summary>Whether the document may be edited from here.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Why it may not, shown by the controls that would edit it.</summary>
    [Parameter]
    public string? ReadOnlyReason { get; set; }

    /// <summary>The colour of the connection state dot.</summary>
    [Parameter]
    public StatusDot Dot { get; set; } = StatusDot.Caution;

    /// <summary>The connection state in words.</summary>
    [Parameter]
    public string State { get; set; } = string.Empty;

    /// <summary>Raised when the user asks for a new track.</summary>
    [Parameter]
    public EventCallback OnAddTrack { get; set; }

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

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);
}
