using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Which automation modes the screen can choose now (D71).</summary>
/// <remarks>Off and read play the lanes; touch, latch and write record a pass. The recorder is wired (D86); the fader grab is not.</remarks>
public static class AutomationModes
{
    /// <summary>Why touch, latch and write cannot be chosen yet: the track faders take no grab until the mixer work lands.</summary>
    public const string FaderGrabPending = "The fader grab is waiting on the mixer work still in review.";

    /// <summary>The reason a mode cannot be chosen, or null when it can.</summary>
    /// <param name="mode">The mode.</param>
    public static string? UnavailableReason(AutomationMode mode)
    {
        return mode is AutomationMode.Touch or AutomationMode.Latch or AutomationMode.Write ? FaderGrabPending : null;
    }
}
