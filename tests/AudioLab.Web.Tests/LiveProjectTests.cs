using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the live project tests.</summary>
public sealed class LiveProjectTests
{
    private readonly FakeProjectHub _hub = new();

    private FakeHubFactory _factory = null!;

    private LiveProject Live()
    {
        _factory = new FakeHubFactory(_hub);
        return new LiveProject(_factory, new CommandSender(CoreCommands.CreateRegistry()));
    }

    private static ProjectChange Change(long revision, string name, string reason = "track_add")
    {
        return new ProjectChange("song", revision, ProjectJson.Write(Song.Empty() with { Name = name }), reason);
    }

    /// <summary>Opening joins and holds the document and revision the join answered.</summary>
    [Fact]
    public async Task OpeningJoinsAndHoldsTheDocumentAndRevisionTheJoinAnswered()
    {
        _hub.JoinRevision = 4;
        await using LiveProject live = Live();

        await live.OpenAsync("song");

        Assert.Equal(["connect", "join song"], _hub.Calls);
        Assert.True(_factory.Reconnects, "a screen's connection opens again by itself after a loss");
        Assert.Equal("Song", live.Document!.Name);
        Assert.Equal(4, live.Revision);
        Assert.Equal(HubState.Connected, live.State);
    }

    /// <summary>Only a newer revision is applied and the newest sent out of order wins.</summary>
    [Fact]
    public async Task OnlyANewerRevisionIsAppliedAndTheNewestSentOutOfOrderWins()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");
        int raised = 0;
        live.Changed += () => raised++;

        _hub.Push(Change(3, "Three"));
        _hub.Push(Change(2, "Two"));
        _hub.Push(Change(3, "Again"));

        Assert.Equal("Three", live.Document!.Name);
        Assert.Equal(3, live.Revision);
        Assert.Equal(1, raised);
    }

    /// <summary>A change that arrives before the join answers is kept and applied after it if it is newer.</summary>
    [Fact]
    public async Task AChangeThatArrivesBeforeTheJoinAnswersIsKeptAndAppliedAfterItIfItIsNewer()
    {
        _hub.JoinRevision = 2;
        _hub.BeforeJoinReturns = () =>
        {
            _hub.Push(Change(5, "Five"));
            _hub.Push(Change(4, "Four"));
            return Task.CompletedTask;
        };
        await using LiveProject live = Live();

        await live.OpenAsync("song");

        Assert.Equal("Five", live.Document!.Name);
        Assert.Equal(5, live.Revision);
    }

    /// <summary>A change before the join that the join already covers is dropped.</summary>
    [Fact]
    public async Task AChangeBeforeTheJoinThatTheJoinAlreadyCoversIsDropped()
    {
        _hub.JoinRevision = 6;
        _hub.BeforeJoinReturns = () =>
        {
            _hub.Push(Change(5, "Five"));
            return Task.CompletedTask;
        };
        await using LiveProject live = Live();

        await live.OpenAsync("song");

        Assert.Equal("Song", live.Document!.Name);
        Assert.Equal(6, live.Revision);
    }

    /// <summary>A send returns only once the revision it made has been applied.</summary>
    [Fact]
    public async Task ASendReturnsOnlyOnceTheRevisionItMadeHasBeenApplied()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");
        _hub.Reply = new CommandReply(true, true, 2, [], ["trk_9"]);
        RenameProject command = new() { Project = ProjectId.From("song"), Name = "x" };

        Task<CommandReply> sending = live.SendAsync(command);
        await Task.Delay(50);
        Assert.False(sending.IsCompleted);
        _hub.Push(Change(2, "Renamed", "project_rename"));
        CommandReply reply = await sending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["trk_9"], reply.Created);
        Assert.Equal("Renamed", live.Document!.Name);
    }

    /// <summary>A refused or unchanged command does not wait for a revision.</summary>
    [Fact]
    public async Task ARefusedOrUnchangedCommandDoesNotWaitForARevision()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");
        RenameProject command = new() { Project = ProjectId.From("song"), Name = "x" };

        _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.Rejected, "no");
        CommandReply refused = await live.SendAsync(command).WaitAsync(TimeSpan.FromSeconds(2));
        _hub.Reply = new CommandReply(true, false, 1, [], []);
        CommandReply unchanged = await live.SendAsync(command).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(refused.Succeeded);
        Assert.True(unchanged.Succeeded);
    }

    /// <summary>A deleted project is a problem the screen can show.</summary>
    [Fact]
    public async Task ADeletedProjectIsAProblemTheScreenCanShow()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");

        _hub.Close(new ProjectClosed("song", "deleted"));

        Assert.Equal("This project was deleted.", live.Problem);
    }

    /// <summary>A document this version cannot read is a problem and the last good one stays.</summary>
    [Fact]
    public async Task ADocumentThisVersionCannotReadIsAProblemAndTheLastGoodOneStays()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");

        _hub.Push(new ProjectChange("song", 2, "{\"version\":4,\"surprise\":1}", "x"));

        Assert.NotNull(live.Problem);
        Assert.Equal(1, live.Revision);
        Assert.Equal("Song", live.Document!.Name);
    }

    /// <summary>After the connection drops and returns the project is joined again and the answer is adopted.</summary>
    [Fact]
    public async Task AfterTheConnectionDropsAndReturnsTheProjectIsJoinedAgainAndTheAnswerIsAdopted()
    {
        await using LiveProject live = Live();
        await live.OpenAsync("song");
        _hub.JoinRevision = 9;
        _hub.JoinDocument = ProjectJson.Write(Song.Empty() with { Name = "After the drop" });

        _hub.Move(HubState.Reconnecting);
        Assert.Equal(HubState.Reconnecting, live.State);
        _hub.Move(HubState.Connected);
        await Task.Delay(50);

        Assert.Equal(2, _hub.Calls.Count(call => call == "join song"));
        Assert.Equal("After the drop", live.Document!.Name);
        Assert.Equal(9, live.Revision);
        Assert.Null(live.Problem);
    }

    /// <summary>Every gesture id is new and fits the rule for ids.</summary>
    [Fact]
    public async Task EveryGestureIdIsNewAndFitsTheRuleForIds()
    {
        await using LiveProject live = Live();

        string first = live.NewGesture();
        string second = live.NewGesture();

        Assert.NotEqual(first, second);
        Assert.True(IdRules.IsValid(first), first);
    }
}
