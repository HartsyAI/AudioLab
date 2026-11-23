using System.Net;
using System.Net.Http.Headers;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the media routes (D37): list, read, upload, delete and peaks. A refusal is an <see cref="ApiException"/>.</summary>
public sealed class MediaApi(HttpClient http)
{
    private const string Media = "api/media";

    private const string WavType = "audio/wav";

    private const string UnreadablePeaks = "The server sent peaks this page cannot read.";

    /// <summary>The user's media files, newest first, and how many stored records the server left out as invalid.</summary>
    public async Task<MediaListing> ListAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync(Media, cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaListing, cancellationToken);
    }

    /// <summary>One media file's summary.</summary>
    public async Task<MediaSummary> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync(Address(id), cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaSummary, cancellationToken);
    }

    /// <summary>Uploads a WAV file and returns the summary the server measured from its bytes; cancelling aborts the request, and the server keeps nothing.</summary>
    /// <param name="name">The label the file is stored under.</param>
    /// <param name="content">The file's bytes; read once to the end, and disposed.</param>
    /// <param name="length">The size of the file, announced to the server so it can refuse an oversized file at once.</param>
    /// <param name="progress">Told how much of the file has been handed to the browser; the browser then still has to send it.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <exception cref="UploadInterruptedException">The connection closed before any answer, which the server also does for a refusal it sends early.</exception>
    public async Task<MediaSummary> UploadAsync(
        string name,
        Stream content,
        long length,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        using StreamContent body = new(new ProgressStream(content, length, progress));
        body.Headers.ContentType = new MediaTypeHeaderValue(WavType);
        body.Headers.ContentLength = length;
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync($"{Media}?name={Uri.EscapeDataString(name)}", body, cancellationToken);
        }
        catch (HttpRequestException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UploadInterruptedException(
                "The upload stopped before the server answered. The server may have refused the file because it is too large or your storage is full.",
                exception);
        }
        using (response)
        {
            await ApiAnswers.EnsureAsync(response, cancellationToken);
            return await ApiAnswers.ReadAsync(response, MediaJsonContext.Default.MediaSummary, cancellationToken);
        }
    }

    /// <summary>Deletes a file; the server refuses while a project of the user still plays it, and says which.</summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.DeleteAsync(Address(id), cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
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

    /// <summary>One level of a media file's peaks, asked for as its own byte range so a zoom that needs a coarse level never loads a fine one.</summary>
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
    private async Task<(byte[] Bytes, HttpStatusCode Status)> RangeAsync(string id, long from, long to, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"{Address(id)}/peaks");
        request.Headers.Range = new RangeHeaderValue(from, to);
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            return (body, response.StatusCode);
        }
        return (from >= body.Length ? [] : body[(int)from..(int)Math.Min(to + 1, body.Length)], response.StatusCode);
    }

    private static string Address(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return $"{Media}/{Uri.EscapeDataString(id)}";
    }
}
