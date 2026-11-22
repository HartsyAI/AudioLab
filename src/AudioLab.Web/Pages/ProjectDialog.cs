namespace AudioLab.Web.Pages;

/// <summary>Which dialog the project list has open.</summary>
internal enum ProjectDialog
{
    /// <summary>None.</summary>
    None,

    /// <summary>The new project's name.</summary>
    Create,

    /// <summary>The new name of a project.</summary>
    Rename,

    /// <summary>The confirmation of a deletion.</summary>
    Delete,
}
