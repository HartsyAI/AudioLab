using AudioLab.Web.Interop;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The drum lane's keys must not be keys the session already answers, since the session listener would take them too.</summary>
public sealed class DrumShortcutTests
{
    /// <summary>No lane key is also a session key, so a key in a lane cannot start a track action as well.</summary>
    [Fact]
    public void NoLaneKeyIsASessionKey()
    {
        foreach (Shortcut lane in Shortcuts.DrumLane)
        {
            Assert.DoesNotContain(
                Shortcuts.All,
                other => other.Scope == Shortcuts.Session
                    && other.Key == lane.Key
                    && other.Ctrl == lane.Ctrl
                    && other.Shift == lane.Shift
                    && other.Alt == lane.Alt);
        }
    }

    /// <summary>A step key that is also a session key is one the grid claims, so the transport does not act on it while the grid has focus.</summary>
    [Fact]
    public void EverySessionKeyAGridUsesIsClaimedByTheGrid()
    {
        foreach (Shortcut step in Shortcuts.DrumStep)
        {
            KeyChord chord = new(step.Key, step.Ctrl, step.Shift, step.Alt);
            if (Shortcuts.All.Any(other => other.Scope == Shortcuts.Session && other.Matches(chord)))
            {
                Assert.Contains(step, Shortcuts.DrumStepClaims);
            }
        }
    }

    /// <summary>The step keys are drums keys, so they are not the session's and not the lanes'.</summary>
    [Fact]
    public void TheStepKeysBelongToTheDrumsScope()
    {
        Assert.All(Shortcuts.DrumStep, step => Assert.Equal(Shortcuts.Drums, step.Scope));
    }

    /// <summary>F2 stays the track rename; a lane renames on its own key.</summary>
    [Fact]
    public void F2IsNotALaneKey()
    {
        Assert.Equal("F2", Shortcuts.RenameTrack.Key);
        Assert.DoesNotContain(Shortcuts.DrumLane, lane => lane.Key == "F2");
    }
}
