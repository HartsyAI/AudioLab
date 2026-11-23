using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Layout;

/// <summary>The frame of every screen. A screen that fails shows a message and can be tried again, or left by any link.</summary>
public sealed partial class MainLayout : IDisposable
{
    private ErrorBoundary? _boundary;

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
