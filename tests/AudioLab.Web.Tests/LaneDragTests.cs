using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The drags in the velocity lane and the pedal lane.</summary>
public sealed class LaneDragTests
{
    private static readonly ProjectId _project = ProjectId.From("song");

    private readonly MidiClip _clip = Song.Midi(
        "m",
        0,
        4,
        new MidiNote(new NoteId(1), 60, 1, 0.5, 90),
        new MidiNote(new NoteId(2), 64, 2, 0.5, 40)) with
    {
        ControlChanges = [new MidiControlChange(1, 64, 127), new MidiControlChange(2, 64, 0)],
    };

    /// <summary>The height of the pointer in the lane is the velocity: the top is 127 and the bottom 1.</summary>
    [Fact]
    public void TheHeightOfThePointerIsTheVelocity()
    {
        Assert.Equal(127, VelocityDrag.At(0, 56));
        Assert.Equal(1, VelocityDrag.At(56, 56));
        Assert.Equal(64, VelocityDrag.At(28, 56));
        Assert.Equal(127, VelocityDrag.At(-30, 56));
        Assert.Equal(1, VelocityDrag.At(100, 56));
    }

    /// <summary>A drag gives every note it holds one velocity, in one command, and sends nothing when they already have it.</summary>
    [Fact]
    public void ADragGivesEveryNoteItHoldsOneVelocityInOneCommand()
    {
        VelocityDrag drag = new(_clip.Id, _clip.Notes, 56, 28);

        drag.Update(0, 14);

        SetNoteVelocity sent = Assert.IsType<SetNoteVelocity>(Assert.Single(Assert.IsType<RollEdit>(drag.Commit(_clip)).Build(_project)));
        Assert.Equal(95, sent.Velocity);
        Assert.Equal(2, sent.Notes!.Count);
        VelocityDrag same = new(_clip.Id, [_clip.Notes[0]], 56, 56 * (1 - (90 / 127.0)));
        Assert.Null(same.Commit(_clip));
    }

    /// <summary>A pedal event moves on the grid with its value kept, as a removal and an add, and never onto another event.</summary>
    [Fact]
    public void APedalEventMovesOnTheGridAsARemovalAndAnAdd()
    {
        RollGeometry geometry = new(80, 12, 4);
        PedalDrag drag = new(Song.Empty().With(Song.WithMidi("keys", _clip)), _clip, geometry, _clip.ControlChanges[0], 80);

        drag.Update(80 + 21, 0);

        IReadOnlyList<Command> sent = Assert.IsType<RollEdit>(drag.Commit(_clip)).Build(_project);
        Assert.Equal(2, sent.Count);
        Assert.Equal(1, Assert.IsType<RemoveControlChanges>(sent[0]).Time);
        AddControlChange added = Assert.IsType<AddControlChange>(sent[1]);
        Assert.Equal((1.25, 64, 127), (added.Time, added.Controller, added.Value));
        drag.Update(80 + 80, 0);
        Assert.Null(drag.Commit(_clip));
    }
}
