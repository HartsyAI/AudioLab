using AudioLab.Web.Media;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the places for uploads: their order, their number changing, and a wait that is cancelled.</summary>
public sealed class UploadSlotsTests
{
    /// <summary>A place is given at once while there is one, and the waiting are served in the order they asked.</summary>
    [Fact]
    public async Task PlacesAreGivenInTheOrderOfAskingWhenOnesIsGivenBack()
    {
        UploadSlots slots = new(1);
        await slots.AcquireAsync(CancellationToken.None);
        Task first = slots.AcquireAsync(CancellationToken.None);
        Task second = slots.AcquireAsync(CancellationToken.None);

        Assert.False(first.IsCompleted);
        slots.Release();
        await first;
        Assert.False(second.IsCompleted);
        slots.Release();
        await second;
    }

    /// <summary>A number that rises lets waiting uploads start; one that falls lets running ones finish and holds new ones back.</summary>
    [Fact]
    public async Task TheNumberCanChangeWhileUploadsWait()
    {
        UploadSlots slots = new(1);
        await slots.AcquireAsync(CancellationToken.None);
        Task waiting = slots.AcquireAsync(CancellationToken.None);

        slots.SetLimit(2);
        await waiting;

        slots.SetLimit(1);
        Task held = slots.AcquireAsync(CancellationToken.None);
        slots.Release();
        Assert.False(held.IsCompleted);
        slots.Release();
        await held;
        Assert.Equal(1, slots.Limit);
    }

    /// <summary>A wait that is cancelled takes no place, and the next in line gets the one that is given back.</summary>
    [Fact]
    public async Task ACancelledWaitTakesNoPlace()
    {
        UploadSlots slots = new(1);
        await slots.AcquireAsync(CancellationToken.None);
        using CancellationTokenSource cancel = new();
        Task cancelled = slots.AcquireAsync(cancel.Token);
        Task next = slots.AcquireAsync(CancellationToken.None);

        await cancel.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => cancelled);
        slots.Release();

        await next;
    }
}
