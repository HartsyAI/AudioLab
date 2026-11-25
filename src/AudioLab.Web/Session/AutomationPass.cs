using AudioLab.Core.Commands;

namespace AudioLab.Web.Session;

/// <summary>A recorded pass ready to commit: segments in time order, and whether each hands the lane back to the old curve.</summary>
/// <remarks>Touch records one segment per touch, so the old curve between two touches is kept (D25). Write and latch record one segment.</remarks>
/// <param name="Segments">The points each segment wrote, in order; a segment is committed as one <c>automation_write_commit</c>.</param>
/// <param name="ReturnToOld">True for touch, which hands the lane back to the old curve just after each segment's last point.</param>
/// <param name="Truncated">True when the pass hit the most points it may hold, after which it stopped recording.</param>
public readonly record struct AutomationPass(IReadOnlyList<IReadOnlyList<WrittenPoint>> Segments, bool ReturnToOld, bool Truncated);
