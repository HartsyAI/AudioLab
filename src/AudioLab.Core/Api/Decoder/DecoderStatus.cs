namespace AudioLab.Core.Api.Decoder;

/// <summary>Whether the optional audio decoder is installed, and what the install is doing now.</summary>
/// <param name="Supported">Whether a decoder build is pinned for this server's platform.</param>
/// <param name="Installed">Whether a verified decoder is in place and can run.</param>
/// <param name="InstalledVersion">The version installed, or null when none is.</param>
/// <param name="Phase">One of <see cref="DecoderPhases"/>.</param>
/// <param name="Received">Bytes of the archive received so far while downloading.</param>
/// <param name="Total">The archive's size while downloading, the same as <see cref="DecoderPackage.Bytes"/>.</param>
/// <param name="Reason">Why the last install failed, or null.</param>
/// <param name="Package">The build an install would fetch, or null when the platform has none.</param>
/// <param name="InstallEnabled">Whether this server lets a user install the decoder at all; false on a shared instance.</param>
public sealed record DecoderStatus(
    bool Supported,
    bool Installed,
    string? InstalledVersion,
    string Phase,
    long Received,
    long Total,
    string? Reason,
    DecoderPackage? Package,
    bool InstallEnabled);
