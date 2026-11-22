using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>Writes a time as the counter does: hours, minutes, seconds and milliseconds, with fixed widths so the digits do not jump.</summary>
public static class TimeText
{
    /// <summary>The counter text of a time, such as 0:00:18.500; a negative time reads as zero.</summary>
    public static string Counter(double seconds)
    {
        long milliseconds = (long)Math.Round(Math.Max(0, seconds) * 1000);
        long hours = milliseconds / 3_600_000;
        long minutes = milliseconds / 60_000 % 60;
        long secs = milliseconds / 1000 % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}.{milliseconds % 1000:000}");
    }

    /// <summary>The ruler text of a time, such as 1:05 or 12:03:09.</summary>
    public static string Ruler(double seconds)
    {
        long total = (long)Math.Floor(Math.Max(0, seconds));
        long hours = total / 3600;
        long minutes = total / 60 % 60;
        long secs = total % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{secs:00}");
    }
}
