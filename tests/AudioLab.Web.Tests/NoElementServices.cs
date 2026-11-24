using AudioLab.Web.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The browser services the effects panel asks for, answered with nothing, for a render that only draws.</summary>
internal static class NoElementServices
{
    /// <summary>Registers a key guard, an element view and an element focus that do nothing.</summary>
    public static void Add(IServiceCollection services)
    {
        services.AddSingleton<IKeyGuard, NoGuard>();
        services.AddSingleton<IElementView, NoView>();
        services.AddSingleton<IElementFocus, NoFocus>();
    }

    private sealed class NoGuard : IKeyGuard
    {
        public void PreventNavigationKeys(string containerId, string targetRole)
        {
        }
    }

    private sealed class NoView : IElementView
    {
        public (double Left, double Top, double Width, double Height)? Bounds(string elementId) => null;

        public (double Left, double Top)? Scrolled(string elementId) => null;

        public void ScrollTo(string elementId, double left, double top)
        {
        }

        public bool HasFocusWithin(string elementId) => false;
    }

    private sealed class NoFocus : IElementFocus
    {
        public void Focus(string elementId)
        {
        }
    }
}
