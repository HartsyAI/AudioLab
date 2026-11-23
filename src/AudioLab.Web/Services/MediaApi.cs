using System.Net.Http.Headers;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the media routes (D37): list, read, upload, delete and peaks. A refusal is an <see cref="ApiException"/>.</summary>
public sealed class MediaApi(HttpClient http)
{
    private const string Media = "api/media";

    private const string WavType = "audio/wav";

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

    /// <summary>The peak file of a media file, read with the Core reader. A 503 is an exception whose <see cref="ApiException.RetryAfter"/> says when to ask again.</summary>
    public async Task<PeakData> GetPeaksAsync(string id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync($"{Address(id)}/peaks", cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        try
        {
            return PeakFile.Read(bytes);
        }
        catch (InvalidDataException)
        {
            throw new ApiException(response.StatusCode, "The server sent peaks this page cannot read.");
        }
    }

    private static string Address(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return $"{Media}/{Uri.EscapeDataString(id)}";
    }
}
