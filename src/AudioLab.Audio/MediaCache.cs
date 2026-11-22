using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Asks the media source once for each file a plan uses, brings it to the plan's sample rate, and remembers what is missing.</summary>
internal sealed class MediaCache(IMediaSource source, int sampleRate)
{
    private readonly Dictionary<MediaId, PcmAudio?> _audio = [];
    private readonly List<MediaId> _missing = [];

    /// <summary>The audio of a media file at the plan's rate, or null, in which case the file is counted as missing.</summary>
    public PcmAudio? Get(MediaId id)
    {
        if (_audio.TryGetValue(id, out PcmAudio? known))
        {
            return known;
        }
        PcmAudio? audio = null;
        if (source.TryGet(id, out PcmAudio? found))
        {
            audio = Resampler.ToRate(found, sampleRate);
        }
        else
        {
            _missing.Add(id);
        }
        _audio[id] = audio;
        return audio;
    }

    public void ThrowIfMissing()
    {
        if (_missing.Count > 0)
        {
            throw new MissingMediaException(_missing);
        }
    }
}
