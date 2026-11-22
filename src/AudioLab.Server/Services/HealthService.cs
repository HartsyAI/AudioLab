using AudioLab.Core;
using AudioLab.Server.Doors;

namespace AudioLab.Server.Services;

/// <summary>Reports that the server is up and which contract and door it serves.</summary>
public sealed class HealthService(IDoor door)
{
    /// <summary>The current report.</summary>
    public HealthReport Report() => new("ok", AudioLabApi.Version, door.Name);
}
