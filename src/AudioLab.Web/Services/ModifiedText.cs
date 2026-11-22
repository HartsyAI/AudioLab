using System.Globalization;

namespace AudioLab.Web.Services;

/// <summary>Says when a project last changed, as a person would: "just now", "5 min ago", "3 h ago", "yesterday", or the date.</summary>
public static class ModifiedText
{
    /// <summary>The text for a change at <paramref name="modified"/>, seen at <paramref name="now"/>, in the viewer's own zone.</summary>
    public static string Of(DateTimeOffset modified, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        TimeSpan age = now - modified;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }
        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min ago";
        }
        if (age < TimeSpan.FromHours(24) && TimeZoneInfo.ConvertTime(modified, zone).Date == TimeZoneInfo.ConvertTime(now, zone).Date)
        {
            return $"{(int)age.TotalHours} h ago";
        }
        DateTime local = TimeZoneInfo.ConvertTime(modified, zone).DateTime;
        DateTime today = TimeZoneInfo.ConvertTime(now, zone).Date;
        return local.Date == today.AddDays(-1) ? "yesterday" : local.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }
}
