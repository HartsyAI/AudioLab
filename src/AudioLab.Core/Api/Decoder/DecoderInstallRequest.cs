namespace AudioLab.Core.Api.Decoder;

/// <summary>The install request: the user has read this version and checksum on the confirmation, and the server installs only that build.</summary>
/// <param name="Version">The version the user confirmed.</param>
/// <param name="Sha256">The checksum the user confirmed.</param>
public sealed record DecoderInstallRequest(string Version, string Sha256);
