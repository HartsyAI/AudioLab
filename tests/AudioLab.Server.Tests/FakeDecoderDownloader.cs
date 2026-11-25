using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

/// <summary>Stands in for the network. It checks the source the way the real downloader does, so a test sees the same refusals:
/// a host that is not pinned, or a body larger than the pinned size. It can also stall until cancelled, or fail with an
/// exception nothing expects.</summary>
internal sealed class FakeDecoderDownloader : IDecoderDownloader
{
    public enum Mode
    {
        Serve,
        Stall,
        Throw,
    }

    public byte[] Payload { get; set; } = [];

    public Mode Behaviour { get; set; } = Mode.Serve;

    public int Calls { get; private set; }

    public async Task<long> DownloadAsync(
        Uri source,
        IReadOnlyList<string> hosts,
        Stream destination,
        long maxBytes,
        Action<long> progress,
        CancellationToken cancellationToken)
    {
        Calls++;
        if (source.Scheme != Uri.UriSchemeHttps || !hosts.Contains(source.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new DecoderInstallFailedException("The download source is not one this build may use.");
        }
        switch (Behaviour)
        {
            case Mode.Stall:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                break;
            case Mode.Throw:
                throw new InvalidOperationException("A failure nothing in the install expects.");
        }
        if (Payload.Length > maxBytes)
        {
            throw new DecoderInstallFailedException("The download is larger than the pinned size.");
        }
        await destination.WriteAsync(Payload, cancellationToken);
        progress(Payload.Length);
        return Payload.Length;
    }
}
