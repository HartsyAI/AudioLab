namespace AudioLab.Web.Session;

/// <summary>Where the playhead is, in seconds: a view state of this client, set by whatever moves it. Until the transport exists it is the start.</summary>
public sealed class SessionPlayhead
{
    /// <summary>The playhead's time.</summary>
    public double Seconds { get; set; }
}
