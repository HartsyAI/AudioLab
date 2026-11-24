using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Media;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of a media file dragged onto the lanes and of the adds of the keyboard: the whole gesture, not its geometry.</summary>
public sealed class MediaDraggerTests
{
    private readonly ProjectDocument _song = Song.Empty().With(Song.NewTrack("t1", 64), Song.WithMidi("keys"), Song.NewTrack("t3", 64));

    private readonly FakePointer _pointer = new();

    private readonly FakeMetrics _metrics = new();

    private readonly MediaDragState _state = new();

    private readonly ToastService _toasts = new(TimeProvider.System);

    private readonly List<MediaPlacement> _added = [];

    private TaskCompletionSource? _answer;

    private static MediaSummary File() => Song.Media("med_1", "kick.wav", 2);

    private MediaDragger Dragger() => new(_pointer, _metrics, _state, new TimelineZoom(), _toasts);

    private Task Add(MediaPlacement placement)
    {
        _added.Add(placement);
        return _answer?.Task ?? Task.CompletedTask;
    }

    private void Begin(MediaDragger dragger, string? blocked = null)
    {
        dragger.Begin(File(), () => _song, Add, 50, 700, blocked);
    }

    // A client point over a row of the lanes, whose top left is at (300, 100).
    private static (double X, double Y) Over(double seconds, int row) => (300 + (seconds * 34), 100 + (row * 64) + 30);

