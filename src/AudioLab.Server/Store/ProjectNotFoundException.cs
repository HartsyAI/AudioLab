namespace AudioLab.Server.Store;

/// <summary>The user has no such project or media record. A record that belongs to another user is reported the same way.</summary>
public sealed class ProjectNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ProjectNotFoundException()
        : base("There is no such project.")
    {
    }
}
