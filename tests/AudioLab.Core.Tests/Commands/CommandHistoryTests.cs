using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandHistoryTests
{
    private static readonly ClientId _a = ClientId.From("connection-a");

    private static readonly ClientId _b = ClientId.From("connection-b");

    private readonly ProjectDocument _initial = TestRegistry.Project();

    private readonly CommandHistory _history;

    public CommandHistoryTests()
    {
        _history = New();
    }

    [Fact]
    public void UndoAndRedoRestoreTheSameDocumentsAndIds()
    {
        _history.Apply(Marker("Chorus"), _a);
        ProjectDocument withMarker = _history.Current;
        string markerId = withMarker.Markers[^1].Id.Value;

        ProjectDocument? undone = _history.Undo();
        ProjectDocument? redone = _history.Redo();

        Assert.Equal(_initial, undone);
        Assert.Equal(withMarker, redone);
        Assert.Equal(markerId, _history.Current.Markers[^1].Id.Value);
        _history.Apply(Marker("Outro"), _a);
        Assert.NotEqual(markerId, _history.Current.Markers[^1].Id.Value);
    }

    [Fact]
    public void UndoRestoresTheWholeDocument()
    {
        _history.Apply(Bpm(140), _a);
        _history.Apply(new RenameProjectCommand { Project = _initial.Id, Name = "Renamed" }, _a);

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
        _history.Apply(Bpm(130), _a);
        _history.Undo();
        Assert.Equal(1, _history.RedoCount);

        _history.Apply(Bpm(150), _a);

        Assert.Equal(0, _history.RedoCount);
        Assert.Null(_history.Redo());
    }

    [Fact]
    public void TheLabelsNameTheCommandsThatBeganTheSteps()
    {
        _history.Apply(Bpm(130), _a);
        _history.Apply(Marker("A"), _a);

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
            CommandResult result = _history.Apply(Bpm(bpm), _a, drag);
            Assert.Equal(bpm, result.Document!.Bpm);
            Assert.Equal(bpm, _history.Current.Bpm);
        }
        _history.EndGesture(_a, drag);

        Assert.Equal(1, _history.UndoCount);
        _history.Undo();
        Assert.Equal(_initial, _history.Current);
        _history.Redo();
        Assert.Equal(105, _history.Current.Bpm);
    }

    [Fact]
    public void ADifferentGestureOrNoGestureFromTheSameClientClosesItsOpenOne()
    {
        _history.Apply(Bpm(101), _a, GestureId.From("a"));
        _history.Apply(Bpm(102), _a, GestureId.From("a"));
        _history.Apply(Bpm(103), _a, GestureId.From("b"));
        _history.Apply(Bpm(104), _a);
        _history.Apply(Bpm(105), _a);

        Assert.Equal(4, _history.UndoCount);
        Assert.Null(_history.OpenGestureOf(_a));
    }

    [Fact]
    public void TheSameGestureIdAfterItWasEndedStartsANewStep()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _a, drag);
        _history.EndGesture(_a, drag);
        _history.Apply(Bpm(102), _a, drag);
        _history.EndGesture(_a, drag);

        Assert.Equal(2, _history.UndoCount);
    }

    [Fact]
    public void UndoDuringAGestureClosesItAndReversesTheWholeStep()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _a, drag);
        _history.Apply(Bpm(102), _a, drag);

        _history.Undo();

        Assert.Null(_history.OpenGestureOf(_a));
        Assert.Equal(_initial, _history.Current);
        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void ARefusedCommandInsideAGestureChangesNothingAndTheGestureGoesOn()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(110), _a, drag);
        ProjectDocument before = _history.Current;

        CommandResult refused = _history.Apply(Bpm(5000), _a, drag);

        Assert.False(refused.Succeeded);
        Assert.Equal(before, _history.Current);
        Assert.Equal(drag, _history.OpenGestureOf(_a));
        _history.Apply(Bpm(111), _a, drag);
        _history.EndGesture(_a, drag);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ARefusedCommandKeepsTheRedoStepsAndTheOpenGesture()
    {
        _history.Apply(Bpm(130), _a);
        _history.Undo();
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _b, drag);

        _history.Apply(Bpm(5000), _a);
        _history.Apply(new RefusedCommand { Project = _initial.Id }, _a, GestureId.From("other"));

        Assert.Equal(1, _history.RedoCount);
        Assert.Equal(drag, _history.OpenGestureOf(_b));
    }

    [Fact]
    public void AnotherClientsCommandsNeverCloseYourGesture()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _a, drag);

        _history.Apply(Marker("From B"), _b);
        _history.Apply(Bpm(102), _a, drag);

        Assert.Equal(drag, _history.OpenGestureOf(_a));
        Assert.Equal(1, _history.UndoCount);
        _history.EndGesture(_a, drag);
        Assert.Equal(2, _history.UndoCount);
        Assert.Equal(102, _history.Current.Bpm);
        Assert.Equal("From B", _history.Current.Markers[^1].Name);
    }

    [Fact]
    public void TwoClientsMayUseTheSameGestureIdWithoutMerging()
    {
        GestureId drag = GestureId.From("drag-1");
        _history.Apply(Bpm(101), _a, drag);
        _history.Apply(Marker("B"), _b, drag);
        _history.Apply(Bpm(102), _a, drag);

        _history.EndGesture(_a, drag);

        Assert.Equal(1, _history.UndoCount);
        Assert.Equal(drag, _history.OpenGestureOf(_b));
        _history.EndGesture(_b, drag);
        Assert.Equal(2, _history.UndoCount);
    }

    [Fact]
    public void AnAcceptedCommandThatChangesNothingStillClosesItsClientsOtherGesture()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _a, drag);

        _history.Apply(new NothingCommand { Project = _initial.Id }, _b);
        Assert.Equal(drag, _history.OpenGestureOf(_a));
        _history.Apply(new NothingCommand { Project = _initial.Id }, _a, drag);
        Assert.Equal(drag, _history.OpenGestureOf(_a));
        _history.Apply(new NothingCommand { Project = _initial.Id }, _a);

        Assert.Null(_history.OpenGestureOf(_a));
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void AnEditByAnotherClientInsideAGestureIsItsOwnStepAndUndoingTheGestureRevertsItToo()
    {
        GestureId drag = GestureId.From("drag");
        _history.Apply(Bpm(101), _a, drag);
        ProjectDocument partial = _history.Current;
        _history.Apply(Marker("From B"), _b);
        _history.Apply(Bpm(102), _a, drag);
        _history.EndGesture(_a, drag);

        Assert.Equal(2, _history.UndoCount);
        Assert.Equal(_initial, _history.Undo());
        Assert.Equal(partial, _history.Undo());
        Assert.Null(_history.Undo());
    }

    [Fact]
    public void AtMostSixtyFourGesturesAreOpenAndTheNextIsRefusedWithALimitError()
    {
        for (int i = 0; i < CommandHistory.MaxOpenGestures; i++)
        {
            _history.Apply(Marker($"m{i}"), ClientId.From($"client-{i}"), GestureId.From("drag"));
        }
        ProjectDocument before = _history.Current;
        ClientId late = ClientId.From("client-late");

        CommandResult refused = _history.Apply(Marker("late"), late, GestureId.From("drag"));

        Assert.Equal(CommandErrorCode.Limit, Assert.Single(refused.Errors).Code);
        Assert.Equal(before, _history.Current);
        Assert.Null(_history.OpenGestureOf(late));
        Assert.True(_history.Apply(Marker("single"), late).Succeeded);
        Assert.True(_history.Apply(Marker("again"), ClientId.From("client-0"), GestureId.From("next")).Succeeded);
        _history.ReleaseClient(ClientId.From("client-1"));
        Assert.True(_history.Apply(Marker("fits"), late, GestureId.From("drag")).Succeeded);
    }

    [Fact]
    public void ACommandThatChangesNothingAddsNoStepAndKeepsRedo()
    {
        _history.Apply(Bpm(130), _a);
        _history.Undo();

        CommandResult result = _history.Apply(new NothingCommand { Project = _initial.Id }, _a);
        _history.Apply(Bpm(_initial.Bpm), _a);

        Assert.True(result.Succeeded);
        Assert.Equal(0, _history.UndoCount);
        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void AGestureThatEndsWhereItBeganLeavesTheHistoryExactlyAsItWas()
    {
        _history.Apply(Marker("One"), _a);
        _history.Apply(Marker("Two"), _a);
        _history.Undo();
        GestureId drag = GestureId.From("drag");

        _history.Apply(Bpm(150), _a, drag);
        _history.Apply(Bpm(_initial.Bpm), _a, drag);
        _history.EndGesture(_a, drag);

        Assert.Equal(1, _history.UndoCount);
        Assert.Equal("add_marker", _history.UndoLabel);
        Assert.Equal(1, _history.RedoCount);
        Assert.Equal("add_marker", _history.RedoLabel);
        Assert.Equal("Two", _history.Redo()!.Markers[^1].Name);
    }

    [Fact]
    public void AGestureThatEndsWhereItBeganKeepsEveryStepOfAFullHistory()
    {
        CommandHistory full = New(depth: 2);
        full.Apply(Bpm(101), _a);
        full.Apply(Bpm(102), _a);
        GestureId drag = GestureId.From("drag");

        full.Apply(Bpm(150), _a, drag);
        full.Apply(Bpm(102), _a, drag);
        full.EndGesture(_a, drag);

        Assert.Equal(2, full.UndoCount);
        full.Undo();
        full.Undo();
        Assert.Equal(_initial, full.Current);
    }

    [Fact]
    public void AnEditThatWouldBreakTheProjectLeavesTheHistoryAlone()
    {
        _history.Apply(Bpm(130), _a);
        ProjectDocument before = _history.Current;

        CommandResult result = _history.Apply(new BreakCommand { Project = _initial.Id, Count = 4 }, _a);

        Assert.Equal(4, result.Errors.Count);
        Assert.Equal(before, _history.Current);
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ACommandForAnotherProjectLeavesTheHistoryAlone()
    {
        _history.Apply(Bpm(130), _a);
        _history.Undo();
        ProjectId other = ProjectId.From("another-project");

        CommandResult result = _history.Apply(new RenameProjectCommand { Project = other, Name = "Stolen" }, _a);

        Assert.Equal(CommandErrorCode.WrongProject, Assert.Single(result.Errors).Code);
        Assert.Equal(_initial, _history.Current);
        Assert.Equal(0, _history.UndoCount);
        Assert.Equal(1, _history.RedoCount);
    }

    [Fact]
    public void ReleasingAGestureThatIsNotOpenChangesNothing()
    {
        GestureId drag = GestureId.From("drag");

        _history.EndGesture(_a, drag);
        _history.ReleaseClient(_a);
        _history.Apply(Bpm(101), _a, drag);
        _history.EndGesture(_a, GestureId.From("late-release"));
        _history.EndGesture(_b, drag);

        Assert.Equal(drag, _history.OpenGestureOf(_a));
        Assert.Equal(0, _history.UndoCount);
    }

    [Fact]
    public void ReleasingAClientClosesItsGesture()
    {
        _history.Apply(Bpm(101), _a, GestureId.From("drag"));

        _history.ReleaseClient(_a);

        Assert.Null(_history.OpenGestureOf(_a));
        Assert.Equal(1, _history.UndoCount);
    }

    [Fact]
    public void ADefaultClientOrGestureIdIsRefused()
    {
        Assert.Throws<ArgumentException>(() => _history.Apply(Bpm(101), default));
        Assert.Throws<ArgumentException>(() => _history.Apply(Bpm(101), _a, default(GestureId)));
        Assert.Throws<ArgumentException>(() => _history.EndGesture(default, GestureId.From("drag")));
        Assert.Throws<ArgumentException>(() => _history.ReleaseClient(default));
        Assert.Throws<ArgumentException>(() => ClientId.From(" "));
        Assert.Throws<ArgumentException>(() => ClientId.From(new string('c', ClientId.MaxLength + 1)));
    }

    [Fact]
    public void TheOldestStepsDropAtTheDepth()
    {
        CommandHistory bounded = New(depth: 3);
        for (int i = 1; i <= 5; i++)
        {
            bounded.Apply(Bpm(100 + i), _a);
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
        bounded.Apply(Bpm(101), _a);
        GestureId drag = GestureId.From("drag");
        for (int i = 0; i < 50; i++)
        {
            bounded.Apply(Marker($"m{i}"), _a, drag);
        }
        bounded.Apply(Bpm(102), _a);

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
            bounded.Apply(Bpm(100 + i), _a);
        }
        bounded.Undo();
        bounded.Redo();
        bounded.Undo();
        bounded.Undo();
        bounded.Redo();
        bounded.Apply(Bpm(120), _a);

        Assert.True(bounded.UndoCount + bounded.RedoCount <= 2);
    }

    [Fact]
    public void TheSameCommandsOnTheSameProjectGiveTheSameHistory()
    {
        CommandHistory first = New();
        CommandHistory second = New();

        foreach (CommandHistory history in new[] { first, second })
        {
            history.Apply(Marker("A"), _a);
            history.Apply(Bpm(120), _a, GestureId.From("g"));
            history.Apply(Bpm(121), _a, GestureId.From("g"));
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
