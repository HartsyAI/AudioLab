using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The sentences for what a file is refused for before it is read or sent.</summary>
public static class UploadChecks
{
    /// <summary>Why a file larger than the limit is refused, with both sizes in binary units.</summary>
    public static string TooLarge(long length, long limit)
    {
        return $"The file is too large: it is {Size(length)} and the server accepts at most {Size(limit)}.";
    }

    /// <summary>The first reason the user's limits refuse a file of this length, in the server's own words where it has them, or null.</summary>
    /// <remarks>The size limit of one file is judged separately (<see cref="TooLarge"/>); this is the quota.</remarks>
    public static string? AgainstQuota(MediaLimits limits, long length)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.FilesRemaining <= 0)
        {
            return $"You already have {limits.MaxFiles} media files, the most allowed; delete one first.";
        }
        if (length > limits.BytesRemaining)
        {
            return $"There is room for {Size(limits.BytesRemaining)} more of media and this file is {Size(length)}; delete some files first.";
        }
        return null;
    }

    /// <summary>A size with a binary unit, such as 258 KiB or 12.4 MiB.</summary>
    public static string Size(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        string format = unit == 0 || value >= 100 ? "0" : "0.#";
        string number = value.ToString(format, CultureInfo.InvariantCulture);
        return $"{number} {units[unit]}";
    }
}
