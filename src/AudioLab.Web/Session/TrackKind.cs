namespace AudioLab.Web.Session;

/// <summary>What a track holds, which decides how its clips are generated and drawn.</summary>
public enum TrackKind
{
    /// <inheritdoc/>
    Vocal,
    /// <inheritdoc/>
    Guitar,
    /// <inheritdoc/>
    Midi,
    /// <inheritdoc/>
    Bass,
    /// <inheritdoc/>
    Drums,
}
