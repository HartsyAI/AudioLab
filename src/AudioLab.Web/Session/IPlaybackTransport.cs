using AudioLab.Core.Playback;

namespace AudioLab.Web.Session;

/// <summary>The transport the automation recorder reads: its state, its playhead, and a tick each time either changes.</summary>
public interface IPlaybackTransport
{
    /// <summary>Raised on each status of the engine, about every 40 ms while it runs, and when the playhead is moved.</summary>
    public event Action? Changed;

    /// <summary>Whether the transport is playing, paused or stopped.</summary>
    public TransportState Transport { get; }

    /// <summary>The playhead in seconds.</summary>
    public double Position { get; }

    /// <summary>Whether the audio engine is running; false once it has failed or been let go, which ends a pass without a commit (D86).</summary>
    public bool EngineRunning { get; }
}
