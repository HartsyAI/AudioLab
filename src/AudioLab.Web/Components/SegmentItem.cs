namespace AudioLab.Web.Components;

/// <summary>One choice of a segmented control: a glyph or a word, with the name its tooltip and its screen-reader label share.</summary>
/// <param name="Id">The choice's id, which the control hands back when it is chosen.</param>
/// <param name="Name">The accessible name and the first line of the tooltip.</param>
/// <param name="Icon">The glyph, or null for a word.</param>
/// <param name="Label">The word, when there is no glyph.</param>
/// <param name="Reason">Why the choice is unavailable now, or null when it is available.</param>
/// <param name="Shortcut">The shortcut the tooltip shows.</param>
public readonly record struct SegmentItem(
    string Id,
    string Name,
    IconName? Icon = null,
    string? Label = null,
    string? Reason = null,
    string? Shortcut = null);
