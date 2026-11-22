namespace AudioLab.Server.Realtime;

/// <summary>What the server calls on a client that has joined a project. The method names are the names on the wire.</summary>
public interface IProjectClient
{
    /// <summary>The project changed: the new revision and the whole document. A client applies a change only if its revision is above its own.</summary>
    public Task DocumentChanged(ProjectChange change);

    /// <summary>The project was deleted; the client is no longer a member of it and should drop what it holds.</summary>
    public Task ProjectDeleted(ProjectClosed closed);
}
