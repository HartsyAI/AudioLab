using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The effect chain edits of the session screen: one command each, by wire name, and a drag as one gesture of steps.</summary>
public sealed class EffectEditorTests
{
    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))),
    };

    private async Task<LiveProject> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        _hub.Reply = new CommandReply(true, true, 2, [], ["e1"]);
        return live;
    }

    private static EffectEditor EditorFor(LiveProject live) => new(live, new MidiEditor(live).SendAsync);

    private static T Parsed<T>((string Command, string Json, string? Gesture) sent)
        where T : Command
    {
        return Assert.IsType<T>(CoreCommands.CreateRegistry().Parse(sent.Command, sent.Json).Command);
    }

    /// <summary>An effect is added by kind, with no preset, and its id comes back for the caller to follow.</summary>
    [Fact]
    public async Task AnEffectIsAddedByKindAndItsIdComesBack()
    {
        await using LiveProject live = await OpenAsync();
        EffectEditor editor = EditorFor(live);

        EditResult result = await editor.AddAsync(TrackId.From("t1"), EffectKind.Delay);

        Assert.Equal(["e1"], result.Created);
        (string command, _, string? gesture) = Assert.Single(_hub.Sent);
        Assert.Equal("effect_add", command);
        Assert.Null(gesture);
        AddEffect sent = Parsed<AddEffect>(_hub.Sent[0]);
        Assert.Equal(EffectKind.Delay, sent.Kind);
        Assert.Null(sent.Preset);
    }

    /// <summary>Bypass and remove name the track and the effect, and bypass sends the state it wants, not a toggle.</summary>
    [Fact]
    public async Task BypassAndRemoveNameTheTrackAndTheEffect()
    {
        await using LiveProject live = await OpenAsync();
        EffectEditor editor = EditorFor(live);

        await editor.SetEnabledAsync(TrackId.From("t1"), EffectId.From("e1"), false);
        await editor.RemoveAsync(TrackId.From("t1"), EffectId.From("e1"));

        SetEffectEnabled enabled = Parsed<SetEffectEnabled>(_hub.Sent[0]);
        Assert.False(enabled.Enabled);
        Assert.Equal(EffectId.From("e1"), enabled.Effect);
        RemoveEffect removed = Parsed<RemoveEffect>(_hub.Sent[1]);
        Assert.Equal(TrackId.From("t1"), removed.Track);
    }

    /// <summary>A move over several places is one command for each place, sent as one gesture, so it is one undo step.</summary>
    [Fact]
    public async Task AMoveOverSeveralPlacesIsOneGestureOfSteps()
    {
        await using LiveProject live = await OpenAsync();
        EffectEditor editor = EditorFor(live);

        await editor.MoveAsync(TrackId.From("t1"), EffectId.From("e1"), -3);

        Assert.Equal(3, _hub.Sent.Count);
        Assert.All(_hub.Sent, sent =>
        {
            Assert.Equal("effect_move", sent.Command);
            Assert.Equal(MoveDirection.Up, Parsed<MoveEffect>(sent).Direction);
        });
        string? gesture = _hub.Sent[0].Gesture;
        Assert.NotNull(gesture);
        Assert.All(_hub.Sent, sent => Assert.Equal(gesture, sent.Gesture));
    }

    /// <summary>One step is sent alone, later is down, and no places sends nothing.</summary>
    [Fact]
    public async Task OneStepIsSentAloneAndNoPlacesSendsNothing()
    {
        await using LiveProject live = await OpenAsync();
        EffectEditor editor = EditorFor(live);

        await editor.MoveAsync(TrackId.From("t1"), EffectId.From("e1"), 1);
        await editor.MoveAsync(TrackId.From("t1"), EffectId.From("e1"), 0);

        (string command, string json, string? gesture) = Assert.Single(_hub.Sent);
        Assert.Equal("effect_move", command);
        Assert.Null(gesture);
        Assert.Equal(MoveDirection.Down, Parsed<MoveEffect>((command, json, gesture)).Direction);
    }

    /// <summary>A step the server refuses in the middle of a drag says how much of it was applied.</summary>
    [Fact]
    public async Task ARefusalInTheMiddleOfADragSaysHowMuchWasApplied()
    {
        await using LiveProject live = await OpenAsync();
        EffectEditor editor = EditorFor(live);
        _hub.ReplyForApply = count => count == 2 ? FakeProjectHub.Refused(CommandErrorCode.NotFound, "There is no effect e1.") : null;

        EditResult result = await editor.MoveAsync(TrackId.From("t1"), EffectId.From("e1"), 3);

        Assert.False(result.Succeeded);
        Assert.Contains("There is no effect e1.", result.Refusal, StringComparison.Ordinal);
        Assert.Contains("1 of 3 commands", result.Refusal, StringComparison.Ordinal);
        Assert.Equal(2, _hub.Sent.Count);
    }
}
