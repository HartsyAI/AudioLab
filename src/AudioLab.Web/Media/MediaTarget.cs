namespace AudioLab.Web.Media;

/// <summary>The place a dragged file would land: a row, a start on the grid, and the box of the mark that shows it, in lane pixels.</summary>
/// <param name="Row">The row of the track it would land on.</param>
/// <param name="Start">Where the clip would start, in seconds.</param>
/// <param name="Left">The left edge of the mark.</param>
/// <param name="Top">The top edge of the mark.</param>
/// <param name="Width">The width of the clip.</param>
/// <param name="Height">The height of the clip.</param>
public readonly record struct MediaTarget(int Row, double Start, double Left, double Top, double Width, double Height);
