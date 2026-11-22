using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class MixerCommandTests
{
    private readonly CommandRig _rig = new();

    private readonly TrackId _vocals;

    private readonly TrackId _guitar;

    private readonly TrackId _reverb;

    public MixerCommandTests()
    {
        _vocals = Add(TrackKind.Audio, "Vocals");
        _guitar = Add(TrackKind.Audio, "Guitar");
        _reverb = Add(TrackKind.Bus, "Reverb");
    }

    private TrackId Add(TrackKind kind, string name) => TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Kind = kind, Name = name })[0]);

    private Track Vocals => _rig.Project.Tracks.First(track => track.Id == _vocals);

    [Fact]
    public void TheFaderPanMuteAndSoloAreSetAndUndone()
    {
        ProjectDocument before = _rig.Project;

        _rig.Ok(new SetTrackVolume { Project = _rig.Id, Track = _vocals, Volume = 1.5 });
        _rig.Ok(new SetTrackPan { Project = _rig.Id, Track = _vocals, Pan = -0.25 });
        _rig.Ok(new SetTrackMuted { Project = _rig.Id, Track = _vocals, Muted = true });
        _rig.Ok(new SetTrackSoloed { Project = _rig.Id, Track = _vocals, Soloed = true });

        Assert.Equal((1.5, -0.25, true, true), (Vocals.Volume, Vocals.Pan, Vocals.Muted, Vocals.Soloed));
        for (int i = 0; i < 4; i++)
        {
            _rig.History.Undo();
        }
        Assert.Equal(before, _rig.Project);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(16.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AFaderOutsideZeroToSixteenIsRefused(double volume)
    {
        _rig.RefusedWith(new SetTrackVolume { Project = _rig.Id, Track = _vocals, Volume = volume }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetMasterVolume { Project = _rig.Id, Volume = volume }, CommandErrorCode.Invalid);
    }

    [Theory]
    [InlineData(-1.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void APanOutsideMinusOneToOneIsRefused(double pan)
    {
        _rig.RefusedWith(new SetTrackPan { Project = _rig.Id, Track = _vocals, Pan = pan }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void TheMasterFaderAndLimiterAreSet()
    {
        _rig.Ok(new SetMasterVolume { Project = _rig.Id, Volume = 0.9 });
        _rig.Ok(new SetMasterLimiter { Project = _rig.Id, Enabled = true });

        Assert.Equal((0.9, true), (_rig.Project.MasterVolume, _rig.Project.MasterLimiter));
        _rig.History.Undo();
        Assert.False(_rig.Project.MasterLimiter);
    }

    [Fact]
    public void ADraggedFaderIsOneUndoStep()
    {
        GestureId drag = GestureId.From("fader");
        int steps = _rig.History.UndoCount;

        foreach (double level in new[] { 0.7, 0.9, 1.1, 1.4 })
        {
            Assert.True(_rig.Run(new SetTrackVolume { Project = _rig.Id, Track = _vocals, Volume = level }, drag).Succeeded);
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(0.8, Vocals.Volume);
    }

    [Fact]
    public void AMixerEditOfAnUnknownTrackIsNotFound()
    {
        TrackId none = TrackId.From("trk_none");

        _rig.RefusedWith(new SetTrackVolume { Project = _rig.Id, Track = none, Volume = 1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetTrackPan { Project = _rig.Id, Track = none, Pan = 0 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetTrackMuted { Project = _rig.Id, Track = none, Muted = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetTrackSoloed { Project = _rig.Id, Track = none, Soloed = true }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ASendStartsHalfwayPostFaderAndUnmuted()
    {
        _rig.Ok(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb });

        Assert.Equal(new Send(_reverb, 0.5, false, false), Assert.Single(Vocals.Sends));
    }

    [Fact]
    public void ASendLevelPlacementAndMuteAreEditedAndRemoved()
    {
        _rig.Ok(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = 0.2 });

        _rig.Ok(new SetSendLevel { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = 0.7 });
        _rig.Ok(new SetSendPre { Project = _rig.Id, Track = _vocals, Bus = _reverb, Pre = true });
        _rig.Ok(new SetSendMuted { Project = _rig.Id, Track = _vocals, Bus = _reverb, Muted = true });
        Assert.Equal(new Send(_reverb, 0.7, true, true), Vocals.Sends[0]);

        _rig.Ok(new RemoveSend { Project = _rig.Id, Track = _vocals, Bus = _reverb });
        Assert.Empty(Vocals.Sends);
    }

    [Fact]
    public void ASendHasToGoFromANonBusTrackToABusOnce()
    {
        _rig.Ok(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb });

        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = _reverb, Bus = _reverb }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = _guitar, Bus = _vocals }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = _guitar, Bus = TrackId.From("trk_none") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = TrackId.From("trk_none"), Bus = _reverb }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ASendLevelOutsideZeroToSixteenIsRefused()
    {
        _rig.RefusedWith(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = 17 }, CommandErrorCode.Invalid);
        _rig.Ok(new AddSend { Project = _rig.Id, Track = _vocals, Bus = _reverb });
        _rig.RefusedWith(new SetSendLevel { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetSendLevel { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = double.NaN }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void EditingASendThatDoesNotExistIsNotFound()
    {
        _rig.RefusedWith(new SetSendLevel { Project = _rig.Id, Track = _vocals, Bus = _reverb, Level = 1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetSendPre { Project = _rig.Id, Track = _vocals, Bus = _reverb, Pre = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetSendMuted { Project = _rig.Id, Track = _vocals, Bus = _reverb, Muted = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveSend { Project = _rig.Id, Track = _vocals, Bus = _reverb }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveSend { Project = _rig.Id, Track = TrackId.From("trk_none"), Bus = _reverb }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void AnUnchangedValueAddsNoUndoStep()
    {
        int steps = _rig.History.UndoCount;

        _rig.Ok(new SetTrackVolume { Project = _rig.Id, Track = _vocals, Volume = 0.8 });
        _rig.Ok(new SetTrackMuted { Project = _rig.Id, Track = _vocals, Muted = false });
        _rig.Ok(new SetMasterVolume { Project = _rig.Id, Volume = 1 });

        Assert.Equal(steps, _rig.History.UndoCount);
    }
}
