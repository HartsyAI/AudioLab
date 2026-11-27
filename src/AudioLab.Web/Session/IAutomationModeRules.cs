using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The rule that says which automation modes the screen can choose on a lane (D71), as a service, so the recorder asks the same rule the menu asks.</summary>
public interface IAutomationModeRules
{
    /// <summary>The reason a mode cannot be chosen on a lane, or null when it can.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="parameter">The parameter of the lane.</param>
    public string? UnavailableReason(AutomationMode mode, AutomationParameter parameter);
}
