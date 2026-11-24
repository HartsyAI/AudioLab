namespace AudioLab.Server.Media;

/// <summary>An upload is larger than the server accepts.</summary>
public sealed class MediaTooLargeException(long limit) : Exception($"The file is larger than the {limit} bytes the server accepts.")
{
    /// <summary>The most bytes an upload may have.</summary>
    public long Limit { get; } = limit;
}
