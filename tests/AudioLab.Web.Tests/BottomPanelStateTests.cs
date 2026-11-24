using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Which tab of the bottom panel is open: held by the client, changed by the arrangement, and back to the mixer for a new project.</summary>
public sealed class BottomPanelStateTests
{
    /// <summary>Opening another tab raises one change, and opening the one that is open raises none.</summary>
    [Fact]
    public void OpeningAnotherTabRaisesOneChangeAndTheSameTabNone()
    {
        BottomPanelState state = new();
        int changes = 0;
        state.Changed += () => changes++;

        state.Show(BottomPanelState.PianoRoll);
        state.Show(BottomPanelState.PianoRoll);

        Assert.Equal(BottomPanelState.PianoRoll, state.Active);
        Assert.Equal(1, changes);
    }

    /// <summary>A project that is opened starts on the mixer.</summary>
    [Fact]
    public void AProjectThatIsOpenedStartsOnTheMixer()
    {
        BottomPanelState state = new();
        state.Show(BottomPanelState.PianoRoll);

        state.Reset();

        Assert.Equal(BottomPanelState.Mixer, state.Active);
    }
}
