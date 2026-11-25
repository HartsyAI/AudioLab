namespace AudioLab.Server.Decoding;

/// <summary>What the optional audio decoder may do, under <c>AudioLab:Decoder</c>.</summary>
public sealed record DecoderOptions
{
    /// <summary>Whether the install route may download the decoder. Off by default: only the desktop door turns it on, since the
    /// decoder is one per server and an install would serve every user of a shared instance.</summary>
    public bool InstallEnabled { get; init; }

    /// <summary>The longest one install may take, from its start to the decoder being in place; a longer one is failed and cleaned up.</summary>
    public TimeSpan InstallTimeout { get; init; } = TimeSpan.FromMinutes(30);

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
