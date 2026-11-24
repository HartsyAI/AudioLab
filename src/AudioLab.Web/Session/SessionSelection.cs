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
        if (Clip is { } clip)
        {
            // A clip that moved to another track is still the selection, now on its new track.
            Track? owner = document.Tracks.FirstOrDefault(
                candidate => candidate.Clips.Any(c => c.Id == clip) || candidate.MidiClips.Any(c => c.Id == clip));
            if (owner is not null)
            {
                Set(owner.Id, clip);
                return;
            }
        }
        Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == Track);
        if (track is null)
        {
            Clear();
        }
        else if (Clip is not null)
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
