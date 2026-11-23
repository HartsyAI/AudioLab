using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Builds the source of each instrument track while a render is prepared.</summary>
public interface ITrackSourceFactory
{
    /// <summary>The source that plays an instrument track, or null to leave it silent.</summary>
    /// <remarks>A file that the source asks <paramref name="media"/> for and that is missing fails the plan, as a missing clip does.</remarks>
    public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format, IPlanMedia media);
}
