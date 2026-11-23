using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The MIDI clip edits of the session screen: one command each, by wire name, with values the server would accept.</summary>
public sealed class MidiEditorTests
{
    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.WithMidi("keys", Song.Midi("a", 0, 4)))),
    };

    private async Task<LiveProject> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        _hub.Reply = new CommandReply(true, true, 2, [], ["clip_new"]);
        return live;
    }

    /// <summary>A clip with no place goes after the last clip and is one bar long.</summary>
    [Fact]
    public async Task AClipWithNoPlaceGoesAfterTheLastClipAndIsOneBarLong()
    {
        await using LiveProject live = await OpenAsync();

        EditResult result = await new MidiEditor(live).AddClipAsync(TrackId.From("keys"));

        Assert.Equal(["clip_new"], result.Created);
        (string command, string json, string? gesture) = Assert.Single(_hub.Sent);
        Assert.Equal("midi_clip_add", command);
        Assert.Null(gesture);
        AddMidiClip sent = Assert.IsType<AddMidiClip>(CoreCommands.CreateRegistry().Parse(command, json).Command);
        Assert.Equal(4, sent.Start, 9);
        Assert.Equal(2, sent.Length, 9);
    }

    /// <summary>A clip put at a place is sent there with its length.</summary>
    [Fact]
    public async Task AClipPutAtAPlaceIsSentThere()
    {
        await using LiveProject live = await OpenAsync();

        await new MidiEditor(live).AddClipAsync(TrackId.From("keys"), 10);

        AddMidiClip sent = Assert.IsType<AddMidiClip>(CoreCommands.CreateRegistry().Parse(_hub.Sent[0].Command, _hub.Sent[0].Json).Command);
        Assert.Equal(10, sent.Start, 9);
    }

    /// <summary>A step sends the absolute length, and a step below the least length is refused without a command.</summary>
    [Fact]
    public async Task AStepSendsTheAbsoluteLengthAndAStepBelowTheLeastLengthSendsNothing()
    {
        await using LiveProject live = await OpenAsync();
        MidiEditor editor = new(live);

        await editor.StepLengthAsync(ClipId.From("a"), 1);
        SetMidiClipLength sent = Assert.IsType<SetMidiClipLength>(CoreCommands.CreateRegistry().Parse(_hub.Sent[0].Command, _hub.Sent[0].Json).Command);
        Assert.Equal(6, sent.Length, 9);

        EditResult below = await editor.StepLengthAsync(ClipId.From("a"), -2);
        Assert.False(below.Succeeded);
        Assert.Single(_hub.Sent);
        EditResult missing = await editor.StepLengthAsync(ClipId.From("nope"), 1);
        Assert.False(missing.Succeeded);
    }

    /// <summary>The server's reason for shortening a clip past a note comes back as written.</summary>
    [Fact]
    public async Task TheServersReasonForAShortenedClipComesBackAsWritten()
    {
        await using LiveProject live = await OpenAsync();
        _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.Rejected, "a note starts at or after the new end");

        EditResult result = await new MidiEditor(live).StepLengthAsync(ClipId.From("a"), 1);

        Assert.Equal("A note starts at or after the new end.", result.Refusal);
    }
}
