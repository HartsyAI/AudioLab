using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The preview path is built when a clip's notes change and not when it is only drawn again, as a lifted copy of a drag is.</summary>
public sealed class MidiPathMemoTests
{
    /// <summary>The same clip, and an equal copy of it that a new revision brings, build the path once.</summary>
    [Fact]
    public void TheSameClipAndAnEqualCopyBuildThePathOnce()
    {
        MidiClip clip = Song.Midi("m", 0, 4, new MidiNote(new NoteId(1), 60, 0, 1, 90));
        MidiPathMemo memo = new();

        Assert.True(memo.Update(clip));
        Assert.False(memo.Update(clip));
        Assert.False(memo.Update(clip with { Name = clip.Name }));
        Assert.Equal(1, memo.Builds);
        Assert.Equal("M0 50h250", memo.Path);
    }

    /// <summary>A change of the notes, or of the clip's length, builds it again.</summary>
    [Fact]
    public void AChangeOfTheNotesOrTheLengthBuildsItAgain()
    {
        MidiClip clip = Song.Midi("m", 0, 4, new MidiNote(new NoteId(1), 60, 0, 1, 90));
        MidiPathMemo memo = new();
        memo.Update(clip);

        Assert.True(memo.Update(clip with { Length = 8 }));
        Assert.True(memo.Update(clip with { Notes = [.. clip.Notes, new MidiNote(new NoteId(2), 62, 2, 1, 90)] }));
        Assert.Equal(3, memo.Builds);
    }
}
