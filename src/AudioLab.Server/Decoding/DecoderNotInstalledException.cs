namespace AudioLab.Server.Decoding;

/// <summary>The compressed upload needs the decoder and it is not installed, or the installed one does not match its checksum.</summary>
/// <param name="reason">The sentence the user reads, which says how to fix it.</param>
public sealed class DecoderNotInstalledException(string reason) : Exception(reason);
