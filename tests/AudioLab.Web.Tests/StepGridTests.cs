using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The step grid as it is rendered: what a pointer does to a lane's steps, how often the row renders, and how a lost hub or a dispose
/// leaves the drag. The pointer's calls go to the rendered grid's own methods, which are the ones its handlers call.</summary>
public sealed class StepGridTests
{
    private static readonly BeatLaneId _kick = BeatLaneId.From("bln_kick");

    private static BeatLane Kick() => new() { Id = _kick, Name = "Kick", Gain = 1, Steps = [.. Enumerable.Repeat(false, 16)] };

    private static PointerEventArgs Pointer(string type = "mouse") => new() { Button = 0, PointerType = type };

    /// <summary>What the grid sent, and the cell a touch is over.</summary>
    private sealed class Sends
    {
        public List<(int[] Steps, bool On)> Batches { get; } = [];

        public Func<int[], Task<EditResult>> Reply { get; set; } = _ => Task.FromResult(EditResult.Applied(true, []));

        public FakeMetrics Metrics { get; } = new();

        public Task<EditResult> Send(IReadOnlyList<int> steps, bool on)
        {
            int[] batch = [.. steps];
            Batches.Add((batch, on));
            return Reply(batch);
        }

        public IEnumerable<int> Painted => Batches.SelectMany(batch => batch.Steps);
    }

    private sealed class FakeMetrics : IElementMetrics
    {
        public string? Under { get; set; }

        public (double Left, double Top)? Origin(string elementId) => null;

        public bool IsOver(string elementId, double clientX, double clientY) => elementId == Under;
    }

    private static Task<RenderedMarkup> StartAsync(Sends sends, EventCallback<StepStroke> ended = default)
    {
        int gestures = 0;
        return RenderedMarkup.StartAsync<StepGrid>(new Dictionary<string, object?>
        {
            ["Lane"] = Kick(),
            ["Editable"] = true,
            ["Cursor"] = 0,
            ["StartStroke"] = new Func<bool, StepStroke>(on => new StepStroke(on, $"g{++gestures}", sends.Send)),
            ["EndStroke"] = ended,
        }, services => services.AddSingleton<IElementMetrics>(sends.Metrics));
    }

    /// <summary>A move over steps the drag has already painted renders nothing; a move that paints renders once.</summary>
    [Fact]
    public async Task APointerMoveRendersOnlyWhenItPaints()
    {
        Sends sends = new();
        RenderedMarkup markup = await StartAsync(sends);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RunAsync(() => grid.PressAsync(0));
        await markup.RunAsync(() => { });
        int afterPress = markup.Batches;
        await markup.RunAsync(() => grid.Enter(0));
        await markup.RunAsync(() => grid.Enter(0));
        Assert.Equal(afterPress, markup.Batches);

        await markup.RunAsync(() => grid.Enter(1));
        Assert.Equal(afterPress + 1, markup.Batches);
        Assert.Equal([0, 1], sends.Painted);
    }

    /// <summary>A touch keeps its press on the cell it began on, so the cell under the finger is the one that is painted.</summary>
    [Fact]
    public async Task ATouchDragPaintsTheCellsUnderTheFinger()
    {
        Sends sends = new();
        RenderedMarkup markup = await StartAsync(sends);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RunAsync(() => grid.PressAsync(2));
        sends.Metrics.Under = BeatLaneText.CellId(_kick, 3);
        await markup.RunAsync(() => grid.TouchMove(Pointer("touch")));
        sends.Metrics.Under = BeatLaneText.CellId(_kick, 4);
        await markup.RunAsync(() => grid.TouchMove(Pointer("touch")));
        await markup.RunAsync(() => grid.ReleaseAsync());

        Assert.Equal([2, 3, 4], sends.Painted);
        Assert.All(sends.Batches, batch => Assert.True(batch.On));
    }

    /// <summary>A press made while the last drag's commands are still unanswered starts a drag of its own, so both are sent.</summary>
    [Fact]
    public async Task ASecondPressUnderLatencyStartsADragOfItsOwn()
    {
        TaskCompletionSource<EditResult> reply = new();
        Sends sends = new() { Reply = _ => reply.Task };
        RenderedMarkup markup = await StartAsync(sends);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RunAsync(() => grid.PressAsync(1));
        Task released = markup.RunAsync(() => grid.ReleaseAsync());
        await markup.RunAsync(() => grid.PressAsync(5));

        Assert.Equal([1, 5], sends.Painted);

        reply.SetResult(EditResult.Applied(true, []));
        await released;
    }

    /// <summary>A hub lost in the middle of a drag leaves the grid out of the drag, so the next press works.</summary>
    [Fact]
    public async Task ALostHubDuringADragDoesNotBlockTheNextPress()
    {
        Sends sends = new();
        EventCallback<StepStroke> lost = new(null, new Func<StepStroke, Task>(_ => throw new InvalidOperationException("The hub is gone.")));
        RenderedMarkup markup = await StartAsync(sends, lost);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RunAsync(() => grid.PressAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => markup.RunAsync(() => grid.ReleaseAsync()));

        await markup.RunAsync(() => grid.PressAsync(7));

        Assert.Contains(7, sends.Painted);
    }

    /// <summary>When the grid goes while a drag is open, the drag ends, so its gesture is closed.</summary>
    [Fact]
    public async Task DisposingTheGridEndsItsDrag()
    {
        Sends sends = new();
        List<StepStroke> ended = [];
        EventCallback<StepStroke> hear = new(null, new Func<StepStroke, Task>(stroke =>
        {
            ended.Add(stroke);
            return Task.CompletedTask;
        }));
        RenderedMarkup markup = await StartAsync(sends, hear);
        await markup.RunAsync(() => markup.Instance<StepGrid>().PressAsync(0));

        markup.TakeDown();

        Assert.Single(ended);
        Assert.True(ended[0].Ended);
    }
}
