namespace AudioLab.Web.Session;

/// <summary>Where the playhead is, in seconds: a view state of this client, set by what moves it; until the transport exists, the start.</summary>
public sealed class SessionPlayhead
{
    /// <summary>The playhead's time.</summary>
    public double Seconds { get; set; }
}
