namespace AudioLab.Web.Media;

/// <summary>The content types the media upload is sent as, and the file types the picker offers for them.</summary>
public static class MediaTypes
{
    /// <summary>The content type of a WAV file.</summary>
    public const string Wav = "audio/wav";

    private const string WavAccept = ".wav,audio/wav,audio/x-wav";

    private const string CompressedAccept =
        ".mp3,.m4a,.flac,.ogg,audio/mpeg,audio/mp3,audio/mp4,audio/x-m4a,audio/flac,audio/ogg";

    /// <summary>The value of a file chooser's accept attribute: WAV always, and the compressed types only while the decoder is installed.</summary>
    public static string Accept(bool decoderInstalled) => decoderInstalled ? $"{WavAccept},{CompressedAccept}" : WavAccept;

    /// <summary>Whether a content type is WAV, which the page checks itself before the upload.</summary>
    public static bool IsWav(string contentType) =>
        contentType.Equals(Wav, StringComparison.OrdinalIgnoreCase) || contentType.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase);

    /// <summary>The content type of a chosen file, from its extension so that the server's rule for it is the one the name
    /// shows. A file with an extension the page does not know is sent as the browser reports it, so the server refuses
    /// it with its own reason.</summary>
    public static string FromName(string name, string browserType)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".wav" => Wav,
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            _ => string.IsNullOrWhiteSpace(browserType) ? "application/octet-stream" : browserType,
        };
    }
}
