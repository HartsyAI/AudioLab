namespace AudioLab.Server.Decoding;

/// <summary>What the optional audio decoder may do, under <c>AudioLab:Decoder</c>.</summary>
public sealed record DecoderOptions
{
    /// <summary>Whether the install route may download the decoder. A hosted instance sets it false, so no user can fetch a binary for all.</summary>
    public bool InstallEnabled { get; init; } = true;

    /// <summary>The largest compressed file one upload may carry; the decoded WAV is still held to <c>AudioLab:Media:MaxUploadBytes</c>.</summary>
    public long MaxInputBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>How long one decode may run before its process is killed and the upload refused.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>How much of the decoder's error output is kept for the log; the rest is read and dropped.</summary>
    public int StderrCapBytes { get; init; } = 64 * 1024;

    /// <summary>The most decodes one user may run at once.</summary>
    public int MaxConcurrentPerUser { get; init; } = 1;

    /// <summary>The most decodes the server runs at once, for every user.</summary>
    public int MaxConcurrent { get; init; } = 2;
}
