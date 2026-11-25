namespace AudioLab.Web.Session;

/// <summary>A decision of a key in an automation lane.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Index">The point it concerns, or the point selected by it.</param>
/// <param name="Direction">The direction of a nudge.</param>
public readonly record struct AutomationKeyAction(AutomationKeyKind Kind, int? Index = null, NudgeDirection? Direction = null);
