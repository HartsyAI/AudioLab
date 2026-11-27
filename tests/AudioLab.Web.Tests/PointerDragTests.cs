using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>A pointer follows one drag at a time: a new drag replaces the one in progress, and the replaced drag is cancelled rather than left
/// waiting for a release that will not come (D86).</summary>
public sealed class PointerDragTests
{
    /// <summary>A second drag returns the cancel of the first, so the owner of the first is told it has ended.</summary>
    [Fact]
    public void ANewDragReturnsTheCancelOfTheOneItReplaces()
    {
        PointerDrag drag = new();
        int cancelled = 0;
        Assert.Null(drag.Begin((_, _) => { }, (_, _) => { }, () => cancelled++));

        Action? displaced = drag.Begin((_, _) => { }, (_, _) => { }, () => { });
        displaced?.Invoke();

        Assert.NotNull(displaced);
        Assert.Equal(1, cancelled);
    }

    /// <summary>After a stop nothing is in progress, so the next drag replaces nothing.</summary>
    [Fact]
    public void AClearedDragReplacesNothing()
    {
        PointerDrag drag = new();
        drag.Begin((_, _) => { }, (_, _) => { }, () => { });
        drag.Clear();

        Assert.Null(drag.Cancel);
        Assert.Null(drag.Begin((_, _) => { }, (_, _) => { }, () => { }));
    }
}
