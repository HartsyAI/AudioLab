using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The screen's rule: <see cref="AutomationModes"/>, as it stands in the build.</summary>
public sealed class ScreenAutomationModes : IAutomationModeRules
{
    /// <inheritdoc />
    public string? UnavailableReason(AutomationMode mode) => AutomationModes.UnavailableReason(mode);
}
