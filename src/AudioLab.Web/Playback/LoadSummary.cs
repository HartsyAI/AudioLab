namespace AudioLab.Web.Playback;

/// <summary>What the scripted drag and scroll load did to the page while the audio played.</summary>
public sealed record LoadSummary
{
    /// <summary>Scenarios finished.</summary>
    public required int Scenarios { get; init; }

    /// <summary>Animation frames recorded over all of them.</summary>
    public required long Frames { get; init; }

    /// <summary>Frames whose interval was over 100 ms.</summary>
    public required long FramesOver100Ms { get; init; }

    /// <summary>The longest frame interval, in milliseconds.</summary>
    public required double MaxFrameMs { get; init; }

    /// <summary>The lowest frame rate over any one-second window of any scenario.</summary>
    public required double WorstSecondFps { get; init; }
}
