using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Reads an LRC file the user picked, and says why a file cannot be used as lyrics.</summary>
public static class LrcFile
{
    private const string NotText = "That file is not a text file, so it was not read.";

    private const string NotUtf8 = "That file is not UTF-8 text, so it was not read.";

    private const string TooLarge = "That file holds more text than a clip's lyrics may, so it was not read.";

    private const string NoTimes = "That file has no timed lines, so it is not an LRC file.";

    private static readonly UTF8Encoding _strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
