namespace AudioLab.Web.Services;

/// <summary>Makes SignalR connections to the hub at one address.</summary>
/// <param name="hub">The hub's address, under the site's base path.</param>
public sealed class SignalRProjectHubFactory(Uri hub) : IProjectHubFactory
{
    /// <inheritdoc />
    public IProjectHub Create(bool reconnects) => new SignalRProjectHub(hub, reconnects);
}
