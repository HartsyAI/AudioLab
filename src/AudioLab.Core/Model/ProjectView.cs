namespace AudioLab.Core.Model;

/// <summary>How the arrangement looks and edits when the project opens; the extension kept this with the project.</summary>
public sealed record ProjectView
{
    /// <summary>Which ruler shows first.</summary>
    public required RulerMode Ruler { get; init; }

    /// <summary>How edits snap and move other material.</summary>
    public required EditMode EditMode { get; init; }

    /// <summary>The grid value: auto, bar, a note value such as 1/8, or a time such as 0.5s.</summary>
    public required string Grid { get; init; }
}
