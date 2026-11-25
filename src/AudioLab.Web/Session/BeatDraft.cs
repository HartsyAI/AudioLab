using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Reads the swing and step count typed into the drums tab; the same bounds Core checks, so a value that passes is one the server takes.</summary>
public static class BeatDraft
{
    /// <summary>Reads a swing from 0 to 1 in invariant decimal form.</summary>
    public static bool TryParseSwing(string text, out double swing)
    {
        swing = 0;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out swing) && swing is >= 0 and <= 1;
    }

    /// <summary>Reads a whole step count from 1 to the most a pattern may have.</summary>
    public static bool TryParseSteps(string text, out int steps)
    {
        steps = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out steps)
            && steps is >= 1 and <= ProjectLimits.MaxBeatSteps;
    }
}
