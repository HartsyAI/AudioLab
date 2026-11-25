namespace AudioLab.Core.Api.Decoder;

/// <summary>The decoder build the server pins: what the install confirmation shows before any download.</summary>
/// <param name="Version">The exact ffmpeg version, such as 8.0.1.</param>
/// <param name="SourceUrl">Where the archive is downloaded from, a pinned release URL.</param>
/// <param name="Bytes">The archive's exact size.</param>
/// <param name="Sha256">The archive's SHA-256 in lower-case hexadecimal.</param>
public sealed record DecoderPackage(string Version, string SourceUrl, long Bytes, string Sha256);
