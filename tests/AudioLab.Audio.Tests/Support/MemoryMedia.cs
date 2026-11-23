using System.Diagnostics.CodeAnalysis;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>A media source that holds the audio the test gives it.</summary>
public sealed class MemoryMedia : IMediaSource
{
    private readonly Dictionary<MediaId, PcmAudio> _audio = [];

    public MemoryMedia Add(string id, PcmAudio audio)
    {
        _audio[MediaId.From(id)] = audio;
        return this;
    }

    public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio) => _audio.TryGetValue(id, out audio);
}
