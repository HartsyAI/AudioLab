namespace AudioLab.Core.Rules;

/// <summary>Constants of placing things on the timeline that more than one domain shares.</summary>
public static class TimelineRules
{
    /// <summary>How close two markers or two tempo changes may be before they count as the same spot, in seconds.</summary>
    public const double SameSpot = 0.001;

    /// <summary>The earliest a tempo change may start; a change at the start is the project tempo instead.</summary>
    public const double MinTempoChangeTime = 0.01;

    /// <summary>A tempo change moved by less than this stays where it is, in seconds.</summary>
    public const double TempoNoMove = 1e-4;

    /// <summary>A move smaller than this changes nothing, in seconds.</summary>
    public const double NoMove = 1e-6;
}
