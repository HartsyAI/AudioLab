using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Which automation modes the screen can choose now (D71).</summary>
/// <remarks>Off and read play the lanes; touch, latch and write record a pass, which needs the recorder wired to playback.</remarks>
public static class AutomationModes
{
    /// <summary>Why touch, latch and write cannot be chosen yet.</summary>
    public const string RecorderNotWired = "The recorder is not wired to playback yet.";

    /// <summary>The reason a mode cannot be chosen, or null when it can.</summary>
    /// <param name="mode">The mode.</param>
    public static string? UnavailableReason(AutomationMode mode)
    {
        return mode is AutomationMode.Touch or AutomationMode.Latch or AutomationMode.Write ? RecorderNotWired : null;
    }
}
