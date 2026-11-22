using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>The toast service: what shows, what goes by itself, and what Undo does.</summary>
public sealed class ToastServiceTests
{
    private readonly ManualClock _clock = new();

    private readonly ToastService _toasts;

    /// <summary>Starts the service on a clock the tests move.</summary>
    public ToastServiceTests()
    {
        _toasts = new ToastService(_clock);
    }

    /// <summary>A toast shows until its lifetime ends.</summary>
    [Fact]
    public void AToastShowsUntilItsLifetimeEnds()
    {
        string id = _toasts.Show(ToastKind.Success, "Saved");

        Assert.Equal([id], _toasts.Items.Select(toast => toast.Id));
        _clock.Advance(ToastService.Lifetime - TimeSpan.FromMilliseconds(1));
        Assert.Single(_toasts.Items);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Empty(_toasts.Items);
    }

    /// <summary>An error stays until it is dismissed.</summary>
    [Fact]
    public void AnErrorStaysUntilItIsDismissed()
    {
        string id = _toasts.Show(ToastKind.Error, "The server refused that");

        _clock.Advance(TimeSpan.FromHours(1));
        Assert.Single(_toasts.Items);
        _toasts.Dismiss(id);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>Undo runs once and removes the toast.</summary>
    [Fact]
    public async Task UndoRunsOnceAndRemovesTheToast()
    {
        int undone = 0;
        string id = _toasts.Show(ToastKind.Info, "Track deleted", () =>
        {
            undone++;
            return Task.CompletedTask;
        });

        await _toasts.UndoAsync(id);
        await _toasts.UndoAsync(id);

        Assert.Equal(1, undone);
        Assert.Empty(_toasts.Items);
    }

    /// <summary>Every change is announced.</summary>
    [Fact]
    public void EveryChangeIsAnnounced()
    {
        int changes = 0;
        _toasts.Changed += () => changes++;

        string id = _toasts.Show(ToastKind.Warning, "Disk is nearly full");
        _toasts.Dismiss(id);
        _toasts.Dismiss(id);

        Assert.Equal(2, changes);
    }

    /// <summary>A blank message is refused.</summary>
    [Fact]
    public void ABlankMessageIsRefused()
    {
        Assert.Throws<ArgumentException>(() => _toasts.Show(ToastKind.Info, "  "));
    }
}
