using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Session;

/// <summary>Reads an LRC file the user picked, and says why a file cannot be used as lyrics.</summary>
public static class LrcFile
{
    /// <summary>The most bytes a picked file may have: four bytes a character, the worst UTF-8 case for a clip's text.</summary>
    public const long MaxBytes = ProjectLimits.MaxLongText * 4L;

    /// <summary>The words for a file that is larger than a clip may hold.</summary>
    public const string TooLarge = "That file holds more text than a clip's lyrics may, so it was not read.";

    /// <summary>The words for a file that could not be read from the browser.</summary>
    public const string Unreadable = "That file could not be read.";

    private const string NotText = "That file is not a text file, so it was not read.";

    private const string NotUtf8 = "That file is not UTF-8 text, so it was not read.";

    private const string NoTimes = "That file has no timed lines, so it is not an LRC file.";

    private static readonly UTF8Encoding _strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads a picked file, checking its size before any of it is read, and says why it cannot be used.</summary>
    /// <param name="file">The file the user chose.</param>
    /// <returns>The text when it can be used, and otherwise the words that say why not.</returns>
    public static async Task<(string? Text, string? Refusal)> ReadPickedAsync(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Size > MaxBytes)
        {
            return (null, TooLarge);
        }
        byte[] bytes;
        try
        {
            using MemoryStream memory = new();
            await using (Stream stream = file.OpenReadStream(MaxBytes))
            {
                await stream.CopyToAsync(memory);
            }
            bytes = memory.ToArray();
        }
        catch (IOException)
        {
            return (null, Unreadable);
        }
        return Read(bytes);
    }

    /// <summary>Decodes the bytes of a picked file as UTF-8 LRC text, or says why it is refused.</summary>
    /// <returns>The text when it can be used, and otherwise the words that say why not.</returns>
    public static (string? Text, string? Refusal) Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            return (null, "That file is empty.");
        }
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return (null, NotText);
        }
        ReadOnlySpan<byte> body = bytes.AsSpan(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0);
        string text;
        try
        {
            text = _strict.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return (null, NotUtf8);
        }
        // The page's own check comes first: a file of no timed lines is still too long when it is.
        if (text.Length > ProjectLimits.MaxLongText)
        {
            return (null, TooLarge);
        }
        IReadOnlyList<LrcLine> lines = LyricsRules.ParseLrc(text, out bool tooLarge);
        if (tooLarge)
        {
            return (null, TooLarge);
        }
        return lines.Count == 0 ? (null, NoTimes) : (text, null);
    }
}
