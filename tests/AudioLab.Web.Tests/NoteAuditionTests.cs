using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>What the roll asks the sound engine for, through the seam: one note at a time, none left sounding, and nothing when it cannot.</summary>
public sealed class NoteAuditionTests
{
    private static readonly TrackId _track = TrackId.From("keys");

    private readonly RecordingSink _sink = new();

    private readonly ManualClock _clock = new();

    // A timer of the clock fires its continuation on the pool, so a test waits for the call it expects and does not count on a yield.
    private async Task SettledAsync(int calls)
    {
        for (int tries = 0; tries < 400 && _sink.Calls.Count < calls; tries++)
        {
            await Task.Delay(5);
        }
    }

    private static MidiNote Note(int pitch, double duration, int velocity = 90) => new(new NoteId(pitch), pitch, 0, duration, velocity);

    /// <summary>With a sink that cannot play, the roll sends it nothing at all and reports the sink's reason.</summary>
    [Fact]
    public async Task WhenTheSinkCannotPlayNothingIsSentAndItsReasonIsReported()
    {
        _sink.Reason = "no engine";
        NoteAudition audition = new(_sink, _clock);

        await audition.PressKeyAsync(_track, 60, 100);
        await audition.HearNoteAsync(_track, Note(60, 1));
        await audition.StopAsync();

        Assert.False(audition.Available);
        Assert.Equal("no engine", audition.Unavailable);
        Assert.Empty(_sink.Calls);
    }

    /// <summary>The sink the page has says what is missing: a single note outside a clip, not that the engine is absent.</summary>
    [Fact]
    public void ThePageSinkSaysWhatIsMissing()
    {
        UnavailableAudition none = new();

        Assert.Contains("a single note outside one", none.Unavailable, StringComparison.Ordinal);
        Assert.DoesNotContain("not running", none.Unavailable, StringComparison.Ordinal);
    }

    /// <summary>A key is on while it is held and off when it is let go, with the pitch and velocity kept in range.</summary>
    [Fact]
    public async Task AKeyIsOnWhileItIsHeldAndOffWhenLetGo()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.PressKeyAsync(_track, 200, 0);
        Assert.True(audition.Sounding);
        await audition.StopAsync();
        await audition.StopAsync();

        Assert.False(audition.Sounding);
        Assert.Equal(["on keys 127 1", "off keys 127"], _sink.Calls);
    }

    /// <summary>A second press lets the first go before it begins, so only one note sounds and none is left on.</summary>
    [Fact]
    public async Task ASecondPressLetsTheFirstGoFirst()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.PressKeyAsync(_track, 60, 100);
        await audition.PressKeyAsync(_track, 64, 100);
        await audition.StopAsync();

        Assert.Equal(["on keys 60 100", "off keys 60", "on keys 64 100", "off keys 64"], _sink.Calls);
    }

    /// <summary>A note that is selected is heard for its own length, never longer than the cap, and a note of velocity 0 is silent.</summary>
    [Fact]
    public async Task ASelectedNoteIsHeardForItsOwnLengthAtMostTheCap()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.HearNoteAsync(_track, Note(64, 0.5));
        Assert.Equal(["on keys 64 90"], _sink.Calls);
        _clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Equal(["on keys 64 90"], _sink.Calls);
        _clock.Advance(TimeSpan.FromMilliseconds(2));
        await SettledAsync(2);
        Assert.Equal(["on keys 64 90", "off keys 64"], _sink.Calls);

        _sink.Calls.Clear();
        await audition.HearNoteAsync(_track, Note(67, 30));
        _clock.Advance(TimeSpan.FromMilliseconds(NoteAudition.MaxHoldMilliseconds - 1));
        Assert.Equal(["on keys 67 90"], _sink.Calls);
        _clock.Advance(TimeSpan.FromMilliseconds(2));
        await SettledAsync(2);
        Assert.Equal(["on keys 67 90", "off keys 67"], _sink.Calls);

        _sink.Calls.Clear();
        await audition.HearNoteAsync(_track, Note(60, 1, 0));
        Assert.Empty(_sink.Calls);
    }

    /// <summary>A later note is not cut by the timer of an earlier one, even on the same pitch.</summary>
    [Fact]
    public async Task AnEarlierTimerDoesNotCutALaterNote()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.HearNoteAsync(_track, Note(64, 0.5));
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        await audition.HearNoteAsync(_track, Note(64, 0.5));
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        await Task.Delay(50);

        Assert.Equal(["on keys 64 90", "off keys 64", "on keys 64 90"], _sink.Calls);
        _clock.Advance(TimeSpan.FromMilliseconds(250));
        await SettledAsync(4);
        Assert.Equal(["on keys 64 90", "off keys 64", "on keys 64 90", "off keys 64"], _sink.Calls);
    }

    /// <summary>A key held when the note of a selection begins is let go, and a stop lets go whatever is sounding.</summary>
    [Fact]
    public async Task AStopLetsGoWhateverIsSoundingAndANoteHeardAfterAKeyLetsTheKeyGo()
    {
        _sink.Reason = null;
        NoteAudition audition = new(_sink, _clock);

        await audition.PressKeyAsync(_track, 60, 100);
        await audition.HearNoteAsync(_track, Note(62, 0.5));
        await audition.StopAsync();
        _clock.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(["on keys 60 100", "off keys 60", "on keys 62 90", "off keys 62"], _sink.Calls);
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
