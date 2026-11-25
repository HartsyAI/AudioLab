using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>A key guard that does nothing: a test has no browser to stop the keys of.</summary>
internal sealed class NoKeyGuard : IKeyGuard
{
    /// <inheritdoc />
    public void PreventNavigationKeys(string containerId, string targetRole)
    {
    }
}
