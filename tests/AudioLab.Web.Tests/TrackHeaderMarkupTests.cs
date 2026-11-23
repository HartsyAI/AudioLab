using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>What a track header offers to the keyboard and says to assistive technology, and what the selection keeps.</summary>
public sealed class TrackHeaderMarkupTests
{
    private static Task<RenderedMarkup> HeaderAsync(bool editable, string? reason)
    {
        return RenderedMarkup.OfAsync<TrackHeader>(new Dictionary<string, object?>
        {
            ["Track"] = Song.NewTrack("t1"),
            ["Height"] = 64.0,
            ["ColorToken"] = "var(--track-1)",
            ["Editable"] = editable,
            ["Reason"] = reason,
        });
    }

    /// <summary>A header that cannot edit says why in the title of every toggle, and its R and I say what they wait for.</summary>
    [Fact]
    public async Task AHeaderThatCannotEditSaysWhyInTheTitleOfEveryToggle()
    {
        RenderedMarkup header = await HeaderAsync(false, "Reconnecting to the server.");

        string[] titles = header.Values("title");

        Assert.Equal(2, titles.Count(title => title == "Reconnecting to the server."));
        Assert.Equal(2, titles.Count(title => title.Contains("audio engine", StringComparison.Ordinal)));
    }

    /// <summary>The header is a list item, and its name is a button, so the keyboard can reach the track.</summary>
    [Fact]
    public async Task TheHeaderIsAListItemAndItsNameIsAButtonTheKeyboardCanReach()
    {
        RenderedMarkup header = await HeaderAsync(true, null);

        Assert.Contains("listitem", header.Values("role"));
        Assert.Contains(header.Values("title"), title => title.StartsWith("Select ", StringComparison.Ordinal));
        Assert.Contains("button", header.Values("type"));
    }

    /// <summary>A clip that moved to another track is still the selection, on its new track.</summary>
    [Fact]
    public void AClipThatMovedToAnotherTrackIsStillTheSelection()
    {
        SessionSelection selection = new();
        selection.SelectClip(TrackId.From("t1"), ClipId.From("a"));
        ProjectDocument after = Song.Empty().With(Song.NewTrack("t1"), Song.NewTrack("t2", 64, Song.Audio("a", 0, 2)));

        selection.Prune(after);

        Assert.Equal(TrackId.From("t2"), selection.Track);
        Assert.Equal(ClipId.From("a"), selection.Clip);
    }

    /// <summary>The command key is named as the user knows it.</summary>
    [Fact]
    public void TheCommandKeyIsNamedAsTheUserKnowsIt()
    {
        string before = Shortcut.CommandKey;
        try
        {
            Assert.Equal("Ctrl+Z", Shortcuts.Undo.Display);
            Shortcut.CommandKey = "Cmd";
            Assert.Equal("Cmd+Shift+Z", Shortcuts.Redo.Display);
        }
        finally
        {
            Shortcut.CommandKey = before;
        }
    }
}
