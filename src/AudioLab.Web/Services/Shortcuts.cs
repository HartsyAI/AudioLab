namespace AudioLab.Web.Services;

/// <summary>The one registry of shortcuts. A shortcut is never written into a component: menus, tooltips and key handlers read it here.</summary>
public static class Shortcuts
{
    /// <summary>Rename the project that has the focus in the list.</summary>
    public static readonly Shortcut RenameProject = new("project.rename", "Rename project", "F2");

    /// <summary>Delete the project that has the focus in the list.</summary>
    public static readonly Shortcut DeleteProject = new("project.delete", "Delete project", "Delete");

    /// <summary>Every shortcut, in the order they are listed.</summary>
    public static readonly IReadOnlyList<Shortcut> All = [RenameProject, DeleteProject];
}
