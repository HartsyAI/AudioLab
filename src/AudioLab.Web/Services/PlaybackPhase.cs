namespace AudioLab.Web.Services;

/// <summary>Whether the page's engine is running: it starts on the first Play, which a browser allows only from a user gesture.</summary>
public enum PlaybackPhase
{
    /// <summary>The engine has not been started.</summary>
    Idle,

    /// <summary>The audio context and the worker are starting.</summary>
    Starting,

    /// <summary>The engine is running.</summary>
    Ready,

    /// <summary>The engine could not start, or failed.</summary>
    Failed,
}
