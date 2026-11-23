using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Media;

/// <summary>Writes what the library shows about a file: its length, its format and its size.</summary>
public static class MediaText
{
    /// <summary>The length as minutes, seconds and tenths, such as 3:07.4, or hours first for a long file.</summary>
    public static string Length(double seconds)
    {
        long tenths = (long)Math.Round(Math.Max(0, seconds) * 10, MidpointRounding.AwayFromZero);
        long hours = tenths / 36_000;
        long minutes = tenths / 600 % 60;
        long secs = tenths / 10 % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}.{tenths % 10}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{secs:00}.{tenths % 10}");
    }

    /// <summary>The file's size with a binary unit, such as 258 KiB or 12.4 MiB.</summary>
    public static string Size(long bytes) => UploadChecks.Size(bytes);

    /// <summary>The sample format and rate and the channels, such as 16-bit 44.1 kHz stereo.</summary>
    public static string Format(MediaSummary file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string bits = file.Encoding switch
        {
            SampleEncoding.Pcm16 => "16-bit",
            SampleEncoding.Pcm24 => "24-bit",
            SampleEncoding.Pcm32 => "32-bit",
            _ => "32-bit float",
        };
        string rate = (file.SampleRate / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        string channels = file.Channels switch
        {
            1 => "mono",
            2 => "stereo",
            _ => string.Create(CultureInfo.InvariantCulture, $"{file.Channels} channels"),
        };
        return $"{bits} {rate} kHz {channels}";
    }

    /// <summary>How long the wait for the server has lasted, such as 12 s or 1 min 5 s.</summary>
    public static string Elapsed(TimeSpan time)
    {
        long seconds = (long)Math.Max(0, Math.Floor(time.TotalSeconds));
        return seconds < 60
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds} s")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} min {seconds % 60} s");
    }
}
