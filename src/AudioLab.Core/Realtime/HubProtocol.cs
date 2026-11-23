namespace AudioLab.Core.Realtime;

/// <summary>The path and the method names of the project hub, the one place the server and every client read them from.</summary>
public static class HubProtocol
{
    /// <summary>Where the hub is mapped, under the site's base path.</summary>
    public const string Path = "/api/hub";

    /// <summary>Client to server: join a project; answers a <see cref="JoinReply"/>.</summary>
    public const string Join = "Join";

    /// <summary>Client to server: leave a project.</summary>
    public const string Leave = "Leave";

    /// <summary>Client to server: apply a command by its wire name; answers a <see cref="CommandReply"/>.</summary>
    public const string Apply = "Apply";

    /// <summary>Client to server: undo one step; answers a <see cref="CommandReply"/>.</summary>
    public const string Undo = "Undo";

    /// <summary>Client to server: redo one step; answers a <see cref="CommandReply"/>.</summary>
    public const string Redo = "Redo";

    /// <summary>Client to server: close this client's open gesture.</summary>
    public const string EndGesture = "EndGesture";

    /// <summary>Server to client: a new revision, as a <see cref="ProjectChange"/>.</summary>
    public const string DocumentChanged = "DocumentChanged";

    /// <summary>Server to client: the project was deleted, as a <see cref="ProjectClosed"/>.</summary>
    public const string ProjectDeleted = "ProjectDeleted";
}
