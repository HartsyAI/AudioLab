using System.Globalization;

namespace AudioLab.Web.Services;

/// <summary>The sentences for what a file is refused for before it is read or sent.</summary>
public static class UploadChecks
{
    /// <summary>Why a file larger than the limit is refused, with both sizes in binary units.</summary>
    public static string TooLarge(long length, long limit)
    {
        return $"The file is too large: it is {Size(length)} and the server accepts at most {Size(limit)}.";
    }

    /// <summary>Why a file larger than the page's own ceiling is refused although the server would take it.</summary>
    public static string TooLargeForPage(long length, long ceiling, long served)
    {
        return $"The file is too large for this page: it is {Size(length)} and the page can send at most {Size(ceiling)} "
            + $"(the server takes up to {Size(served)}).";
    }

    /// <summary>The reason the room left in the quota cannot take a file of this length, or null when it can.</summary>
    /// <param name="maxFiles">The most files the user may keep.</param>
    /// <param name="filesLeft">How many more files fit, after the uploads on their way.</param>
    /// <param name="bytesLeft">How many more bytes fit, after the uploads on their way.</param>
    /// <param name="length">The size of the file.</param>
    public static string? AgainstQuota(int maxFiles, long filesLeft, long bytesLeft, long length)
    {
        if (filesLeft <= 0)
        {
            return maxFiles == 1
                ? "You already have 1 media file, the most allowed; delete it first."
                : $"You already have {maxFiles} media files, the most allowed; delete one first.";
        }
        long room = Math.Max(0, bytesLeft);
        if (length <= room)
        {
            return null;
        }
        // Two sizes that round to the same figure would read as a refusal of a file that fits.
        bool alike = Size(room) == Size(length);
        return $"There is room for {(alike ? Exact(room) : Size(room))} more of media and this file is {(alike ? Exact(length) : Size(length))}; "
            + "delete some files first.";
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

    private static string Exact(long bytes) => $"{bytes.ToString("N0", CultureInfo.InvariantCulture)} bytes";
}
