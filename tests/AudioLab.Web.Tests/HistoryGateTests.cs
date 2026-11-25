using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the history gate tests: undo and redo wait for any drag that is under way, a word drag included.</summary>
public sealed class HistoryGateTests
{
    /// <summary>A word drag makes undo and redo wait, as a clip drag does.</summary>
    [Fact]
    public void AWordDragMakesHistoryWait()
    {
        Assert.True(HistoryGate.Waits(clipDragging: false, rollBusy: false, wordDragging: true, paintDragging: false));
        Assert.True(HistoryGate.Waits(clipDragging: true, rollBusy: false, wordDragging: false, paintDragging: false));
        Assert.True(HistoryGate.Waits(clipDragging: false, rollBusy: true, wordDragging: false, paintDragging: false));
    }

    /// <summary>A paint drag over the drum steps makes undo and redo wait, as the other drags do.</summary>
    [Fact]
    public void APaintDragMakesHistoryWait()
    {
        Assert.True(HistoryGate.Waits(clipDragging: false, rollBusy: false, wordDragging: false, paintDragging: true));
    }

    /// <summary>With no drag under way, undo and redo go through.</summary>
    [Fact]
    public void WithNoDragHistoryGoesThrough()
    {
        Assert.False(HistoryGate.Waits(clipDragging: false, rollBusy: false, wordDragging: false, paintDragging: false));
    }
}
