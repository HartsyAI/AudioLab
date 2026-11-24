namespace AudioLab.Web.Services;

/// <summary>Whether the engine in the page keeps up with the audio device, the signal that a server mix would have to take over (D24).</summary>
public enum PlaybackHealth
{
    /// <summary>Every chunk was ready in time.</summary>
    Steady,

    /// <summary>A chunk took most of the time it has, so a busier moment would drop out.</summary>
    Strained,

    /// <summary>The audio thread ran out of audio and played silence in its place.</summary>
    Dropping,

    /// <summary>The audio context runs faster or slower than the clock, so playback is at the wrong speed; no dropout shows it.</summary>
    Drifting,
}
