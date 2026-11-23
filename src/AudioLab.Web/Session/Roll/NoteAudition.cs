using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session.Roll;

/// <summary>What the piano roll asks the sound engine to play: a key held under the pointer, and a note heard for as long as it lasts.</summary>
/// <remarks>It asks only when the sink says it can; with no engine in the page it does nothing, and <see cref="Unavailable"/> shows.</remarks>
public sealed class NoteAudition(IAuditionSink sink, TimeProvider clock)
{
    /// <summary>The longest a note is held when it is heard by selecting it, in milliseconds.</summary>
    public const int MaxHoldMilliseconds = 1500;

    /// <summary>Why nothing can be heard now, or null when notes can be.</summary>
    public string? Unavailable => sink.Unavailable;

    /// <summary>Whether notes can be heard now.</summary>
    public bool Available => sink.Unavailable is null;

    /// <summary>Starts the note of a key pressed in the roll; it sounds until <see cref="ReleaseKeyAsync"/>.</summary>
    public Task PressKeyAsync(TrackId track, int pitch, int velocity)
    {
        return Available ? sink.NoteOnAsync(track, Math.Clamp(pitch, 0, 127), Math.Clamp(velocity, 1, 127)) : Task.CompletedTask;
    }

    /// <summary>Lets the note of a key go.</summary>
    public Task ReleaseKeyAsync(TrackId track, int pitch)
    {
        return Available ? sink.NoteOffAsync(track, Math.Clamp(pitch, 0, 127)) : Task.CompletedTask;
    }

    /// <summary>Plays a note for its own length, at most <see cref="MaxHoldMilliseconds"/>, so that selecting or walking to it is heard.</summary>
    public async Task HearNoteAsync(TrackId track, MidiNote note)
    {
        if (!Available || note.Velocity < 1)
        {
            return;
        }
        await sink.NoteOnAsync(track, note.Pitch, note.Velocity);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(note.Duration * 1000, MaxHoldMilliseconds)), clock);
        }
        finally
        {
            await sink.NoteOffAsync(track, note.Pitch);
        }
    }
}
