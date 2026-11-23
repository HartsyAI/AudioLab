using AudioLab.Core.Model;

namespace AudioLab.Audio.Live;

/// <summary>Fetches clips from the server's media routes with <see cref="HttpClient"/> (D37): <c>api/media/{id}/content</c>, then decodes them.</summary>
/// <remarks>The client's base address is the page's, path base included. The file is held whole while it decodes.</remarks>
public sealed class HttpClipSource(HttpClient http) : IClipSource
{
    /// <inheritdoc />
    public async Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        byte[] file = await http.GetByteArrayAsync($"api/media/{Uri.EscapeDataString(id.Value)}/content", cancellationToken);
        return await WavDecoder.DecodeAsync(file, pause, cancellationToken);
    }
}
