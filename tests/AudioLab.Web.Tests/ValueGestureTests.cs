using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Where a drag or a key leaves a control's travel: absolute, bounded, and silent until the caller sends it.</summary>
public sealed class ValueGestureTests
{
    /// <summary>A press that never travels is not a drag, and a release where it began sends nothing.</summary>
    [Fact]
    public void APressThatNeverTravelsSendsNothing()
    {
        ValueGesture gesture = new(0.5, 100);

        gesture.Move(102, fine: false);

        Assert.False(gesture.Active);
        Assert.Equal(0.5, gesture.Position);
        Assert.Null(gesture.Commit());
    }

    /// <summary>Up is more, and the full travel is the same height whatever the start.</summary>
    [Fact]
    public void UpIsMoreAndAFullTravelCoversTheWholeRange()
    {
        ValueGesture gesture = new(0, 400);

        gesture.Move(400 - ValueGesture.TravelPixels, fine: false);

        Assert.Equal(1, gesture.Position, 9);
        Assert.Equal(1, gesture.Commit());
    }

    /// <summary>A drag past an end is held there, and the way back is measured from the end, not from the press.</summary>
    [Fact]
    public void ADragPastAnEndIsHeldThere()
    {
        ValueGesture gesture = new(0.9, 500);

        gesture.Move(500 - ValueGesture.TravelPixels, fine: false);
        Assert.Equal(1, gesture.Position);
        gesture.Move(500, fine: false);

        Assert.Equal(0, gesture.Position, 9);
    }

    /// <summary>A fine movement counts a tenth, and switching to it part way leaves the place where it was.</summary>
    [Fact]
    public void AFineMovementCountsATenthWithoutAJump()
    {
        ValueGesture gesture = new(0.5, 300);

        gesture.Move(300 - 16, fine: false);
        double before = gesture.Position;
        gesture.Move(300 - 16 - 16, fine: true);

        Assert.Equal(0.5 + (16 / ValueGesture.TravelPixels), before, 9);
        Assert.Equal(before + (16 / ValueGesture.TravelPixels * ValueGesture.FineFactor), gesture.Position, 9);
    }

    /// <summary>A drag that ends where it began sends nothing, so undo is left as it was.</summary>
    [Fact]
    public void ADragThatEndsWhereItBeganSendsNothing()
    {
        ValueGesture gesture = new(0.5, 300);

        gesture.Move(250, fine: false);
        gesture.Move(300, fine: false);

        Assert.True(gesture.Active);
        Assert.Null(gesture.Commit());
    }

    /// <summary>Keys step by a hundredth, a tenth of that with Shift, a tenth with the page keys, and go to the ends with Home and End.</summary>
    [Fact]
    public void KeysStepAndGoToTheEnds()
    {
        Assert.Equal(0.51, ValueGesture.Step("ArrowUp", false, 0.5)!.Value, 9);
        Assert.Equal(0.49, ValueGesture.Step("ArrowLeft", false, 0.5)!.Value, 9);
        Assert.Equal(0.501, ValueGesture.Step("ArrowRight", true, 0.5)!.Value, 9);
        Assert.Equal(0.6, ValueGesture.Step("PageUp", false, 0.5)!.Value, 9);
        Assert.Equal(0.4, ValueGesture.Step("PageDown", false, 0.5)!.Value, 9);
        Assert.Equal(0, ValueGesture.Step("Home", false, 0.5));
        Assert.Equal(1, ValueGesture.Step("End", false, 0.5));
        Assert.Null(ValueGesture.Step("a", false, 0.5));
    }

    /// <summary>A key at an end stays there.</summary>
    [Fact]
    public void AKeyAtAnEndStaysThere()
    {
        Assert.Equal(1, ValueGesture.Step("ArrowUp", false, 1));
        Assert.Equal(0, ValueGesture.Step("PageDown", false, 0.05));
    }
}
