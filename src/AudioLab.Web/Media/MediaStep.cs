namespace AudioLab.Web.Media;

/// <summary>Where the arrow keys, Home and End take the focus in the library list.</summary>
public enum MediaStep
{
    /// <summary>The file below.</summary>
    Next,

    /// <summary>The file above.</summary>
    Previous,

    /// <summary>The first file.</summary>
    First,

    /// <summary>The last file.</summary>
    Last,
}
