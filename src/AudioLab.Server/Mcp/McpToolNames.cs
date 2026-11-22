namespace AudioLab.Server.Mcp;

/// <summary>The names of the tools that are not commands; a command's wire name has a domain before its verb, so these never meet one.</summary>
public static class McpToolNames
{
    /// <summary>Lists the user's projects.</summary>
    public const string ListProjects = "list_projects";

    /// <summary>Returns a project's document.</summary>
    public const string GetProject = "get_project";

    /// <summary>Returns what undo and redo hold in a project.</summary>
    public const string GetHistory = "get_history";

    /// <summary>Goes back one undo step.</summary>
    public const string Undo = "undo";

    /// <summary>Goes forward one redo step.</summary>
    public const string Redo = "redo";
}
