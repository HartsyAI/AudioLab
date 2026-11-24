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
