namespace AudioLab.Web.Session;

/// <summary>The scale of the lanes now, held where everything that maps a pointer to a time reads it: the timeline, and a dragged file.</summary>
public sealed class TimelineZoom
{
    /// <summary>How seconds become pixels.</summary>
    public TimelineGeometry Geometry { get; set; } = TimelineGeometry.Default;
}
