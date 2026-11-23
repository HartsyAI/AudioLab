namespace AudioLab.Web.Media;

/// <summary>Whether the library's list has been read from the server.</summary>
public enum LibraryState
{
    /// <summary>Not asked for yet.</summary>
    NotLoaded,

    /// <summary>The list is on its way.</summary>
    Loading,

    /// <summary>The list is here.</summary>
    Ready,

    /// <summary>The list could not be read; <see cref="MediaLibrary.Failure"/> says why.</summary>
    Failed,
}
