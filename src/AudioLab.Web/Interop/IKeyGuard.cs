namespace AudioLab.Web.Interop;

/// <summary>Stops the browser's own action for the keys the arrangement handles, which Blazor cannot decide per key.</summary>
public interface IKeyGuard
{
    /// <summary>Prevents the default action of the arrow keys, Home and End pressed on an element of the given role inside the container.</summary>
    public void PreventNavigationKeys(string containerId, string targetRole);
}
