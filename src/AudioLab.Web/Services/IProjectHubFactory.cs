namespace AudioLab.Web.Services;

/// <summary>Makes connections to the project hub, one for each user of it, so a test can hand out a fake.</summary>
public interface IProjectHubFactory
{
    /// <summary>A new connection that is not yet open.</summary>
    public IProjectHub Create();
}
