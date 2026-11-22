using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>The media of one render plan, at the plan's sample rate, for whatever builds the plan's sources.</summary>
public interface IPlanMedia
{
    /// <summary>The audio of a media file, or null when it is not available, in which case the plan fails once it is built.</summary>
    public PcmAudio? Get(MediaId id);
}
