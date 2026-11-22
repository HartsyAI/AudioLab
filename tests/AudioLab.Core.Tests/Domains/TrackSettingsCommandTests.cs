using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class TrackSettingsCommandTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private readonly TrackId _audio = TrackId.From("trk_audio");

    private readonly TrackId _keys = TrackId.From("trk_keys");

    private readonly TrackId _bus = TrackId.From("trk_bus");

    private readonly CommandRig _rig;

    public TrackSettingsCommandTests()
    {
        Instrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#abc", synth);
        _rig = new CommandRig(BlankProject.Create() with
        {
            Tracks = [BlankProject.Track("trk_audio", "Audio"), keys, BlankProject.Track("trk_bus", "Bus", TrackKind.Bus)],
        });
    }

    private Track Track(TrackId id) => _rig.Project.Tracks.First(track => track.Id == id);

    // ----- arm and monitor

    [Fact]
    public void AnAudioAndAnInstrumentTrackAreArmedAndMonitoredAndDisarmed()
    {
        foreach (TrackId id in new[] { _audio, _keys })
        {
            _rig.Ok(new SetTrackArmed { Project = _rig.Id, Track = id, Armed = true });
            _rig.Ok(new SetTrackMonitor { Project = _rig.Id, Track = id, Monitor = true });
            Assert.True(Track(id).Armed && Track(id).Monitor);
            _rig.Ok(new SetTrackArmed { Project = _rig.Id, Track = id, Armed = false });
            _rig.Ok(new SetTrackMonitor { Project = _rig.Id, Track = id, Monitor = false });
            Assert.False(Track(id).Armed || Track(id).Monitor);
        }
    }

    [Fact]
    public void ArmingAndMonitoringAreLiveStateAndAddNoUndoStepAndUndoDoesNotTakeThemBack()
    {
        _rig.Ok(new SetTrackHeight { Project = _rig.Id, Track = _audio, Height = 120 });
        int steps = _rig.History.UndoCount;

        _rig.Ok(new SetTrackArmed { Project = _rig.Id, Track = _audio, Armed = true });
        _rig.Ok(new SetTrackMonitor { Project = _rig.Id, Track = _keys, Monitor = true });
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.History.Undo();

        Assert.Equal(100.0, Track(_audio).View.Height);
        Assert.True(Track(_audio).Armed);
        Assert.True(Track(_keys).Monitor);
        _rig.History.Redo();
        Assert.Equal(120.0, Track(_audio).View.Height);
        Assert.True(Track(_audio).Armed);
    }

    [Fact]
    public void ATrackBroughtBackByUndoIsNotArmedAndOnlyTheArmAndMonitorCommandsAreLiveState()
    {
        _rig.Ok(new SetTrackArmed { Project = _rig.Id, Track = _audio, Armed = true });
        _rig.Ok(new RemoveTrack { Project = _rig.Id, Track = _audio });

        _rig.History.Undo();

        Assert.False(Track(_audio).Armed);
        IEnumerable<string> live = CoreCommands.CreateRegistry().Commands.Where(command => !command.Undoable).Select(command => command.Name);
        Assert.Equal(["track_set_armed", "track_set_monitor"], live.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnAuxBusHasNoInputToArmOrMonitorButTurningItOffIsAlwaysFine()
    {
        int steps = _rig.History.UndoCount;

        _rig.RefusedWith(new SetTrackArmed { Project = _rig.Id, Track = _bus, Armed = true }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetTrackMonitor { Project = _rig.Id, Track = _bus, Monitor = true }, CommandErrorCode.Rejected);
        Assert.True(_rig.Run(new SetTrackArmed { Project = _rig.Id, Track = _bus, Armed = false }).Succeeded);
        Assert.True(_rig.Run(new SetTrackMonitor { Project = _rig.Id, Track = _bus, Monitor = false }).Succeeded);

        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.RefusedWith(new SetTrackArmed { Project = _rig.Id, Track = TrackId.From("trk_none"), Armed = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetTrackMonitor { Project = _rig.Id, Track = TrackId.From("trk_none"), Monitor = false }, CommandErrorCode.NotFound);
    }

    // ----- input

    [Fact]
    public void TheInputIsSetAsADeviceAndChannelsAndASingleChannelNeedsItsNumber()
    {
        _rig.Ok(new SetTrackInput { Project = _rig.Id, Track = _audio, DeviceId = "dev-1", Channels = InputChannelMode.Single, Channel = 3 });
        Assert.Equal(new TrackInput("dev-1", InputChannelMode.Single, 3), Track(_audio).Input);

        _rig.Ok(new SetTrackInput { Project = _rig.Id, Track = _audio, Channels = InputChannelMode.Stereo });

        Assert.Equal(new TrackInput(null, InputChannelMode.Stereo), Track(_audio).Input);
    }

    [Fact]
    public void ASingleChannelWithNoNumberOrANumberOutsideOneToTwoFiftySixIsInvalidAndANumberOnAnotherModeIsRejected()
    {
        SetTrackInput single = new() { Project = _rig.Id, Track = _audio, Channels = InputChannelMode.Single, Channel = 1 };

        _rig.RefusedWith(single with { Channel = null }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { Channel = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { Channel = TrackRules.MaxChannel + 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { Channel = TrackRules.MinChannel - 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { Channels = InputChannelMode.Mono }, CommandErrorCode.Rejected);
        _rig.RefusedWith(single with { Channels = InputChannelMode.Mono, Channel = 999 }, CommandErrorCode.Invalid);
        string tooLong = new('d', TrackRules.MaxDeviceIdLength + 1);
        _rig.RefusedWith(single with { Channels = InputChannelMode.Stereo, DeviceId = tooLong }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { DeviceId = new string('d', 513) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { DeviceId = string.Empty }, CommandErrorCode.Invalid);
        _rig.RefusedWith(single with { Channels = (InputChannelMode)9, Channel = null }, CommandErrorCode.Invalid);
        _rig.Ok(single with { Channel = TrackRules.MaxChannel, DeviceId = new string('d', TrackRules.MaxDeviceIdLength) });
    }

    [Fact]
    public void OnlyAnAudioTrackHasAnInputToChooseAndATrackThatIsNotThereIsNotFound()
    {
        SetTrackInput mono = new() { Project = _rig.Id, Track = _bus, Channels = InputChannelMode.Mono };

        _rig.RefusedWith(mono, CommandErrorCode.Rejected);
        _rig.RefusedWith(mono with { Track = _keys }, CommandErrorCode.Rejected);
        _rig.RefusedWith(mono with { Track = _keys, Channel = 999 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(mono with { Track = TrackId.From("trk_none"), Channel = 99 }, CommandErrorCode.NotFound);
    }

    // ----- height

    [Fact]
    public void TheHeightIsSetFromTheMinimumToTheMaximumAndEveryMenuHeightIsAllowed()
    {
        foreach ((string _, double height) in TrackRules.HeightPresets)
        {
            _rig.Ok(new SetTrackHeight { Project = _rig.Id, Track = _audio, Height = height });
            Assert.Equal(height, Track(_audio).View.Height);
        }
        _rig.Ok(new SetTrackHeight { Project = _rig.Id, Track = _audio, Height = TrackRules.MinHeight });
        _rig.Ok(new SetTrackHeight { Project = _rig.Id, Track = _audio, Height = TrackRules.MaxHeight });

        SetTrackHeight tall = new() { Project = _rig.Id, Track = _audio, Height = 1 };
        _rig.RefusedWith(tall with { Height = double.BitDecrement(TrackRules.MinHeight) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(tall with { Height = double.BitIncrement(TrackRules.MaxHeight) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(tall with { Height = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(tall with { Track = TrackId.From("trk_none"), Height = double.NaN }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ADragOfTheLaneEdgeIsOneGestureAndEveryFrameSucceeds()
    {
        GestureId drag = GestureId.From("height-drag");
        int steps = _rig.History.UndoCount;

        foreach (double height in new[] { 110.0, 130.5, 150.25, 160.0 })
        {
            Assert.True(_rig.Run(new SetTrackHeight { Project = _rig.Id, Track = _audio, Height = height }, drag).Succeeded);
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(100.0, Track(_audio).View.Height);
    }

    // ----- icon

    [Fact]
    public void AnEmojiAndASmallImageAreIconsAndAbsentRemovesThem()
    {
        string image = "data:image/png;base64,iVBORw0KGgo=";

        _rig.Ok(new SetTrackIcon { Project = _rig.Id, Track = _audio, Icon = "\U0001F3B8" });
        Assert.Equal("\U0001F3B8", Track(_audio).Icon);
        _rig.Ok(new SetTrackIcon { Project = _rig.Id, Track = _audio, Icon = image });
        Assert.Equal(image, Track(_audio).Icon);

        _rig.Ok(new SetTrackIcon { Project = _rig.Id, Track = _audio });

        Assert.Null(Track(_audio).Icon);
    }

    [Fact]
    public void AnIconThatIsNotShortCleanTextOrASmallImageIsInvalidAndRemovingNoneAddsNoUndoStep()
    {
        SetTrackIcon icon = new() { Project = _rig.Id, Track = _audio, Icon = "x" };

        _rig.RefusedWith(icon with { Icon = string.Empty }, CommandErrorCode.Invalid);
        _rig.RefusedWith(icon with { Icon = new string('a', 17) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(icon with { Icon = "a\u0007" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(icon with { Icon = "data:text/html;base64,AAAA" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(icon with { Icon = "data:image/png;base64," + new string('A', TrackIcon.MaxImageLength) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(icon with { Track = TrackId.From("trk_none"), Icon = string.Empty }, CommandErrorCode.NotFound);
        int steps = _rig.History.UndoCount;

        Assert.True(_rig.Run(new SetTrackIcon { Project = _rig.Id, Track = _audio }).Succeeded);

        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(icon with { Icon = new string('a', 16) });
    }
}
