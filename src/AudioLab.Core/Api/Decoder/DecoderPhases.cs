namespace AudioLab.Core.Api.Decoder;

/// <summary>The phases of a decoder install, as the status reports them.</summary>
public static class DecoderPhases
{
    /// <summary>No install is running.</summary>
    public const string Idle = "idle";

    /// <summary>The archive is being downloaded.</summary>
    public const string Downloading = "downloading";

    /// <summary>The archive's checksum is being checked.</summary>
    public const string Verifying = "verifying";

    /// <summary>The one binary is being extracted and its checksum checked.</summary>
    public const string Extracting = "extracting";

    /// <summary>The last install finished and the decoder is in place.</summary>
    public const string Installed = "installed";

    /// <summary>The last install stopped; <see cref="DecoderStatus.Reason"/> says why.</summary>
    public const string Failed = "failed";
}
