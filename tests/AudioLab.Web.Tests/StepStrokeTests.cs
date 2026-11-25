using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>A paint drag over a lane's steps: one absolute state, one command at a time, one gesture, and a refusal that stops it.</summary>
public sealed class StepStrokeTests
{
    private readonly List<(IReadOnlyList<int> Steps, bool On)> _sent = [];

    /// <summary>The steps painted while a batch is on its way go out as the next batch, and no second batch starts before the first is answered.</summary>
    [Fact]
    public async Task EachBatchIsOneCommandAndOneBatchIsOnItsWayAtATime()
    {
        TaskCompletionSource<EditResult> first = new();
        StepStroke stroke = new(true, "g1", (steps, on) =>
        {
            _sent.Add((steps, on));
            return _sent.Count == 1 ? first.Task : Task.FromResult(EditResult.Applied(true, []));
        });

        stroke.Paint(1, false);
        stroke.Paint(2, false);
        stroke.Paint(3, false);
        Assert.Single(_sent);

        first.SetResult(EditResult.Applied(true, []));
        await stroke.EndAsync();

        Assert.Equal(2, _sent.Count);
        Assert.Equal(new[] { 1 }, _sent[0].Steps);
        Assert.Equal(new[] { 2, 3 }, _sent[1].Steps);
        Assert.All(_sent, sent => Assert.True(sent.On));
    }

    /// <summary>A step already in the drag's state is not sent, so a drag over a lit step changes nothing there.</summary>
    [Fact]
    public async Task AStepAlreadyInTheStrokesStateSendsNothing()
    {
        StepStroke stroke = new(true, "g1", (steps, on) =>
        {
            _sent.Add((steps, on));
            return Task.FromResult(EditResult.Applied(true, []));
        });

        Assert.False(stroke.Paint(4, true));
        await stroke.EndAsync();

        Assert.Empty(_sent);
    }

    /// <summary>A refused batch stops the drag and takes its painted steps back; the words are kept for the status line.</summary>
    [Fact]
    public async Task ARefusalStopsTheDragAndTakesBackWhatWasPainted()
    {
        int changes = 0;
        StepStroke stroke = new(true, "g1", (steps, on) =>
        {
            _sent.Add((steps, on));
            return Task.FromResult(EditResult.Refused("There is no drum lane with the id bln_gone."));
        });
        stroke.Changed += () => changes++;

        stroke.Paint(1, false);
        stroke.Paint(2, false);
        await stroke.EndAsync();

        Assert.Single(_sent);
        Assert.Equal("There is no drum lane with the id bln_gone.", stroke.Refusal);
        Assert.Empty(stroke.Painted);
        Assert.Equal(1, changes);
    }

    /// <summary>A step painted and not yet answered shows the drag's state, so the pointer sees what it did.</summary>
    [Fact]
    public void APaintedStepShowsTheStrokesStateUntilItIsAnswered()
    {
        TaskCompletionSource<EditResult> reply = new();
        StepStroke stroke = new(true, "g1", (_, _) => reply.Task);

        stroke.Paint(5, false);

        Assert.True(stroke.Painted[5]);
    }

    /// <summary>Once the drag has ended, the pointer paints nothing more.</summary>
    [Fact]
    public async Task AFinishedDragPaintsNothingMore()
    {
        StepStroke stroke = new(false, "g1", (steps, on) =>
        {
            _sent.Add((steps, on));
            return Task.FromResult(EditResult.Applied(true, []));
        });
        await stroke.EndAsync();

        Assert.False(stroke.Paint(2, true));
        Assert.Empty(_sent);
    }
}
