namespace AudioLab.Web.Session;

/// <summary>The open session as the arrangement view needs it.</summary>
public sealed class SessionModel
{
    /// <summary>Length of the song in bars.</summary>
    public required int Bars { get; init; }

    /// <summary>Tracks from top to bottom.</summary>
    public required IReadOnlyList<TrackModel> Tracks { get; init; }

    /// <summary>Mixer strips, the master last.</summary>
    public required IReadOnlyList<StripModel> Strips { get; init; }

    /// <summary>Sections in order.</summary>
    public required IReadOnlyList<SectionModel> Sections { get; init; }

    /// <summary>First bar of the loop, from the song start.</summary>
    public required double LoopStartBar { get; init; }

    /// <summary>Loop length in bars.</summary>
    public required double LoopBars { get; init; }

    /// <summary>Playhead position in bars from the song start.</summary>
    public required double PlayheadBar { get; init; }

    /// <summary>Every clip of every track, in id order.</summary>
    public required IReadOnlyList<ClipModel> Clips { get; init; }

    /// <summary>Selects one clip and clears the selection of the others.</summary>
    public void SelectClip(ClipModel? clip)
    {
        foreach (ClipModel other in Clips)
        {
            bool selected = ReferenceEquals(other, clip);
            if (other.Selected != selected)
            {
                other.Selected = selected;
                other.Revision++;
            }
        }
    }
}
