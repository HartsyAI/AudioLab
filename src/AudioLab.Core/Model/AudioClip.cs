namespace AudioLab.Core.Model;

/// <summary>An audio clip: a window onto a media file, placed on the timeline.</summary>
public sealed record AudioClip
{
    /// <summary>The clip's id.</summary>
    public required ClipId Id { get; init; }

    /// <summary>The clip's name.</summary>
    public required string Name { get; init; }

    /// <summary>The audio file the clip plays.</summary>
    public required MediaId Media { get; init; }

    /// <summary>Where the clip starts on the timeline, in seconds.</summary>
    public required double Start { get; init; }

    /// <summary>The length of the whole media file, in seconds.</summary>
    public required double Duration { get; init; }

    /// <summary>How far into the media file the clip starts, in seconds.</summary>
    public required double Offset { get; init; }

    /// <summary>How much of the media file's end the clip leaves out, in seconds.</summary>
    public required double TrimEnd { get; init; }

    /// <summary>The clip gain, from 0 up; 1 is unity.</summary>
    public required double Gain { get; init; }

    /// <summary>The fade-in length, in seconds.</summary>
    public required double FadeIn { get; init; }

    /// <summary>The fade-out length, in seconds.</summary>
    public required double FadeOut { get; init; }

    /// <summary>Whether the clip is silent.</summary>
    public required bool Muted { get; init; }

    /// <summary>A colour that overrides the track colour, or null.</summary>
    public string? Color { get; init; }

    /// <summary>What the clip was made from or carries: its score, its lyrics, the clip it is a stem of.</summary>
    public ClipMeta? Meta { get; init; }
}
