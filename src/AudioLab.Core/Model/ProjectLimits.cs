namespace AudioLab.Core.Model;

/// <summary>The practical bounds a version 4 document keeps, so no value can overflow a calculation.</summary>
public static class ProjectLimits
{
    /// <summary>The latest time or length, in seconds: about eleven and a half days.</summary>
    public const double MaxSeconds = 1_000_000;

    /// <summary>The highest gain of a fader, clip, send or group: +24 dB.</summary>
    public const double MaxGain = 16;

    /// <summary>The slowest tempo, in beats per minute.</summary>
    public const double MinBpm = 1;

    /// <summary>The fastest tempo, in beats per minute.</summary>
    public const double MaxBpm = 1000;

    /// <summary>The longest name of a project, track, marker, section, group or clip, in characters.</summary>
    public const int MaxName = 200;

    /// <summary>The longest short label such as a preset, a grid value or a source, in characters.</summary>
    public const int MaxLabel = 128;

    /// <summary>The longest long text such as lyrics or a score, in characters.</summary>
    public const int MaxLongText = 1_000_000;

    /// <summary>The most tracks a project has.</summary>
    public const int MaxTracks = 1024;

    /// <summary>The most steps a drum pattern has.</summary>
    public const int MaxBeatSteps = 256;

    /// <summary>The least swing a drum pattern takes.</summary>
    public const double MinBeatSwing = 0;

    /// <summary>The most swing a drum pattern takes.</summary>
    public const double MaxBeatSwing = 1;

    /// <summary>The most lanes a drum pattern has.</summary>
    public const int MaxBeatLanes = 128;

    /// <summary>How far apart two times in seconds may be and still count as the same.</summary>
    public const double Tolerance = 1e-9;

    /// <summary>The longest command a client may send as JSON, in characters; the biggest holds a million-character text.</summary>
    public const int MaxCommandLength = 16_777_216;

    /// <summary>The most problems an error lists before it says how many more there were.</summary>
    public const int MaxProblems = 100;

    /// <summary>The most items in one list of notes, controller events, words or automation points.</summary>
    public const int MaxListItems = 1_000_000;

    /// <summary>The most audio or MIDI clips on one track.</summary>
    public const int MaxClips = 100_000;

    /// <summary>The most effects on one track.</summary>
    public const int MaxEffects = 64;

    /// <summary>The most sends on one track, and groups in a project.</summary>
    public const int MaxSends = 1024;

    /// <summary>The most markers, sections or tempo changes in a project.</summary>
    public const int MaxMarks = 100_000;

    /// <summary>The most unknown fields one holder keeps.</summary>
    public const int MaxExtraFields = 256;

    /// <summary>The most characters of JSON text all unknown fields of one holder may total.</summary>
    public const int MaxExtraLength = 65_536;

    /// <summary>The most characters one listed problem keeps.</summary>
    public const int MaxProblemLength = 300;

    /// <summary>Cuts text to <see cref="MaxProblemLength"/> characters, so an error never echoes an attack.</summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length > MaxProblemLength ? text[..MaxProblemLength] + "..." : text;
    }
}
