using System.Net;
using AudioLab.Audio;
using AudioLab.Audio.Live;
using AudioLab.Core.Model;

namespace AudioLab.Web.Interop.Worker;

/// <summary>Fetches clips from the media routes (D37), <c>api/media/{id}/content</c>, with <see cref="HttpClient"/> and decodes them.</summary>
/// <remarks>
/// The client's base address is the page's, path base included, so the request is the page's own and carries its sign-in. A refusal is said plainly
/// (not found, not signed in, too large); a failure that may pass (the network, a timeout, a server that is busy) is tried again after a pause.
/// The file is held whole while it decodes, so one beyond <see cref="MaxClipBytes"/> is refused before it is read.
/// </remarks>
/// <param name="http">The client, whose base address is the page's.</param>
/// <param name="pauses">The pauses before each new try, which also say how many there are; a test gives none.</param>
public sealed class HttpClipSource(HttpClient http, IReadOnlyList<TimeSpan>? pauses = null) : IClipSource
{
    /// <summary>The largest file the page takes: decoding holds the bytes, the samples and the resampled copy at once.</summary>
    public const long MaxClipBytes = 128L * 1024 * 1024;

    private static readonly TimeSpan[] _defaultPauses = [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1)];

    private readonly IReadOnlyList<TimeSpan> _pauses = pauses ?? _defaultPauses;

    /// <inheritdoc />
    public async Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await TryLoadAsync(id, pause, cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken) && attempt < _pauses.Count)
            {
                await Task.Delay(_pauses[attempt], cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken))
            {
                throw new ClipUnavailableException($"could not be fetched ({Describe(exception)}) after {_pauses.Count + 1} tries");
            }
        }
    }

    private async Task<PcmAudio> TryLoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(
            $"api/media/{Uri.EscapeDataString(id.Value)}/content", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new ClipUnavailableException("was not found: it may have been deleted, or it belongs to another user");
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new ClipUnavailableException("cannot be read because this page is not signed in");
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length > MaxClipBytes)
        {
            throw new ClipUnavailableException($"is {length / (1024 * 1024)} MB, over the {MaxClipBytes / (1024 * 1024)} MB a page can hold");
        }
        byte[] file = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return await ClipDecoder.DecodeAsync(file, pause, cancellationToken);
    }

    // A request that failed on the way or timed out may work again; one the caller cancelled, or that the server refused for good, may not.
    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException { StatusCode: null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests } => true,
        HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => true,
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };

    private static string Describe(Exception exception) =>
        exception switch
        {
            HttpRequestException { StatusCode: { } status } => $"the server answered {(int)status}",
            TaskCanceledException => "it timed out",
            _ => exception.Message,
        };
}
