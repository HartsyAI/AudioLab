
namespace AudioLab.Server.Decoding;

/// <summary>Downloads the pinned archive over HTTPS, following redirects by hand so that every hop must stay on a pinned host.</summary>
public sealed class HttpDecoderDownloader : IDecoderDownloader, IDisposable
{
    private const int MaxRedirects = 5;
    private const int BufferBytes = 81920;

    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(30),
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// <summary>Creates the downloader with a generic client name, which is all the source is told.</summary>
    public HttpDecoderDownloader()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("AudioLab-Decoder-Install");
    }

    /// <inheritdoc />
    public async Task<long> DownloadAsync(
        Uri source,
        IReadOnlyList<string> hosts,
        Stream destination,
        long maxBytes,
        Action<long> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(hosts);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(progress);
        Uri current = source;
        for (int hop = 0; ; hop++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || !hosts.Contains(current.Host, StringComparer.OrdinalIgnoreCase))
            {
                throw new DecoderInstallFailedException("The download would leave the pinned source, so it was not started.");
            }
            using HttpResponseMessage response = await _client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            int status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                if (hop >= MaxRedirects || response.Headers.Location is null)
                {
                    throw new DecoderInstallFailedException("The download redirected too often, so it was stopped.");
                }
                current = new Uri(current, response.Headers.Location);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new DecoderInstallFailedException($"The download failed with HTTP status {status}.");
            }
            return await CopyAsync(response, destination, maxBytes, progress, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private static async Task<long> CopyAsync(
        HttpResponseMessage response,
        Stream destination,
        long maxBytes,
        Action<long> progress,
        CancellationToken cancellationToken)
    {
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[BufferBytes];
        long total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new DecoderInstallFailedException("The download is larger than the pinned size, so it was stopped.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress(total);
        }
        return total;
    }
}
