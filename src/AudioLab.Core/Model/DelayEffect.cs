namespace AudioLab.Core.Model;

/// <summary>A feedback delay, free-running or synced to the tempo.</summary>
public sealed record DelayEffect : Effect
{
    /// <summary>The note divisions a synced delay may use, longest first.</summary>
    public static IReadOnlyList<string> Divisions { get; } = ["1/2", "1/4", "1/4.", "1/8", "1/8.", "1/16"];

    /// <summary>The delay time when not synced, in seconds.</summary>
    public required double Time { get; init; }

    /// <summary>Whether the time follows the tempo.</summary>
    public required bool Sync { get; init; }

    /// <summary>The note value used when synced, such as 1/4 or 1/8.</summary>
    public required string Division { get; init; }

    /// <summary>The feedback share, 0 to 0.9.</summary>
    public required double Feedback { get; init; }

    /// <summary>The low-pass frequency of the feedback, in hertz.</summary>
    public required double Tone { get; init; }

    /// <summary>The wet share, 0 to 1.</summary>
    public required double Mix { get; init; }
}
