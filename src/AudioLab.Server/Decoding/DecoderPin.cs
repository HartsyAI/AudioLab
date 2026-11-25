using AudioLab.Core.Api.Decoder;

namespace AudioLab.Server.Decoding;

/// <summary>
/// The one decoder build this server pins: gyan.dev FFmpeg 9.0.2 essentials for Windows x64. The archive's SHA-256 is the vendor's own
/// published value. <see cref="BinarySha256"/> is the checksum of <c>bin/ffmpeg.exe</c> read from that archive in the owner-approved live check;
/// an empty value would mean no platform has a pinned build, and the install route would answer that none is available.
/// </summary>
public static class DecoderPin
{
    private const string Version = "9.0.2";
    private const string SourceUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip";
    private const long ArchiveBytes = 114768076;
    private const string ArchiveSha256 = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";
    private const string EntryName = "ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe";
    private const string BinaryName = "ffmpeg.exe";
    private const string BinarySha256 = "3256173f3f8bffd7df12227c68adf68025edb1832273a9530688a7bb1ed8edec";

    /// <summary>The build for this platform, or null when none is pinned for it.</summary>
    public static DecoderBuild? ForThisPlatform()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem || BinarySha256.Length == 0)
        {
            return null;
        }
        DecoderPackage package = new(Version, SourceUrl, ArchiveBytes, ArchiveSha256);
        return new DecoderBuild(package, ["www.gyan.dev", "gyan.dev"], EntryName, BinaryName, BinarySha256);
    }
}
