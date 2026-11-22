namespace AudioLab.Web.Interop;

/// <summary>Stops the browser's own action for the keys the arrangement handles, which Blazor cannot decide per key.</summary>
public interface IKeyGuard
{
    /// <summary>Prevents the default action of the arrow keys, Home and End when they are pressed on an element with the given role inside the element with this id.</summary>
    public void PreventNavigationKeys(string containerId, string targetRole);
}
