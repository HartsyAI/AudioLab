using AudioLab.Core.Model;

namespace AudioLab.Server.Services;

/// <summary>Builds the empty project a user starts from.</summary>
public static class NewProjectFactory
{
    /// <summary>The project's starting tempo, in beats per minute.</summary>
    public const double StartingBpm = 120;

    /// <summary>An empty project at <see cref="StartingBpm"/> in 4/4, owned by <paramref name="owner"/>.</summary>
    public static ProjectDocument Create(ProjectId id, UserId owner, string name)
    {
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = id,
            Owner = owner,
            Name = name,
            Bpm = StartingBpm,
            TempoMap = [],
            TimeSignature = new TimeSignature(4, 4),
            MasterVolume = 1,
            MasterLimiter = true,
            Metronome = false,
            Markers = [],
            Sections = [],
            Groups = [],
            BeatPattern = new BeatPattern { Steps = 16, Swing = 0, Lanes = [] },
            Tracks = [],
            View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Grid, Grid = "auto" },
        };
    }
}
