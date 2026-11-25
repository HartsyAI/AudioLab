using AudioLab.Core.Api.Decoder;

namespace AudioLab.Server.Decoding;

/// <summary>One pinned decoder build: the archive the user confirms, and the one binary taken out of it and checked before it runs.</summary>
/// <param name="Package">The archive: version, source, size and checksum, as the confirmation shows them.</param>
/// <param name="Hosts">The hosts the download may come from, redirects included; any other host is refused.</param>
/// <param name="EntryName">The path of the one entry extracted from the archive, exactly as the archive names it.</param>
/// <param name="BinaryName">The file name the binary is installed under, in the version's folder.</param>
/// <param name="BinarySha256">The binary's SHA-256 in lower-case hexadecimal; a binary that does not match is never run.</param>
public sealed record DecoderBuild(DecoderPackage Package, IReadOnlyList<string> Hosts, string EntryName, string BinaryName, string BinarySha256);
