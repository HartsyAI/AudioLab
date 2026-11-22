using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class EffectCommandTests
{
    private readonly TrackId _track = TrackId.From("trk_a");

    private readonly TrackId _bus = TrackId.From("trk_bus");

    private readonly CommandRig _rig;

    public EffectCommandTests()
    {
        _rig = new CommandRig(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_a", "A"), BlankProject.Track("trk_bus", "Bus", TrackKind.Bus)] });
    }

    private ImmutableChain Chain(TrackId id = default) => new(_rig.Project.Tracks.First(track => track.Id == (id.Value.Length == 0 ? _track : id)).Effects);

    private EffectId Add(EffectKind kind, string? preset = null, TrackId? track = null)
    {
        return EffectId.From(_rig.Created(new AddEffect { Project = _rig.Id, Track = track ?? _track, Kind = kind, Preset = preset })[0]);
    }

    private sealed record ImmutableChain(ValueList<Effect> Effects)
    {
        public Effect this[int index] => Effects[index];

        public int Count => Effects.Count;

        public IEnumerable<string> Ids => Effects.Select(effect => effect.Id.Value);

        public T Get<T>(EffectId id)
            where T : Effect
        {
            return Assert.IsType<T>(Effects.First(effect => effect.Id == id));
        }
    }

    [Fact]
    public void EveryKindIsAddedAtTheEndWithItsDefaultsAndAServerAllocatedId()
    {
        EffectId[] ids = [.. Enum.GetValues<EffectKind>().Select(kind => Add(kind))];

        Assert.Equal("fxn_1001", ids[0].Value);
        Assert.Equal(ids.Select(id => id.Value), Chain().Ids);
        Assert.Equal(Enum.GetValues<EffectKind>(), Chain().Effects.Select(EffectRules.KindOf));
        Assert.All(Chain().Effects, effect => Assert.True(effect.Enabled));
    }

    [Fact]
    public void AnEffectCanStartFromAPresetAndABusHoldsEffectsToo()
    {
        EffectId id = Add(EffectKind.Reverb, "Hall", _bus);

        ReverbEffect reverb = Chain(_bus).Get<ReverbEffect>(id);
        Assert.Equal((3.5, 0.3), (reverb.Size, reverb.Mix));
    }

    [Fact]
    public void APresetOfAnotherKindOrAnUnknownTrackIsRefusedAndAFullChainIsOutOfRange()
    {
        _rig.RefusedWith(new AddEffect { Project = _rig.Id, Track = _track, Kind = EffectKind.Eq, Preset = "Hall" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddEffect { Project = _rig.Id, Track = TrackId.From("trk_none"), Kind = EffectKind.Eq }, CommandErrorCode.NotFound);
        for (int i = 0; i < ProjectLimits.MaxEffects; i++)
        {
            Add(EffectKind.Saturation);
        }

        CommandResult result = _rig.Refused(new AddEffect { Project = _rig.Id, Track = _track, Kind = EffectKind.Eq });

        Assert.Equal(CommandErrorCode.Invalid, result.Errors[0].Code);
        Assert.Contains("at most", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEffectIsRemovedAndComesBackWithItsPlaceOnUndo()
    {
        EffectId first = Add(EffectKind.Eq);
        EffectId second = Add(EffectKind.Compressor);
        Add(EffectKind.Reverb);
        ProjectDocument before = _rig.Project;

        _rig.Ok(new RemoveEffect { Project = _rig.Id, Track = _track, Effect = second });

        Assert.Equal(2, Chain().Count);
        Assert.DoesNotContain(second.Value, Chain().Ids);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
        Assert.Equal(first.Value, Chain()[0].Id.Value);
    }

    [Fact]
    public void AnEffectStepsUpAndDownTheChainAndNothingHappensAtTheEnds()
    {
        EffectId a = Add(EffectKind.Eq);
        EffectId b = Add(EffectKind.Compressor);
        EffectId c = Add(EffectKind.Delay);
        int steps = _rig.History.UndoCount;

        _rig.Ok(new MoveEffect { Project = _rig.Id, Track = _track, Effect = a, Direction = MoveDirection.Up });
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(new MoveEffect { Project = _rig.Id, Track = _track, Effect = a, Direction = MoveDirection.Down });
        Assert.Equal([b.Value, a.Value, c.Value], Chain().Ids);
        _rig.Ok(new MoveEffect { Project = _rig.Id, Track = _track, Effect = c, Direction = MoveDirection.Up });

        Assert.Equal([b.Value, c.Value, a.Value], Chain().Ids);
        _rig.History.Undo();
        _rig.History.Undo();
        Assert.Equal([a.Value, b.Value, c.Value], Chain().Ids);
    }

    [Fact]
    public void AnEffectIsBypassedAndPutBack()
    {
        EffectId id = Add(EffectKind.Eq);

        _rig.Ok(new SetEffectEnabled { Project = _rig.Id, Track = _track, Effect = id, Enabled = false });
        Assert.False(Chain()[0].Enabled);
        _rig.Ok(new SetEffectEnabled { Project = _rig.Id, Track = _track, Effect = id, Enabled = true });

        Assert.True(Chain()[0].Enabled);
    }

    [Fact]
    public void ASettingIsSetByNameWithinItsRange()
    {
        EffectId id = Add(EffectKind.Compressor);

        _rig.Ok(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "threshold", Value = -40 });

        Assert.Equal(-40, Chain().Get<CompressorEffect>(id).Threshold);
        foreach (double value in new[] { 1.0, -61.0, double.NaN, double.PositiveInfinity })
        {
            _rig.RefusedWith(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "threshold", Value = value }, CommandErrorCode.Invalid);
        }
    }

    [Fact]
    public void ASettingTheEffectDoesNotHaveIsRejectedAndTheNameIsCaseSensitive()
    {
        EffectId id = Add(EffectKind.Compressor);

        _rig.RefusedWith(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "midFreq", Value = 1000 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "Threshold", Value = -10 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = new string('x', 5000), Value = 1 }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void AKnobDragIsOneGestureAndEveryFrameSucceeds()
    {
        EffectId id = Add(EffectKind.Reverb);
        GestureId drag = GestureId.From("knob");
        int steps = _rig.History.UndoCount;

        foreach (double mix in new[] { 0.4, 0.5, 0.6, 0.7 })
        {
            Assert.True(_rig.Run(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "mix", Value = mix }, drag).Succeeded);
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(0.7, Chain().Get<ReverbEffect>(id).Mix);
        _rig.History.Undo();
        Assert.Equal(0.3, Chain().Get<ReverbEffect>(id).Mix);
    }

    [Fact]
    public void ADelayFollowsTheTempoWithADivisionAndOnlyADelayDoes()
    {
        EffectId delay = Add(EffectKind.Delay);
        EffectId eq = Add(EffectKind.Eq);

        _rig.Ok(new SetDelaySync { Project = _rig.Id, Track = _track, Effect = delay, Sync = true, Division = "1/8." });
        Assert.Equal((true, "1/8."), (Chain().Get<DelayEffect>(delay).Sync, Chain().Get<DelayEffect>(delay).Division));
        _rig.Ok(new SetDelaySync { Project = _rig.Id, Track = _track, Effect = delay, Sync = false });
        Assert.Equal((false, "1/8."), (Chain().Get<DelayEffect>(delay).Sync, Chain().Get<DelayEffect>(delay).Division));
        _rig.RefusedWith(new SetDelaySync { Project = _rig.Id, Track = _track, Effect = delay, Sync = true, Division = "1/3" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetDelaySync { Project = _rig.Id, Track = _track, Effect = eq, Sync = true }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void APresetReplacesTheSettingsAndKeepsThePlaceAndTheBypass()
    {
        Add(EffectKind.Eq);
        EffectId id = Add(EffectKind.Compressor);
        _rig.Ok(new SetEffectEnabled { Project = _rig.Id, Track = _track, Effect = id, Enabled = false });
        _rig.Ok(new SetEffectParameter { Project = _rig.Id, Track = _track, Effect = id, Parameter = "knee", Value = 5 });

        _rig.Ok(new ApplyEffectPreset { Project = _rig.Id, Track = _track, Effect = id, Preset = "Drum smash" });

        CompressorEffect compressor = Chain().Get<CompressorEffect>(id);
        Assert.Equal((-30.0, 8.0, 30.0, 6.0), (compressor.Threshold, compressor.Ratio, compressor.Knee, compressor.Makeup));
        Assert.False(compressor.Enabled);
        Assert.Equal(id.Value, Chain()[1].Id.Value);
    }

    [Fact]
    public void TheSameBadPresetIsTheSameClassOfErrorWhetherAddedOrApplied()
    {
        EffectId eq = Add(EffectKind.Eq);

        CommandErrorCode added = _rig.Refused(new AddEffect { Project = _rig.Id, Track = _track, Kind = EffectKind.Eq, Preset = "Hall" }).Errors[0].Code;
        CommandErrorCode applied = _rig.Refused(new ApplyEffectPreset { Project = _rig.Id, Track = _track, Effect = eq, Preset = "Hall" }).Errors[0].Code;

        Assert.Equal(CommandErrorCode.Rejected, added);
        Assert.Equal(added, applied);
    }

    [Fact]
    public void APresetOfAnotherKindOrNoneIsRejected()
    {
        EffectId id = Add(EffectKind.Compressor);

        _rig.RefusedWith(new ApplyEffectPreset { Project = _rig.Id, Track = _track, Effect = id, Preset = "Hall" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new ApplyEffectPreset { Project = _rig.Id, Track = _track, Effect = id, Preset = "" }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void AMissingTrackOrEffectIsNotFoundByEveryCommand()
    {
        EffectId id = Add(EffectKind.Eq);
        TrackId none = TrackId.From("trk_none");
        EffectId noFx = EffectId.From("fx_none");

        foreach ((TrackId track, EffectId effect) in new[] { (none, id), (_track, noFx) })
        {
            _rig.RefusedWith(new RemoveEffect { Project = _rig.Id, Track = track, Effect = effect }, CommandErrorCode.NotFound);
            _rig.RefusedWith(new MoveEffect { Project = _rig.Id, Track = track, Effect = effect, Direction = MoveDirection.Up }, CommandErrorCode.NotFound);
            _rig.RefusedWith(new SetEffectEnabled { Project = _rig.Id, Track = track, Effect = effect, Enabled = false }, CommandErrorCode.NotFound);
            _rig.RefusedWith(new SetEffectParameter { Project = _rig.Id, Track = track, Effect = effect, Parameter = "x", Value = double.NaN }, CommandErrorCode.NotFound);
            _rig.RefusedWith(new SetDelaySync { Project = _rig.Id, Track = track, Effect = effect, Sync = true }, CommandErrorCode.NotFound);
            _rig.RefusedWith(new ApplyEffectPreset { Project = _rig.Id, Track = track, Effect = effect, Preset = "x" }, CommandErrorCode.NotFound);
        }
    }

    [Fact]
    public void ARedoneEffectKeepsItsId()
    {
        EffectId id = Add(EffectKind.Eq);

        _rig.History.Undo();
        _rig.History.Redo();

        Assert.Equal(id, Chain()[0].Id);
    }
}
