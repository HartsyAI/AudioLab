using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>What the roll asks the sound engine for, through the seam, and that it asks for nothing when the engine is not there.</summary>
public sealed class NoteAuditionTests
{
    private static readonly TrackId _track = TrackId.From("keys");

    private readonly RecordingSink _sink = new();

    private readonly ManualClock _clock = new();

    /// <summary>The page has no sound engine yet: the sink says why, and the roll sends it nothing.</summary>
    [Fact]
    public async Task WithNoEngineTheSinkSaysWhyAndNothingIsSent()
    {
        UnavailableAudition none = new();
        NoteAudition audition = new(none, _clock);

        Assert.NotNull(none.Unavailable);
        Assert.False(audition.Available);
        await audition.PressKeyAsync(_track, 60, 100);
        await audition.ReleaseKeyAsync(_track, 60);
        await audition.HearNoteAsync(_track, new MidiNote(new NoteId(1), 60, 0, 1, 100));
        Assert.Equal(none.Unavailable, audition.Unavailable);
    }

    /// <summary>A key is on while it is held and off when it is let go, with the pitch and velocity kept in range.</summary>
    [Fact]
    public async Task AKeyIsOnWhileItIsHeldAndOffWhenLetGo()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.PressKeyAsync(_track, 200, 0);
        await audition.ReleaseKeyAsync(_track, 200);

        Assert.Equal(["on keys 127 1", "off keys 127"], _sink.Calls);
    }

    /// <summary>A note that is selected is heard for its own length, never longer than the cap, and a note of velocity 0 is silent.</summary>
    [Fact]
    public async Task ASelectedNoteIsHeardForItsOwnLengthAtMostTheCap()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        Task short1 = audition.HearNoteAsync(_track, new MidiNote(new NoteId(1), 64, 0, 0.5, 90));
        Assert.Equal(["on keys 64 90"], _sink.Calls);
        _clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(short1.IsCompleted);
        _clock.Advance(TimeSpan.FromMilliseconds(2));
        await short1;
        Assert.Equal(["on keys 64 90", "off keys 64"], _sink.Calls);

        _sink.Calls.Clear();
        Task longer = audition.HearNoteAsync(_track, new MidiNote(new NoteId(2), 67, 0, 30, 90));
        _clock.Advance(TimeSpan.FromMilliseconds(NoteAudition.MaxHoldMilliseconds + 1));
        await longer;
        Assert.Equal(["on keys 67 90", "off keys 67"], _sink.Calls);

        _sink.Calls.Clear();
        await audition.HearNoteAsync(_track, new MidiNote(new NoteId(3), 60, 0, 1, 0));
        Assert.Empty(_sink.Calls);
    }

    private sealed class RecordingSink : IAuditionSink
    {
        public string? Reason { get; set; } = "no engine";

        public List<string> Calls { get; } = [];

        public string? Unavailable => Reason;

        public Task NoteOnAsync(TrackId track, int pitch, int velocity)
        {
            Calls.Add($"on {track.Value} {pitch} {velocity}");
            return Task.CompletedTask;
        }

        public Task NoteOffAsync(TrackId track, int pitch)
        {
            Calls.Add($"off {track.Value} {pitch}");
            return Task.CompletedTask;
        }
    }
}
