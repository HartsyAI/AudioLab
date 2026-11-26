using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Reads and writes the swing and step count typed into the drums tab, with the bounds Core checks.</summary>
public static class BeatDraft
{
    private const NumberStyles Spaces = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    private const NumberStyles SwingStyle = Spaces | NumberStyles.AllowDecimalPoint;

    private const NumberStyles StepsStyle = Spaces;

    /// <summary>Reads a swing from 0 to 1 in invariant decimal form.</summary>
    public static bool TryParseSwing(string text, out double swing)
    {
        swing = 0;
        return double.TryParse(text, SwingStyle, CultureInfo.InvariantCulture, out swing)
            && swing is >= ProjectLimits.MinBeatSwing and <= ProjectLimits.MaxBeatSwing;
    }

    /// <summary>Reads a whole step count from 1 to the most a pattern may have.</summary>
    public static bool TryParseSteps(string text, out int steps)
    {
        steps = 0;
        return int.TryParse(text, StepsStyle, CultureInfo.InvariantCulture, out steps)
            && steps is >= 1 and <= ProjectLimits.MaxBeatSteps;
    }

    /// <summary>Writes a swing so that reading it back gives the same value.</summary>
    public static string FormatSwing(double swing) => swing.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Whether a typed swing is a different value from the pattern's, so that Set swing has something to send.</summary>
    public static bool CanApplySwing(string text, double current) => TryParseSwing(text, out double swing) && swing != current;

    /// <summary>Whether a typed step count is a different value from the pattern's, so that Set steps has something to send.</summary>
    public static bool CanApplySteps(string text, int current) => TryParseSteps(text, out int steps) && steps != current;

    /// <summary>The reason a typed swing is not accepted; a comma is named, since it is the usual slip.</summary>
    public static string SwingReason(string text)
    {
        return text.Contains(',', StringComparison.Ordinal)
            ? "Use a dot for the decimal point, for example 0.25."
            : "Enter a number from 0 to 1, for example 0.25.";
    }

    /// <summary>The reason a typed step count is not accepted.</summary>
    public static string StepsReason() => $"Enter a whole number from 1 to {ProjectLimits.MaxBeatSteps}.";

    /// <summary>Reads a lane gain from 0 up to the most a lane may have, in invariant decimal form.</summary>
    public static bool TryParseGain(string text, out double gain)
    {
        gain = 0;
        return double.TryParse(text, SwingStyle, CultureInfo.InvariantCulture, out gain)
            && gain is >= 0 and <= ProjectLimits.MaxGain;
    }

    /// <summary>Writes a gain so that reading it back gives the same value.</summary>
    public static string FormatGain(double gain) => gain.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Whether a typed gain is a different value from the lane's, so that Set gain has something to send.</summary>
    public static bool CanApplyGain(string text, double current) => TryParseGain(text, out double gain) && gain != current;

    /// <summary>The reason a typed gain is not accepted; a comma is named, as it is for swing.</summary>
    public static string GainReason(string text)
    {
        return text.Contains(',', StringComparison.Ordinal)
            ? "Use a dot for the decimal point, for example 0.5."
            : $"Enter a number from 0 to {ProjectLimits.MaxGain.ToString(CultureInfo.InvariantCulture)}, for example 1.";
    }

    /// <summary>Whether the pattern's swing or step count differs from the values the drafts came from.</summary>
    public static bool ChangedSince(BeatPattern pattern, double seenSwing, int seenSteps)
    {
        return pattern.Swing != seenSwing || pattern.Steps != seenSteps;
    }
}
