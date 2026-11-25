using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>What one automation lane draws, as values: a lane draws again only when one of them changed (D68).</summary>
/// <param name="Points">The lane's points as the document holds them; compared by reference, as D16 keeps unchanged lists.</param>
/// <param name="Selected">The selected point, or null.</param>
/// <param name="Dragging">Whether a point of this lane is dragged.</param>
/// <param name="DragIndex">The dragged point's index, or null.</param>
/// <param name="DragTime">Where the dragged point would go, in seconds.</param>
/// <param name="DragValue">The value the dragged point would take.</param>
/// <param name="Width">The lane's width in pixels.</param>
/// <param name="Height">The lane's height in pixels.</param>
/// <param name="Top">The lane's top in pixels.</param>
/// <param name="ColorToken">The track colour.</param>
/// <param name="Editable">Whether the lane may be edited.</param>
/// <param name="Reason">Why it may not, or null.</param>
/// <param name="Name">The accessible name.</param>
/// <param name="Parameter">The lane's parameter.</param>
public readonly record struct AutomationLaneShown(
    object? Points,
    int? Selected,
    bool Dragging,
    int? DragIndex,
    double DragTime,
    double DragValue,
    double Width,
    double Height,
    double Top,
    string ColorToken,
    bool Editable,
    string? Reason,
    string Name,
    AutomationParameter Parameter);
