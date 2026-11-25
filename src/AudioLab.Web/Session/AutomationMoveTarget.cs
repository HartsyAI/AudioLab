namespace AudioLab.Web.Session;

/// <summary>What a released point sends: the index it had, its absolute time and value, and the index the server's rule gives it.</summary>
/// <param name="Index">The index before the command runs.</param>
/// <param name="Time">The absolute time in seconds.</param>
/// <param name="Value">The absolute value.</param>
/// <param name="LandsAt">The index of the point after the move, which the lane's selection then follows.</param>
public readonly record struct AutomationMoveTarget(int Index, double Time, double Value, int LandsAt);
