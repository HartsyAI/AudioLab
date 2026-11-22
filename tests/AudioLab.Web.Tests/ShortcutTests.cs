using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Tests;

public sealed class ShortcutTests
{
    [Fact]
    public void NoTwoCommandsShareAnIdOrAChordOnTheSameScreen()
    {
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(shortcut => shortcut.Id).Distinct().Count());
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(shortcut => (shortcut.Scope, shortcut.Display)).Distinct().Count());
    }

    [Fact]
    public void AChordMatchesOnlyItsOwnModifiers()
    {
        Shortcut undo = new("edit.undo", "Undo", "z", "test", Ctrl: true);

        Assert.Equal("Ctrl+Z", undo.Display);
        Assert.True(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true }));
        Assert.True(undo.Matches(new KeyboardEventArgs { Key = "Z", MetaKey = true }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z" }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true, ShiftKey = true }));
        Assert.False(undo.Matches(new KeyboardEventArgs { Key = "z", CtrlKey = true, AltKey = true }));
    }
}
