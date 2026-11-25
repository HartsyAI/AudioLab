using System.Net;
using System.Net.Http.Headers;
using AudioLab.Core.Model;
using AudioLab.Web.Media;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the media routes (D37): list, read, upload, delete and peaks. A refusal is an <see cref="ApiException"/>.</summary>
/// <remarks>
/// A request that the client's own timeout ends is a <see cref="TimeoutException"/> (an upload, an <see cref="UploadInterruptedException"/>),
/// never an <see cref="OperationCanceledException"/>: that is only ever the caller's own cancellation.
/// </remarks>
/// <param name="http">The client of every route but the upload; it has the ordinary timeout.</param>
/// <param name="uploads">The client of the upload, which has no timeout: a large file takes as long as it takes, and only the user stops it.</param>
public sealed class MediaApi(HttpClient http, HttpClient uploads)
{
    /// <summary>The most one upload may carry whatever the server allows: the browser holds the whole body first, in 32-bit memory.</summary>
    public const long MemoryCeilingBytes = 512L * 1024 * 1024;

    /// <summary>The largest file the server accepts by default (<c>AudioLab:Media:MaxUploadBytes</c>), until it says what its own limit is.</summary>
    public const long DefaultMaxUploadBytes = MemoryCeilingBytes;

    private const string Media = "api/media";

    private const string WavType = MediaTypes.Wav;

    private const string TookTooLong = "The server took too long to answer. Try again.";

    private const string UnreadablePeaks = "The server sent peaks this page cannot read.";

    /// <summary>The most bytes a file may have to be sent; a larger one is refused unread, since the browser holds all of it first.</summary>
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;

    /// <summary>The user's media files, newest first, and how many stored records the server left out as invalid.</summary>
    public Task<MediaListing> ListAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(
            async () =>
            {
                using HttpResponseMessage response = await http.GetAsync(Media, cancellationToken);
                await ApiAnswers.EnsureAsync(response, cancellationToken);
                return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaListing, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>What the user may upload and how much of it they have used, to check a file before it is read.</summary>
    /// <remarks>The numbers are advice: another upload can finish first, and the upload route still judges the file on its bytes.</remarks>
    public Task<MediaLimits> LimitsAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(
            async () =>
            {
                using HttpResponseMessage response = await http.GetAsync($"{Media}/limits", cancellationToken);
                await ApiAnswers.EnsureAsync(response, cancellationToken);
                return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaLimits, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>One media file's summary.</summary>
    public Task<MediaSummary> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        string address = Address(id);
        return RunAsync(
            async () =>
            {
                using HttpResponseMessage response = await http.GetAsync(address, cancellationToken);
                await ApiAnswers.EnsureAsync(response, cancellationToken);
                return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaSummary, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>Uploads a WAV file and returns the summary the server measured; cancelling aborts it, and the server keeps nothing.</summary>
    /// <param name="name">The label the file is stored under.</param>
    /// <param name="content">The file's bytes; read once to the end, and disposed.</param>
    /// <param name="length">The size of the file, announced to the server so it can refuse an oversized file at once.</param>
    /// <param name="progress">Told how much of the file has been handed to the browser; the browser then still has to send it.</param>
    /// <param name="cancellationToken">Cancels the upload; it is the only thing that does.</param>
    /// <param name="contentType">The type the file is sent as: WAV, or a compressed type the server decodes.</param>
    /// <exception cref="ApiException">The file is larger than <see cref="MaxUploadBytes"/>, and is not read, or the server refused it.</exception>
    /// <exception cref="UploadInterruptedException">The upload ended with no answer: the connection closed, or the browser gave up.</exception>
    public async Task<MediaSummary> UploadAsync(
        string name,
        Stream content,
        long length,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string contentType = WavType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length > MaxUploadBytes)
        {
            await content.DisposeAsync();
            throw new ApiException(HttpStatusCode.RequestEntityTooLarge, UploadChecks.TooLarge(length, MaxUploadBytes));
        }
        using StreamContent body = new(new ProgressStream(content, length, progress));
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        body.Headers.ContentLength = length;
        try
        {
            using HttpResponseMessage response = await uploads.PostAsync($"{Media}?name={Uri.EscapeDataString(name)}", body, cancellationToken);
            await ApiAnswers.EnsureAsync(response, cancellationToken);
            return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaSummary, cancellationToken);
        }
        catch (HttpRequestException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UploadInterruptedException(UploadInterruptedException.NoAnswer, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UploadInterruptedException(UploadInterruptedException.GaveUp, exception);
        }
    }

    /// <summary>Deletes a file; the server refuses while a project of the user still plays it, and says which.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        string address = Address(id);
        return RunAsync(
            async () =>
            {
                using HttpResponseMessage response = await http.DeleteAsync(address, cancellationToken);
                await ApiAnswers.EnsureAsync(response, cancellationToken);
                return true;
            },
            cancellationToken);
    }

    /// <summary>The header and level table of a media file's peak file, which say how long each level is and where it lies.</summary>
    /// <remarks>A 503 is an exception whose <see cref="ApiException.RetryAfter"/> says when to ask again.</remarks>
    public async Task<PeakHeader> GetPeakHeaderAsync(string id, CancellationToken cancellationToken = default)
    {
        const int TableBytes = PeakFile.HeaderBytes + (PeakFile.LevelEntryBytes * PeakFile.MaxLevels);
        (byte[] bytes, HttpStatusCode status) = await RangeAsync(id, 0, TableBytes - 1, cancellationToken);
        try
        {
            return PeakFile.ReadHeader(bytes);
        }
        catch (InvalidDataException)
        {
            throw new ApiException(status, UnreadablePeaks);
        }
    }

    /// <summary>One level of a media file's peaks, as its own byte range, so a zoom that needs a coarse level never loads a fine one.</summary>
    public async Task<PeakLevel> GetPeakLevelAsync(string id, PeakHeader header, int level, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        (long offset, long length) = PeakFile.LevelRange(header, level);
        (byte[] bytes, HttpStatusCode status) = await RangeAsync(id, offset, offset + length - 1, cancellationToken);
        try
        {
            return PeakFile.ReadLevel(header, level, bytes);
        }
        catch (InvalidDataException)
        {
            throw new ApiException(status, UnreadablePeaks);
        }
    }

    // A server that ignores the range answers 200 with the whole file; the wanted bytes are then cut out of it.
    private Task<(byte[] Bytes, HttpStatusCode Status)> RangeAsync(string id, long from, long to, CancellationToken cancellationToken)
    {
        string address = $"{Address(id)}/peaks";
        return RunAsync(
            async () =>
            {
                using HttpRequestMessage request = new(HttpMethod.Get, address);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
                await ApiAnswers.EnsureAsync(response, cancellationToken);
                byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    return (body, response.StatusCode);
                }
                return (from >= body.Length ? [] : body[(int)from..(int)Math.Min(to + 1, body.Length)], response.StatusCode);
            },
            cancellationToken);
    }

    // The client's own timeout ends a request with a cancellation that the caller did not ask for; that is a failure to report.
    private static async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            return await work();
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(TookTooLong, exception);
        }
    }

    private static string Address(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return $"{Media}/{Uri.EscapeDataString(id)}";
    }
}
