using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session.Roll;

/// <summary>What the piano roll asks the sound engine to play: one note at a time, a key held, or a note heard for as long as it lasts.</summary>
/// <remarks>
/// There is one voice. A new note lets the one before it go first, so a note can never be left sounding by one that began after it, and the timer of
/// an earlier note cannot cut a later one. It remembers the track and the pitch it started, so it can let go of them from wherever the pointer, the
/// focus or the page went. It asks only when the sink says it can; with no engine in the page it does nothing, and <see cref="Unavailable"/> shows.
/// </remarks>
public sealed class NoteAudition(IAuditionSink sink, TimeProvider clock)
{
    /// <summary>The longest a note is held when it is heard by selecting it, in milliseconds.</summary>
    public const int MaxHoldMilliseconds = 1500;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private (TrackId Track, int Pitch)? _sounding;

    private CancellationTokenSource? _timer;

    private int _generation;

    /// <summary>Why nothing can be heard now, or null when notes can be.</summary>
    public string? Unavailable => sink.Unavailable;

    /// <summary>Whether notes can be heard now.</summary>
    public bool Available => sink.Unavailable is null;

    /// <summary>Whether a note is sounding.</summary>
    public bool Sounding => _sounding is not null;

    /// <summary>Starts the note of a key pressed in the roll; it sounds until <see cref="StopAsync"/>, or until another note begins.</summary>
    public Task PressKeyAsync(TrackId track, int pitch, int velocity) => StartAsync(track, pitch, velocity, null);

    /// <summary>Plays a note for its own length, at most <see cref="MaxHoldMilliseconds"/>, so that selecting or walking to it is heard.</summary>
    public Task HearNoteAsync(TrackId track, MidiNote note)
    {
        if (note.Velocity < 1)
        {
            return Task.CompletedTask;
        }
        return StartAsync(track, note.Pitch, note.Velocity, TimeSpan.FromMilliseconds(Math.Min(note.Duration * 1000, MaxHoldMilliseconds)));
    }

    /// <summary>Lets the sounding note go, whatever started it; nothing sounding is ignored.</summary>
    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartAsync(TrackId track, int pitch, int velocity, TimeSpan? hold)
    {
        if (!Available)
        {
            return;
        }
        await _gate.WaitAsync();
        try
        {
            await StopCoreAsync();
            int sounded = Math.Clamp(pitch, 0, 127);
            await sink.NoteOnAsync(track, sounded, Math.Clamp(velocity, 1, 127));
            _sounding = (track, sounded);
            int generation = ++_generation;
            if (hold is { } span)
            {
                CancellationTokenSource timer = new();
                _timer = timer;
                _ = ExpireAsync(span, generation, timer);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // The timer of a heard note lets it go, unless another note began, or it was stopped, which cancels the timer.
    private async Task ExpireAsync(TimeSpan span, int generation, CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(span, clock, timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await _gate.WaitAsync();
        try
        {
            if (generation == _generation)
            {
                await StopCoreAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        _timer?.Cancel();
        _timer?.Dispose();
        _timer = null;
        if (_sounding is { } voice)
        {
            _sounding = null;
            await sink.NoteOffAsync(voice.Track, voice.Pitch);
        }
    }
}
