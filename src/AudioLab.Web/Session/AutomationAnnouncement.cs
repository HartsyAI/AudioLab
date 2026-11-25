using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session;

/// <summary>The words a point edit or a selection is announced with, in the status region (D68).</summary>
public static class AutomationAnnouncement
{
    /// <summary>A point selected in a lane.</summary>
    public static string Selected(AutomationParameter parameter, int index, int count, double time, double value)
    {
        return $"Point {index + 1} of {count} selected: {Name(parameter)} {Format(value)} at {Format(time)} seconds.";
    }

    /// <summary>The announcement of a move, when the server applied it; a refused move announces nothing, since its reason is shown.</summary>
    public static string? MovedIfApplied(EditResult result, AutomationParameter parameter, double time, double value)
    {
        return result.Succeeded ? Moved(parameter, time, value) : null;
    }

    /// <summary>A point added to a lane.</summary>
    public static string Added(AutomationParameter parameter, double time, double value)
    {
        return $"Point added: {Name(parameter)} {Format(value)} at {Format(time)} seconds.";
    }

    /// <summary>A point moved in a lane.</summary>
    public static string Moved(AutomationParameter parameter, double time, double value)
    {
        return $"Point moved: {Name(parameter)} {Format(value)} at {Format(time)} seconds.";
    }

    /// <summary>A point removed from a lane.</summary>
    public static string Removed(int remaining)
    {
        return $"Point removed; {remaining} left in the lane.";
    }

    private static string Name(AutomationParameter parameter) => parameter == AutomationParameter.Volume ? "volume" : "pan";

    private static string Format(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
