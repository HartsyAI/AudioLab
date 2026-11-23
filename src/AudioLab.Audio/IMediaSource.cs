using System.Diagnostics.CodeAnalysis;
using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Supplies decoded audio by media id. The engine asks while it prepares a render, never while it renders.</summary>
public interface IMediaSource
{
    /// <summary>Finds the decoded audio of a media file, or returns false when it is not available.</summary>
    public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio);
}
