using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Which automation modes the screen can choose now (D71).</summary>
/// <remarks>Off, read and write are open for every lane. Touch and latch record a volume lane, whose fader takes the grab (D86); a pan lane has
/// no grab yet, so touch and latch on it stay disabled with their reason, rather than saving a mode that records nothing.</remarks>
public static class AutomationModes
{
    /// <summary>Why touch and latch cannot be chosen on a pan lane: the pan knob takes no grab yet.</summary>
    public const string PanGrabPending = "Touch and latch on a pan lane wait on the pan knob's grab.";

    /// <summary>The reason a mode cannot be chosen on a lane, or null when it can.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="parameter">The parameter of the lane the mode would be chosen on.</param>
    public static string? UnavailableReason(AutomationMode mode, AutomationParameter parameter)
    {
        return mode is AutomationMode.Touch or AutomationMode.Latch && parameter == AutomationParameter.Pan ? PanGrabPending : null;
    }
}
