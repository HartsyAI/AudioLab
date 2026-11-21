namespace AudioLab.Core.Model;

/// <summary>The lyrics a clip carries and how they were timed.</summary>
public sealed record ClipLyrics
{
    /// <summary>The text the user sees and edits; section tags such as [Verse 1] are allowed.</summary>
    public required string Text { get; init; }

    /// <summary>The timed words, in order.</summary>
    public required IReadOnlyList<LyricWord> Words { get; init; }

    /// <summary>Where the lyrics came from, such as manual, generated or transcribed.</summary>
    public string? Source { get; init; }

    /// <summary>The model that generated or aligned them.</summary>
    public string? Model { get; init; }
}
