using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Layout;

/// <summary>The frame of every screen. A screen that fails shows a message and can be tried again, or left by any link.</summary>
public sealed partial class MainLayout : IDisposable
{
    private ErrorBoundary? _boundary;

    private bool _dropping;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <inheritdoc />
    public void Dispose()
    {
        Navigation.LocationChanged -= Left;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += Left;
    }

    // Only a drag that carries files opens the drop target; text and links dragged across the page leave it alone.
    private void DragEnter(DragEventArgs drag)
    {
        _dropping = drag.DataTransfer.Types.Contains("Files");
    }

    // The handlers exist only while the overlay is up, so a key press or a pointer press elsewhere costs the page nothing.
    private EventCallback<KeyboardEventArgs> CloseOnKey => _dropping ? EventCallback.Factory.Create<KeyboardEventArgs>(this, CloseDrop) : default;

    private EventCallback<PointerEventArgs> CloseOnPress => _dropping ? EventCallback.Factory.Create<PointerEventArgs>(this, CloseDrop) : default;

    // A drop that the chooser raises no change for (a folder in Chrome) must not leave the overlay over the page, so a key or a press ends it.
    private void CloseDrop() => _dropping = false;

    private void Recover()
    {
        _boundary?.Recover();
    }

    // The layout outlives the screens, so a boundary that failed would stay failed after the user went somewhere else.
    private void Left(object? sender, LocationChangedEventArgs change)
    {
        _ = InvokeAsync(Recover);
    }
}
