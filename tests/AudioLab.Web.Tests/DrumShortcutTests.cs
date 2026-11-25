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

    /// <summary>F2 stays the track rename; a lane renames on its own key.</summary>
    [Fact]
    public void F2IsNotALaneKey()
    {
        Assert.Equal("F2", Shortcuts.RenameTrack.Key);
        Assert.DoesNotContain(Shortcuts.DrumLane, lane => lane.Key == "F2");
    }
}
