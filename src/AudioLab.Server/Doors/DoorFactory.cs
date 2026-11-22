using AudioLab.Server.Configuration;

namespace AudioLab.Server.Doors;

/// <summary>Builds the door a configuration names.</summary>
public static class DoorFactory
{
    /// <summary>The door for <paramref name="options"/>.</summary>
    public static IDoor Create(DoorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Kind switch
        {
            DoorKind.Desktop => new DesktopDoor(),
            DoorKind.Swarm => new ProxyDoor("swarm", options),
            DoorKind.Hosted => new ProxyDoor("hosted", options),
            _ => throw new ArgumentException("Unknown door.", nameof(options)),
        };
    }
}
