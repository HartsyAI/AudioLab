using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandHistoryTests
{
    private readonly ProjectDocument _initial = TestRegistry.Project();

    private readonly CommandHistory _history;

    public CommandHistoryTests()
    {
        _history = New();
    }

    [Fact]
    public void UndoAndRedoRestoreTheSameDocumentsAndIds()
    {
        _history.Apply(Marker("Chorus"));
        ProjectDocument withMarker = _history.Current;
        string markerId = withMarker.Markers[^1].Id.Value;

        ProjectDocument? undone = _history.Undo();
        ProjectDocument? redone = _history.Redo();

        Assert.Equal(_initial, undone);
        Assert.Equal(withMarker, redone);
        Assert.Equal(markerId, _history.Current.Markers[^1].Id.Value);
        _history.Apply(Marker("Outro"));
        Assert.NotEqual(markerId, _history.Current.Markers[^1].Id.Value);
    }

    [Fact]
    public void UndoRestoresTheWholeDocument()
    {
        _history.Apply(Bpm(140));
        _history.Apply(new RenameProjectCommand { Project = _initial.Id, Name = "Renamed" });

        _history.Undo();
        Assert.Equal(_initial with { Bpm = 140 }, _history.Current);
        _history.Undo();

        Assert.Equal(_initial, _history.Current);
    }

    [Fact]
    public void NothingToUndoOrRedoReturnsNull()
    {
        Assert.Null(_history.Undo());
        Assert.Null(_history.Redo());
        Assert.Equal(_initial, _history.Current);
    }

    [Fact]
    public void ANewEditClearsTheRedoSteps()
    {
        _history.Apply(Bpm(130));
        _history.Undo();
        Assert.Equal(1, _history.RedoCount);

        _history.Apply(Bpm(150));

        Assert.Equal(0, _history.RedoCount);
        Assert.Null(_history.Redo());
    }

    [Fact]
    public void TheLabelsNameTheCommandsThatBeganTheSteps()
    {
        _history.Apply(Bpm(130));
        _history.Apply(Marker("A"));

        Assert.Equal("add_marker", _history.UndoLabel);
        _history.Undo();
        Assert.Equal("set_bpm", _history.UndoLabel);
        Assert.Equal("add_marker", _history.RedoLabel);
    }

    [Fact]
    public void TheCommandsOfOneGestureAreOneStepAndEachShowsAtOnce()
    {
        GestureId drag = GestureId.From("drag-1");

        foreach (double bpm in new[] { 101.0, 102.0, 103.0, 104.0, 105.0 })
        {
            CommandResult result = _history.Apply(Bpm(bpm), drag);
            Assert.Equal(bpm, result.Document!.Bpm);
            Assert.Equal(bpm, _history.Current.Bpm);
        }

        Assert.Equal(1, _history.UndoCount);
        _history.Undo();
        Assert.Equal(_initial, _history.Current);
        _history.Redo();
        Assert.Equal(105, _history.Current.Bpm);
    }

    [Fact]
    public void ADifferentGestureOrNoGestureClosesTheOpenOne()
    {
        _history.Apply(Bpm(101), GestureId.From("a"));
        _history.Apply(Bpm(102), GestureId.From("a"));
        _history.Apply(Bpm(103), GestureId.From("b"));
        _history.Apply(Bpm(104));
        _history.Apply(Bpm(105));

        Assert.Equal(4, _history.UndoCount);
        Assert.Null(_history.OpenGesture);
    }

    [Fact]
    public void TheSameGestureIdAfterItWasEndedStartsANewStep()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), drag);
        _history.EndGesture(drag);
        _history.Apply(Bpm(102), drag);

        Assert.Equal(2, _history.UndoCount);
        Assert.Equal(drag, _history.OpenGesture);
    }

    [Fact]
    public void UndoDuringAGestureClosesItAndReversesTheWholeStep()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), drag);
        _history.Apply(Bpm(102), drag);

        _history.Undo();

        Assert.Null(_history.OpenGesture);
        Assert.Equal(_initial, _history.Current);
        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void ARefusedCommandInsideAGestureChangesNothingAndTheGestureGoesOn()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(110), drag);
        ProjectDocument before = _history.Current;

        CommandResult refused = _history.Apply(Bpm(5000), drag);

        Assert.False(refused.Succeeded);
        Assert.Equal(before, _history.Current);
        Assert.Equal(drag, _history.OpenGesture);
        Assert.Equal(1, _history.UndoCount);
        _history.Apply(Bpm(111), drag);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ARefusalFromAnotherSourceDoesNotCloseTheOpenGesture()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), drag);

        _history.Apply(Bpm(5000));
        _history.Apply(new RefusedCommand { Project = _initial.Id }, GestureId.From("other"));
        _history.Apply(Bpm(102), drag);

        Assert.Equal(drag, _history.OpenGesture);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ARefusedCommandKeepsTheRedoSteps()
    {
        _history.Apply(Bpm(130));
        _history.Undo();

        _history.Apply(Bpm(5000));

        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void TheReleaseOfAnotherGestureLeavesTheOpenOneAlone()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), drag);

        _history.EndGesture(GestureId.From("late-release"));
        _history.Apply(Bpm(102), drag);

        Assert.Equal(drag, _history.OpenGesture);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ACommandThatChangesNothingAddsNoStepAndKeepsRedo()
    {
        _history.Apply(Bpm(130));
        _history.Undo();

        CommandResult result = _history.Apply(new NothingCommand { Project = _initial.Id });
        _history.Apply(Bpm(_initial.Bpm));

        Assert.True(result.Succeeded);
        Assert.Equal(0, _history.UndoCount);
        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void AGestureThatEndsWhereItBeganLeavesNoStep()
    {
        _history.Apply(Marker("Before"));
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(150), drag);
        _history.Apply(Bpm(_initial.Bpm), drag);

        _history.EndGesture(drag);

        Assert.Equal(1, _history.UndoCount);
        Assert.Equal("add_marker", _history.UndoLabel);
    }

    [Fact]
    public void AnEditThatWouldBreakTheProjectLeavesTheHistoryAlone()
    {
        _history.Apply(Bpm(130));
        ProjectDocument before = _history.Current;

        CommandResult result = _history.Apply(new BreakCommand { Project = _initial.Id, Count = 4 });

        Assert.Equal(4, result.Errors.Count);
        Assert.Equal(before, _history.Current);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void TheOldestStepsDropAtTheDepth()
    {
        CommandHistory bounded = New(depth: 3);
        for (int i = 1; i <= 5; i++)
        {
            bounded.Apply(Bpm(100 + i));
        }

        Assert.Equal(3, bounded.UndoCount);
        bounded.Undo();
        bounded.Undo();
        bounded.Undo();
        Assert.Equal(_initial with { Bpm = 102 }, bounded.Current);
        Assert.Null(bounded.Undo());
    }

    [Fact]
    public void AGestureCountsAsOneStepAgainstTheDepth()
    {
        CommandHistory bounded = New(depth: 2);
        bounded.Apply(Bpm(101));
        GestureId drag = GestureId.From("drag");
        for (int i = 0; i < 50; i++)
        {
            bounded.Apply(Marker($"m{i}"), drag);
        }
        bounded.Apply(Bpm(102));

        Assert.Equal(2, bounded.UndoCount);
        bounded.Undo();
        Assert.Equal(_initial.Markers.Count + 50, bounded.Current.Markers.Count);
    }

    [Fact]
    public void UndoAndRedoStepsTogetherNeverExceedTheDepth()
    {
        CommandHistory bounded = New(depth: 2);
        for (int i = 1; i <= 4; i++)
        {
            bounded.Apply(Bpm(100 + i));
        }
        bounded.Undo();
        bounded.Redo();
        bounded.Undo();
        bounded.Undo();
        bounded.Redo();
        bounded.Apply(Bpm(120));

        Assert.True(bounded.UndoCount + bounded.RedoCount <= 2);
    }

    [Fact]
    public void TheSameCommandsOnTheSameProjectGiveTheSameHistory()
    {
        CommandHistory first = New();
        CommandHistory second = New();

        foreach (CommandHistory history in new[] { first, second })
        {
            history.Apply(Marker("A"));
            history.Apply(Bpm(120), GestureId.From("g"));
            history.Apply(Bpm(121), GestureId.From("g"));
            history.Undo();
            history.Redo();
        }

        Assert.Equal(first.Current, second.Current);
        Assert.Equal(first.UndoCount, second.UndoCount);
    }

    [Fact]
    public void AHistoryNeedsAValidProjectAndASaneDepth()
    {
        Assert.Throws<ArgumentException>(
            () => new CommandHistory(_initial with { Bpm = 0 }, TestRegistry.Create(), TestRegistry.Ids()));
        Assert.Throws<ArgumentOutOfRangeException>(() => New(depth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => New(depth: CommandHistory.MaxDepth + 1));
    }

    private CommandHistory New(int depth = CommandHistory.DefaultDepth)
    {
        return new CommandHistory(_initial, TestRegistry.Create(), TestRegistry.Ids(), depth);
    }

    private SetBpmCommand Bpm(double bpm) => new() { Project = _initial.Id, Bpm = bpm };

    private AddMarkerCommand Marker(string name) => new() { Project = _initial.Id, Time = 100, Name = name };
}
