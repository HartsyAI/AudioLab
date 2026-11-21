using AudioLab.Web.Session;

namespace AudioLab.Web.Rendering;

/// <summary>The drawing primitives the lane scene needs. A backend is one library: Skia or the browser's 2D canvas.</summary>
public interface IPaintBackend
{
    /// <summary>Drawing calls made across the interop boundary since the last <see cref="Begin"/>, where that is meaningful.</summary>
    public int CallCount { get; }

    /// <summary>Starts a frame that repaints only the given rectangle, in CSS pixels, filling it with the background; nothing outside it changes.</summary>
    public void Begin(double x, double y, double width, double height, Rgba background);

    /// <summary>Fills a rectangle.</summary>
    public void FillRect(double x, double y, double width, double height, Rgba colour);

    /// <summary>Fills a rounded rectangle.</summary>
    public void FillRoundRect(double x, double y, double width, double height, double radius, Rgba colour);

    /// <summary>Strokes a rounded rectangle; the line is centred on the edge.</summary>
    public void StrokeRoundRect(double x, double y, double width, double height, double radius, double lineWidth, Rgba colour);

    /// <summary>Restricts drawing to a rounded rectangle until <see cref="PopClip"/>.</summary>
    public void PushRoundClip(double x, double y, double width, double height, double radius);

    /// <summary>Ends the last clip.</summary>
    public void PopClip();

    /// <summary>Fills a clip's cached waveform into a box.</summary>
    public void FillWave(ClipModel clip, double x, double y, double width, double height, Rgba colour);

    /// <summary>Draws one line of text whose baseline is at y, with the left edge at x.</summary>
    public void DrawText(string text, double x, double baseline, double size, int weight, Rgba colour);

    /// <summary>Width of the text in CSS pixels.</summary>
    public double MeasureText(string text, double size, int weight);

    /// <summary>Ends the frame.</summary>
    public void End();
}
