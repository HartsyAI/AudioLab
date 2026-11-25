using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session;

/// <summary>The word under the playhead of the clip whose lyrics are shown; it reads the transport once per tick and says so only when the word changes.</summary>
/// <remarks>Each word listens and redraws itself only when it becomes or stops being the one under the playhead, so the list is never drawn again for a tick.</remarks>
public sealed class LyricsCursor : IDisposable
{
    private readonly PlaybackService _playback;

    private AudioClip? _clip;

    private IReadOnlyList<LyricWord> _words = [];

    /// <summary>Creates a cursor that follows the transport of <paramref name="playback"/>.</summary>
    public LyricsCursor(PlaybackService playback)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _playback.Changed += OnPlaybackChanged;
    }

    /// <summary>Raised when the word under the playhead changes.</summary>
    public event Action? Changed;

    /// <summary>The index of the word under the playhead, or null when none is.</summary>
    public int? Index { get; private set; }

    /// <summary>Follows the words of a clip, or nothing when <paramref name="clip"/> is null.</summary>
    public void Follow(AudioClip? clip, ClipLyrics? lyrics)
    {
        _clip = clip;
        _words = lyrics?.Words ?? [];
        Index = Locate();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _playback.Changed -= OnPlaybackChanged;
    }

    private void OnPlaybackChanged()
    {
        int? next = Locate();
        if (next != Index)
        {
            Index = next;
            Changed?.Invoke();
        }
    }

    private int? Locate() => _clip is { } clip ? LyricsTiming.WordAt(_words, LyricsTiming.SourceAt(clip, _playback.Position)) : null;
}
