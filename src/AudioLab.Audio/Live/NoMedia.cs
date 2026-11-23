using System.Diagnostics.CodeAnalysis;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Live;

/// <summary>A media source that has nothing: the live host fills its cache itself, a clip at a time, so a plan only ever asks the cache.</summary>
internal sealed class NoMedia : IMediaSource
{
    /// <summary>The one instance.</summary>
    public static NoMedia Instance { get; } = new();

    /// <inheritdoc />
    public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio)
    {
        audio = null;
        return false;
    }
}
