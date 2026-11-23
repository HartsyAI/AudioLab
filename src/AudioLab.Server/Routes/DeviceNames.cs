namespace AudioLab.Server.Routes;

/// <summary>The names Windows reserves for devices, which a file of that name cannot be made as, with or without an extension.</summary>
public static class DeviceNames
{
    private static readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Whether the part of <paramref name="name"/> before its first dot is a device name.</summary>
    public static bool IsDeviceName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        return _reserved.Contains((dot < 0 ? name : name[..dot]).TrimEnd());
    }
}
