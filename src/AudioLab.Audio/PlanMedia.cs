using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>The media of one plan: it asks the shared cache, and remembers what was missing so that the plan can fail on it.</summary>
internal sealed class PlanMedia(MediaCache cache) : IPlanMedia
{
    private readonly List<MediaId> _missing = [];

    public PcmAudio? Get(MediaId id)
    {
        PcmAudio? audio = cache.Get(id);
        if (audio is null && !_missing.Contains(id))
        {
            _missing.Add(id);
        }
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
