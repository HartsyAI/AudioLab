using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The rule that says which automation modes the screen can choose (D71), as a service, so the recorder asks the same rule the menu asks.</summary>
public interface IAutomationModeRules
{
    /// <summary>The reason a mode cannot be chosen, or null when it can.</summary>
    /// <param name="mode">The mode.</param>
    public string? UnavailableReason(AutomationMode mode);
}
