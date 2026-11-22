namespace AudioLab.Core.Model;

/// <summary>One breakpoint of an automation lane.</summary>
/// <param name="Time">Where it sits, in seconds.</param>
/// <param name="Value">The parameter value there.</param>
public readonly record struct AutomationPoint(double Time, double Value);
