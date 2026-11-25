namespace AudioLab.Server.Decoding;

/// <summary>An install stopped; the message is the reason the user reads. Nothing is left in place when it is thrown.</summary>
/// <param name="reason">The sentence the user reads.</param>
public sealed class DecoderInstallFailedException(string reason) : Exception(reason);
