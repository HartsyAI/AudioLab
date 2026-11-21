namespace AudioLab.Web.Session;

/// <summary>One clip on a lane. Time is in bars from the start of the song; peaks are the cached waveform.</summary>
public sealed class ClipModel
{
    /// <summary>Unique within the session.</summary>
    public required int Id { get; init; }

    /// <summary>Title shown in the clip header.</summary>
    public required string Name { get; init; }

    /// <summary>Length in bars.</summary>
    public required double LengthBars { get; init; }

    /// <summary>Cached peak amplitudes, 0 to 1; empty for a MIDI clip.</summary>
    public required float[] Peaks { get; init; }

    /// <summary>Notes of a MIDI clip; empty for an audio clip.</summary>
    public required NoteSpan[] Notes { get; init; }

    /// <summary>Index of the track the clip sits on.</summary>
    public int Track { get; set; }

    /// <summary>Start in bars from the song start.</summary>
    public double StartBar { get; set; }

    /// <summary>Whether the clip carries the selection ring.</summary>
    public bool Selected { get; set; }

    /// <summary>Incremented on every change a view must redraw for.</summary>
    public int Revision { get; set; }

    /// <summary>True when the clip holds notes, not a waveform.</summary>
    public bool IsMidi => Notes.Length > 0;
}
