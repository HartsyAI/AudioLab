using AudioLab.Core.Model;
using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>A job in memory: its state file, and the progress its chunk log holds. Changed only under the jobs' lock.</summary>
internal sealed class TranscriptionJob(UserId owner, MediaId media)
{
    /// <summary>The user who owns the job's media.</summary>
    public UserId Owner { get; } = owner;

    /// <summary>The media file the job transcribes.</summary>
    public MediaId Media { get; } = media;

    /// <summary>The state file as last written.</summary>
    public TranscriptionJobFile File { get; set; } = null!;

    /// <summary>The order in which the job was accepted; the queue runs the oldest first.</summary>
    public long Sequence { get; set; }

    /// <summary>The length of the media in seconds, read when the job starts running.</summary>
    public double Duration { get; set; }

    /// <summary>Whether the chunk log has been read and checked for this run.</summary>
    public bool Prepared { get; set; }

    /// <summary>How many windows are finished.</summary>
    public int Done { get; set; }

    /// <summary>How far into the media the finished windows reach, in seconds.</summary>
    public double Reached { get; set; }

    /// <summary>How many segments the kept chunks hold, counted against the transcript's limit (D94).</summary>
    public int Segments { get; set; }

    /// <summary>The bytes the kept chunks would take in the transcript, counted against its limit (D95).</summary>
    public long Bytes { get; set; }

    /// <summary>The control characters removed from the kept chunks' answers, reported on the status (D94).</summary>
    public int Stripped { get; set; }

    /// <summary>Whether a cancel is waiting for the next chunk boundary.</summary>
    public bool CancelRequested { get; set; }
}
