namespace AudioLab.Server.Routes;

/// <summary>The names Windows reserves for devices, which a file of that name cannot be made as, with or without an extension.</summary>
public static class DeviceNames
{
    private static readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
    };

    // COM and LPT with one digit, 0 to 9 and the superscript digits that the file system treats as the same names.
    private const string PortDigits = "0123456789\u00B9\u00B2\u00B3";

    /// <summary>Whether the part of <paramref name="name"/> before its first dot is a device name.</summary>
    public static bool IsDeviceName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = (dot < 0 ? name : name[..dot]).TrimEnd();
        return _reserved.Contains(stem)
            || (stem.Length == 4
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && PortDigits.Contains(stem[3], StringComparison.Ordinal));
    }
}
