using AudioLab.Audio;
using Microsoft.Net.Http.Headers;

namespace AudioLab.Server.Routes;

/// <summary>Makes the headers of a file the server sends as a download, and a file name that is safe in them.</summary>
public static class AttachmentResponse
{
    private const int MaxNameLength = 100;

    /// <summary>A file name made of text a user typed: cleaned, never empty, no path and no device name, with <paramref name="extension"/>.</summary>
    /// <param name="text">The user's text, such as a project's or a clip's name.</param>
    /// <param name="fallback">The name to use when nothing of the text is left.</param>
    /// <param name="extension">The extension, with its dot.</param>
    public static string FileName(string text, string fallback, string extension)
    {
        string name = ExportFileName.Clean(text, MaxNameLength);
        string safe = name.Length == 0 ? fallback : DeviceNames.IsDeviceName(name) ? "_" + name : name;
        return safe + extension;
    }

    /// <summary>Sets the content type, the length and a <c>Content-Disposition: attachment</c> with an ASCII and an encoded name.</summary>
    public static void Apply(HttpResponse response, string contentType, string fileName, long length)
    {
        ArgumentNullException.ThrowIfNull(response);
        ContentDispositionHeaderValue disposition = new("attachment") { FileNameStar = fileName };
        disposition.SetHttpFileName(new string([.. fileName.Select(character => character is >= ' ' and <= '~' ? character : '_')]));
        response.ContentType = contentType;
        response.ContentLength = length;
        response.Headers.ContentDisposition = disposition.ToString();
        response.Headers.CacheControl = "no-store";
        response.Headers.XContentTypeOptions = "nosniff";
    }
}
