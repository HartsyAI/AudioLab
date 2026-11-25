namespace AudioLab.Server.Decoding;

/// <summary>Fetches the pinned decoder archive. The one network call the decoder ever makes; a fake stands in for it in tests.</summary>
public interface IDecoderDownloader
{
    /// <summary>Copies the archive to <paramref name="destination"/>, refusing any host not in <paramref name="hosts"/> and any body over <paramref name="maxBytes"/>.</summary>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="DecoderInstallFailedException">The source is refused, the server answers with an error, or the body is too large.</exception>
    public Task<long> DownloadAsync(
        Uri source,
        IReadOnlyList<string> hosts,
        Stream destination,
        long maxBytes,
        Action<long> progress,
        CancellationToken cancellationToken);
}
