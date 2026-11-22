namespace AudioLab.Server.Store;

/// <summary>The user has no such project or media record. A record that belongs to another user is reported the same way.</summary>
public sealed class ProjectNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ProjectNotFoundException()
        : base("There is no such project.")
    {
    }

    /// <summary>Creates the exception with a reason a client can be told, such as that the project is closing.</summary>
    public ProjectNotFoundException(string message)
        : base(message)
    {
    }
}
