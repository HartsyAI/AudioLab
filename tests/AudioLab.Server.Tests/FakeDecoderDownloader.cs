using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

/// <summary>Stands in for the network: writes a payload the test chose, and counts the calls so a test can see whether any download happened.</summary>
internal sealed class FakeDecoderDownloader : IDecoderDownloader
{
    public byte[] Payload { get; set; } = [];

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
        await destination.WriteAsync(Payload, cancellationToken);
        progress(Payload.Length);
        return Payload.Length;
    }
}
