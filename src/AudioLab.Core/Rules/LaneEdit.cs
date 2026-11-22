using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>A lane after an edit, with where the edited point now sits.</summary>
/// <param name="Points">The lane's points, by time.</param>
/// <param name="Index">The place of the point that was added or moved.</param>
public readonly record struct LaneEdit(ValueList<AutomationPoint> Points, int Index);
