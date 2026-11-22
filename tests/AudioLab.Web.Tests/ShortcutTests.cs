using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Tests;

/// <summary>Holds the shortcut tests.</summary>
public sealed class ShortcutTests
{
    /// <summary>No two commands share an id or a chord.</summary>
    [Fact]
    public void NoTwoCommandsShareAnIdOrAChord()
    {
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(shortcut => shortcut.Id).Distinct().Count());
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(shortcut => shortcut.Display).Distinct().Count());
    }

    /// <summary>A chord matches only its own modifiers.</summary>
    [Fact]
    public void AChordMatchesOnlyItsOwnModifiers()
    {
        Shortcut undo = new("edit.undo", "Undo", "z", Ctrl: true);

        Assert.Equal("Ctrl+Z", undo.Display);
        Assert.True(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true }));
        Assert.True(undo.Matches(new KeyboardEventArgs { Key = "Z", MetaKey = true }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z" }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true, ShiftKey = true }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true, AltKey = true }));
    }
}
