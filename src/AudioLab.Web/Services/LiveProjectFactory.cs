namespace AudioLab.Web.Services;

/// <summary>Makes a <see cref="LiveProject"/> for each project the page opens, so moving to another project starts a new connection.</summary>
public sealed class LiveProjectFactory(IProjectHubFactory hubs, CommandSender sender)
{
    /// <summary>A live project that is not yet open.</summary>
    public LiveProject Create() => new(hubs, sender);
}
