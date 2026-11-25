using AudioLab.Core.Playback;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>A transport the test moves by hand: each <see cref="Tick"/> is one status of the engine, with no audio.</summary>
internal sealed class FakePlaybackTransport : IPlaybackTransport
{
    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public TransportState Transport { get; private set; } = TransportState.Stopped;

    /// <inheritdoc />
    public double Position { get; private set; }

    /// <summary>Sets the state and the playhead, and raises the change as the engine's status does.</summary>
    public void Tick(TransportState state, double position)
    {
        Transport = state;
        Position = position;
        Changed?.Invoke();
    }
}
