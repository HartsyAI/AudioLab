namespace AudioLab.Web.Services;

/// <summary>Makes connections to the project hub, one for each user of it, so a test can hand out a fake.</summary>
public interface IProjectHubFactory
{
    /// <summary>A new connection that is not yet open.</summary>
    /// <param name="reconnects">True for a connection a screen keeps open, which opens again by itself; false for a one-shot one.</param>
    public IProjectHub Create(bool reconnects);
}