    /// <summary>While it moves the drag is shown, over the lanes it has a mark, and nothing is sent.</summary>
    [Fact]
    public void WhileItMovesTheDragIsShownAndNothingIsSent()
    {
        MediaDragger dragger = Dragger();
        int changes = 0;
        _state.Changed += () => changes++;
        Begin(dragger);
        (double x, double y) = Over(4, 2);

        _pointer.Move(x, y);

        Assert.NotNull(_state.Current);
        Assert.NotNull(_state.Current!.Target);
        Assert.True(changes >= 2);
        Assert.Empty(_added);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>A release over an audio lane adds one clip, and the mark stays until the server has answered.</summary>
    [Fact]
    public async Task AReleaseOverAnAudioLaneAddsOneClipAndTheMarkStaysUntilTheServerAnswers()
    {
        _answer = new TaskCompletionSource();
        MediaDragger dragger = Dragger();
        Begin(dragger);
        (double x, double y) = Over(4, 2);
        _pointer.Move(x, y);

        _pointer.Release(x, y);

        MediaPlacement placement = Assert.Single(_added);
        Assert.Equal((TrackId.From("t3"), 4.0), (placement.Track, placement.Start));
        Assert.NotNull(_state.Current);
        Assert.NotNull(_state.Current!.Target);
        _answer.SetResult();
        await UntilAsync(() => _state.Current is null);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>A release over a lane the file does not fit adds nothing and raises the reason as an error toast.</summary>
    [Fact]
    public async Task AReleaseOverALaneTheFileDoesNotFitRaisesTheReason()
    {
        MediaDragger dragger = Dragger();
        Begin(dragger);
        (double x, double y) = Over(4, 1);
        _pointer.Move(x, y);

        _pointer.Release(x, y);
        await UntilAsync(() => _state.Current is null);

        Assert.Empty(_added);
        ToastItem toast = Assert.Single(_toasts.Items);
        Assert.Equal(ToastKind.Error, toast.Kind);
        Assert.Equal("\"kick.wav\" is audio and goes on an audio track; \"keys\" is an instrument track.", toast.Message);
    }

    /// <summary>A release away from the lanes, or over something that covers them, adds nothing and says nothing.</summary>
    [Fact]
    public async Task AReleaseAwayFromTheLanesOrOverAHeaderAddsNothingAndSaysNothing()
    {
        MediaDragger dragger = Dragger();
        Begin(dragger);
        (double x, double y) = Over(4, 0);
        _metrics.OverLanes = false;
        _pointer.Move(x, y);

        _pointer.Release(x, y);
        await UntilAsync(() => _state.Current is null);

        Assert.Empty(_added);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>Escape ends the drag with nothing added, and so does a panel that goes while a file is lifted.</summary>
    [Fact]
    public void EscapeEndsTheDragWithNothingAddedAndSoDoesAPanelThatGoes()
    {
        MediaDragger dragger = Dragger();
        Begin(dragger);
        (double x, double y) = Over(4, 0);
        _pointer.Move(x, y);

        _pointer.Cancel();

        Assert.Null(_state.Current);
        Assert.Empty(_added);
        Begin(dragger);
        _pointer.Move(x, y);
        dragger.Cancel();
        Assert.Null(_state.Current);
        Assert.True(_pointer.Stopped);
        Assert.Empty(_added);
    }

    /// <summary>A press that never travels is a click: it adds nothing and says nothing.</summary>
    [Fact]
    public async Task APressThatNeverTravelsIsAClick()
    {
        MediaDragger dragger = Dragger();
        Begin(dragger);

        _pointer.Release(51, 701);
        await UntilAsync(() => _state.Current is null);

        Assert.Empty(_added);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>Where the project cannot be edited the drag still follows and says why, and the release raises that.</summary>
    [Fact]
    public async Task WhereTheProjectCannotBeEditedTheReleaseRaisesTheReason()
    {
        MediaDragger dragger = Dragger();
        Begin(dragger, "Reconnecting to the server.");
        (double x, double y) = Over(4, 0);
        _pointer.Move(x, y);

        _pointer.Release(x, y);
        await UntilAsync(() => _state.Current is null);

        Assert.Empty(_added);
        Assert.Equal("kick.wav cannot be added now. Reconnecting to the server.", Assert.Single(_toasts.Items).Message);
    }

    /// <summary>A key held down, or a second Enter while the first add is on its way, adds only once.</summary>
    [Fact]
    public async Task ASecondAddWhileTheFirstIsOnItsWayAddsNothing()
    {
        MediaAdder adder = new(_toasts);
        _answer = new TaskCompletionSource();

        Task first = adder.AddAsync(File(), _song, TrackId.From("t3"), null, 1, true, null, Add);
        Task second = adder.AddAsync(File(), _song, TrackId.From("t3"), null, 1, true, null, Add);
        await second;
        _answer.SetResult();
        await first;
        await adder.AddAsync(File(), _song, TrackId.From("t3"), null, 1, true, null, Add);

        Assert.Equal(2, _added.Count);
    }

    /// <summary>An add that cannot be made says why: the project cannot be edited, or there is no audio track to go on.</summary>
    [Fact]
    public async Task AnAddThatCannotBeMadeSaysWhy()
    {
        MediaAdder adder = new(_toasts);

        await adder.AddAsync(File(), _song, null, null, 0, false, "Not connected to the server.", Add);
        await adder.AddAsync(File(), Song.Empty().With(Song.WithMidi("keys")), null, null, 0, true, null, Add);

        Assert.Empty(_added);
        Assert.Equal(
            ["kick.wav was not added. Not connected to the server.", "Add an audio track first; a file needs an audio track to go on."],
            _toasts.Items.Select(toast => toast.Message));
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Yield();
        }
        Assert.True(condition(), "The condition did not become true in time.");
    }

    private sealed class FakePointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        private Action? _cancel;

        public bool Shift => false;

        public bool Stopped { get; private set; }

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            (_move, _end, _cancel) = (move, end, cancel);
            Stopped = false;
        }

        public void Stop()
        {
            Stopped = true;
            (_move, _end, _cancel) = (null, null, null);
        }

        public void Move(double x, double y) => _move!(x, y);

        public void Release(double x, double y)
        {
            Action<double, double> end = _end!;
            Stop();
            end(x, y);
        }

        public void Cancel()
        {
            Action cancel = _cancel!;
            Stop();
            cancel();
        }
    }

    private sealed class FakeMetrics : IElementMetrics
    {
        public bool OverLanes { get; set; } = true;

        public (double Left, double Top)? Origin(string elementId) => (300, 100);

        public bool IsOver(string elementId, double clientX, double clientY) => OverLanes;
    }
}
