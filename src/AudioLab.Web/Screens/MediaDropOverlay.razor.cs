using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>Covers the session while files are dragged over it, so the drop lands on a file chooser that hands the files to the media library.</summary>
/// <remarks>A drop on the chooser itself is the one way the browser gives a page the files of a drop without a script.</remarks>
public sealed partial class MediaDropOverlay
{
    /// <summary>Whether files are being dragged over the page, so the overlay is up.</summary>
    [Parameter]
    public bool Active { get; set; }

    /// <summary>Raised when the overlay should go away: the drag left the page, or the files were dropped.</summary>
    [Parameter]
    public EventCallback<bool> ActiveChanged { get; set; }

    private Task CloseAsync() => ActiveChanged.InvokeAsync(false);
}
