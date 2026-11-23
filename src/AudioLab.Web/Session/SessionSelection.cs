using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>What is selected in the arrangement: a view state of this client, never part of the document.</summary>
public sealed class SessionSelection
{
    /// <summary>Raised when the selection changed.</summary>
    public event Action? Changed;

    /// <summary>The selected track, or null.</summary>
    public TrackId? Track { get; private set; }

    /// <summary>The selected clip, or null.</summary>
    public ClipId? Clip { get; private set; }

    /// <summary>Selects a track, and with it no clip.</summary>
    public void SelectTrack(TrackId track) => Set(track, null);

    /// <summary>Selects a clip and the track it is on.</summary>
    public void SelectClip(TrackId track, ClipId clip) => Set(track, clip);

    /// <summary>Selects nothing.</summary>
    public void Clear() => Set(null, null);

    /// <summary>Drops a selection of something the document no longer has.</summary>
    public void Prune(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == Track);
        bool clipExists = track is not null && Clip is { } clip && (track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip));
        if (track is null)
        {
            Clear();
        }
        else if (Clip is not null && !clipExists)
        {
            Set(track.Id, null);
        }
    }

    private void Set(TrackId? track, ClipId? clip)
    {
        if (track == Track && clip == Clip)
        {
            return;
        }
        Track = track;
        Clip = clip;
        Changed?.Invoke();
    }
}
