namespace AudioLab.Server.Store;

/// <summary>A change or save reached a session that was closed or discarded; the project must be opened again.</summary>
public sealed class SessionClosedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SessionClosedException()
        : base("The project was closed or deleted; open it again.")
    {
    }
}
