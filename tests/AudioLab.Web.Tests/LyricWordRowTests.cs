using AudioLab.Core.Model;
using AudioLab.Web.Components.Lyrics;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>Holds the word row tests: a drag or a key that goes out is the one the row meant, and a drag that outlives its word sends nothing.</summary>
public sealed class LyricWordRowTests
{
    private static readonly ClipLyrics _lyrics = new()
    {
        Text = "one two three",
        Words =
        [
            new LyricWord("one", 1, 2),
            new LyricWord("two", 2.5, 3),
            new LyricWord("three", 4, 5),
        ],
    };

    // The same place in the list, now another word: the lyrics changed under the drag.
    private static readonly ClipLyrics _changed = new()
    {
        Text = "one two four",
        Words =
        [
            new LyricWord("one", 1, 2),
            new LyricWord("two", 2.5, 3),
            new LyricWord("four", 4, 4.5),
        ],
    };

    /// <summary>A drag of the end that ends on the same word sends its edit once.</summary>
    [Fact]
    public async Task ADragThatEndsOnTheSameWordSendsItsEdit()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        await harness.Renderer.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100 });
        await harness.Renderer.RunAsync(() => harness.Pointer.Move(110, 0));
        await harness.Renderer.RunAsync(() => harness.Pointer.Release(110, 0));

        LyricWordEdit edit = Assert.Single(harness.Commits);
        Assert.Equal(2, edit.Word);
        Assert.Null(edit.Start);
        Assert.True(edit.End > 5);
        Assert.False(harness.Drags.Busy);
    }

    /// <summary>A drag whose word has changed under it sends nothing, and says so.</summary>
    [Fact]
    public async Task ADragThatEndsAfterTheWordChangedSendsNothing()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        await harness.Renderer.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100 });
        await harness.Renderer.RunAsync(() => harness.Pointer.Move(110, 0));
        await harness.Renderer.RenderAgainAsync(harness.ParametersFor(_changed));
        await harness.Renderer.RunAsync(() => harness.Pointer.Release(110, 0));

        Assert.Empty(harness.Commits);
        Assert.Contains(harness.Said, text => text.StartsWith("The word changed while it was being dragged", StringComparison.Ordinal));
        Assert.False(harness.Drags.Busy);
    }

    /// <summary>A word drag marks the drag as under way, so undo and redo wait, and the mark goes when the drag ends.</summary>
    [Fact]
    public async Task AWordDragMarksTheDragAsUnderWayUntilItEnds()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        Assert.False(harness.Drags.Busy);
        await harness.Renderer.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100 });
        Assert.True(harness.Drags.Busy);
        await harness.Renderer.RunAsync(() => harness.Pointer.Release(100, 0));
        Assert.False(harness.Drags.Busy);
    }

    /// <summary>Taking the row down during a drag stops the pointer and ends the drag, so nothing is left running.</summary>
    [Fact]
    public async Task TakingTheRowDownDuringADragStopsIt()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        await harness.Renderer.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100 });
        await harness.Renderer.RunAsync(() => harness.Pointer.Move(110, 0));
        harness.Renderer.TakeDown();

        Assert.True(harness.Pointer.Stopped);
        Assert.False(harness.Drags.Busy);
    }

    /// <summary>A held move key sends nothing; the same key once sends one edit.</summary>
    [Fact]
    public async Task AHeldMoveKeySendsNothingAndOnePressSendsOneEdit()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        await harness.Renderer.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft", CtrlKey = true, Repeat = true });
        Assert.Empty(harness.Commits);

        await harness.Renderer.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft", CtrlKey = true });
        Assert.Single(harness.Commits);
    }

    /// <summary>End asks for the last word of the list and Home for the first, so the keys reach every row.</summary>
    [Fact]
    public async Task HomeAndEndAskForTheFirstAndLastWord()
    {
        using Harness harness = await Harness.StartAsync(_lyrics);
        await harness.Renderer.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "End" });
        await harness.Renderer.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "Home" });

        Assert.Equal([2, 0], harness.Steps);
    }

    private sealed class Harness : IDisposable
    {
        private readonly HttpClient _http;

        private readonly PlaybackService _playback;

        private Harness(FakePointer pointer, LyricsDrags drags, HttpClient http, PlaybackService playback)
        {
            Pointer = pointer;
            Drags = drags;
            _http = http;
            _playback = playback;
        }

        public RenderedMarkup Renderer { get; private set; } = null!;

        public FakePointer Pointer { get; }

        public LyricsDrags Drags { get; }

        public List<LyricWordEdit> Commits { get; } = [];

        public List<string> Said { get; } = [];

        public List<int> Steps { get; } = [];

        public static async Task<Harness> StartAsync(ClipLyrics lyrics)
        {
            FakePointer pointer = new();
            LyricsDrags drags = new();
            HttpClient http = new() { BaseAddress = new Uri("http://example.test/audiolab/") };
            PlaybackService playback = new(new FakeAudioHost(), http, TimeProvider.System);
            Harness made = new(pointer, drags, http, playback);
            made.Renderer = await RenderedMarkup.StartAsync<LyricWordRow>(
                made.ParametersFor(lyrics),
                services =>
                {
                    services.AddSingleton(playback);
                    services.AddScoped<LyricsCursor>();
                    services.AddSingleton<IPointerTracker>(pointer);
                    services.AddSingleton(drags);
                });
            return made;
        }

        public IDictionary<string, object?> ParametersFor(ClipLyrics lyrics) => new Dictionary<string, object?>
        {
            ["Index"] = 2,
            ["Lyrics"] = lyrics,
            ["Geometry"] = TimelineGeometry.Default,
            ["Width"] = 400.0,
            ["Editable"] = true,
            ["OnCommit"] = EventCallback.Factory.Create<LyricWordEdit>(this, edit => Commits.Add(edit)),
            ["OnSay"] = EventCallback.Factory.Create<string>(this, text => Said.Add(text)),
            ["OnFocusWord"] = EventCallback.Factory.Create<int>(this, _ => { }),
            ["OnStep"] = EventCallback.Factory.Create<int>(this, index => Steps.Add(index)),
        };

        public void Dispose()
        {
            _playback.Dispose();
            _http.Dispose();
        }
    }

    private sealed class FakePointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        public bool Shift => false;

        public bool Stopped { get; private set; }

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            _move = move;
            _end = end;
        }

        public void Stop() => Stopped = true;

        public void Move(double x, double y) => _move?.Invoke(x, y);

        public void Release(double x, double y) => _end?.Invoke(x, y);
    }
}
