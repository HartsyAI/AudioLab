using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The step grid as it is rendered: the pointer events reach its handlers, a move renders only when it paints, and a lost hub, a cancel
/// or a dispose leaves no painted step behind.</summary>
public sealed class StepGridTests
{
    private static readonly BeatLaneId _kick = BeatLaneId.From("bln_kick");

    private static BeatLane Kick() => new() { Id = _kick, Name = "Kick", Gain = 1, Steps = [.. Enumerable.Repeat(false, 16)] };

    private static string Cell(int step) => BeatLaneText.CellId(_kick, step);

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

        await markup.RaiseOnAsync(Cell(0), "onpointerdown", Pointer());
        await markup.RunAsync(() => { });
        int afterPress = markup.Batches;

        await markup.RaiseOnAsync(Cell(0), "onpointerenter", Pointer());
        await markup.RaiseOnAsync(Cell(0), "onpointerenter", Pointer());
        Assert.Equal(afterPress, markup.Batches);

        await markup.RaiseOnAsync(Cell(1), "onpointerenter", Pointer());
        Assert.Equal(afterPress + 1, markup.Batches);
        Assert.Equal([0, 1], sends.Painted);
    }

    /// <summary>A touch keeps its press on the cell it began on, so the cell under the finger is the one that is painted.</summary>
    [Fact]
    public async Task ATouchDragPaintsTheCellsUnderTheFinger()
    {
        Sends sends = new();
        RenderedMarkup markup = await StartAsync(sends);

        await markup.RaiseOnAsync(Cell(2), "onpointerdown", Pointer("touch"));
        sends.Metrics.Under = Cell(3);
        await markup.RaiseOnAsync(null, "onpointermove", Pointer("touch"));
        sends.Metrics.Under = Cell(4);
        await markup.RaiseOnAsync(null, "onpointermove", Pointer("touch"));
        await markup.RaiseOnAsync(null, "onpointerup", Pointer("touch"));

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

        await markup.RaiseOnAsync(Cell(1), "onpointerdown", Pointer());
        Task released = markup.RaiseOnAsync(null, "onpointerup", Pointer());
        await markup.RaiseOnAsync(Cell(5), "onpointerdown", Pointer());

        Assert.Equal([1, 5], sends.Painted);

        reply.SetResult(EditResult.Applied(true, []));
        await released;
    }

    /// <summary>A hub lost in the middle of a drag leaves no painted step behind, and the next press works.</summary>
    [Fact]
    public async Task ALostHubDuringADragLeavesNoPaintBehind()
    {
        Sends sends = new();
        EventCallback<StepStroke> lost = new(null, new Func<StepStroke, Task>(_ => throw new InvalidOperationException("The hub is gone.")));
        RenderedMarkup markup = await StartAsync(sends, lost);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RaiseOnAsync(Cell(1), "onpointerdown", Pointer());
        await Assert.ThrowsAsync<InvalidOperationException>(() => markup.RaiseOnAsync(null, "onpointerup", Pointer()));

        Assert.False(grid.ShownOn(1));

        await markup.RaiseOnAsync(Cell(7), "onpointerdown", Pointer());
        Assert.Contains(7, sends.Painted);
    }

    /// <summary>The browser cancelling the pointer ends the drag and withdraws the steps not yet sent; a press after that starts again.</summary>
    [Fact]
    public async Task APointerCancelEndsTheDragAndWithdrawsWhatWasNotSent()
    {
        TaskCompletionSource<EditResult> reply = new();
        Sends sends = new() { Reply = _ => reply.Task };
        RenderedMarkup markup = await StartAsync(sends);
        StepGrid grid = markup.Instance<StepGrid>();

        await markup.RaiseOnAsync(Cell(0), "onpointerdown", Pointer("touch"));
        await markup.RaiseOnAsync(Cell(1), "onpointerenter", Pointer("touch"));
        Task cancelled = markup.RaiseOnAsync(null, "onpointercancel", Pointer("touch"));

        Assert.False(grid.ShownOn(1));
        Assert.Equal([0], sends.Painted);

        reply.SetResult(EditResult.Applied(true, []));
        await cancelled;

        Assert.Equal([0], sends.Painted);
        await markup.RaiseOnAsync(Cell(5), "onpointerdown", Pointer());
        Assert.Contains(5, sends.Painted);
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
        await markup.RaiseOnAsync(Cell(0), "onpointerdown", Pointer());

        markup.TakeDown();

        Assert.Single(ended);
        Assert.True(ended[0].Ended);
    }
}
