using AudioLab.Core.Commands;

namespace AudioLab.Web.Session;

/// <summary>A recorded pass ready to commit: its points in time order, and whether the lane hands back to the old curve after the last one.</summary>
/// <param name="Points">The points the pass wrote.</param>
/// <param name="ReturnToOld">True for touch, which hands the lane back to the old curve just after the last point.</param>
public readonly record struct AutomationPass(IReadOnlyList<WrittenPoint> Points, bool ReturnToOld);
