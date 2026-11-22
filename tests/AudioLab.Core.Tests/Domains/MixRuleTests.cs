using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

// Solo and mute through groups and aux buses: audit rows 26 and 27, which the one engine must keep (D5).
public sealed class MixRuleTests
{
    private static readonly GroupId _band = GroupId.From("grp_band");

    private static readonly TrackId _bus = TrackId.From("bus");

    private static TrackGroup Band(bool muted = false, bool soloed = false, double volume = 1)
    {
        return new TrackGroup { Id = _band, Name = "Band", Color = "#7a8cff", Collapsed = false, Tint = true, Muted = muted, Soloed = soloed, Volume = volume };
    }

    private static ProjectDocument Mix(Track[] tracks, params TrackGroup[] groups)
    {
        return BlankProject.Create() with { Tracks = [.. tracks], Groups = [.. groups] };
    }

    private static Track Source(string id, params Send[] sends) => BlankProject.Track(id) with { Sends = [.. sends] };

    private static Track Bus() => BlankProject.Track("bus", "Reverb", TrackKind.Bus);

    [Fact]
    public void AGroupMuteSoloAndFaderApplyToItsMembers()
    {
        Track member = BlankProject.Track("a") with { Group = _band, Volume = 0.5 };
        Track loose = BlankProject.Track("b") with { Volume = 0.5 };
        TrackGroup[] groups = [Band(muted: true, soloed: true, volume: 0.5)];

        Assert.True(MixRules.IsMuted(member, groups));
        Assert.False(MixRules.IsMuted(loose, groups));
        Assert.True(MixRules.IsSoloed(member, groups));
        Assert.False(MixRules.IsSoloed(loose, groups));
        Assert.Equal(0.25, MixRules.TrackLevel(member, groups));
        Assert.Equal(0.5, MixRules.TrackLevel(loose, groups));
    }

    [Fact]
    public void ANonsenseGroupVolumeNeverSilencesOrBoostsByAccident()
    {
        Track member = BlankProject.Track("a") with { Group = _band, Volume = 0.5 };

        Assert.Equal(0.5, MixRules.TrackLevel(member, [Band(volume: double.NaN)]));
        Assert.Equal(0, MixRules.TrackLevel(member, [Band(volume: -3)]));
    }

    [Fact]
    public void NothingIsSoloedSoEverythingIsHeard()
    {
        ProjectDocument document = Mix([Source("a"), Bus()]);

        Assert.False(MixRules.HasSolo(document));
        Assert.All(document.Tracks, track => Assert.True(MixRules.IsSoloAudible(document, track)));
    }

    [Fact]
    public void SoloSilencesTheOthersExceptABusThatASoloedTrackFeeds()
    {
        Send toBus = new(_bus, 0.5, false, false);
        ProjectDocument document = Mix([Source("a", toBus) with { Soloed = true }, Source("b", toBus), Bus()]);
        Track a = document.Tracks[0];
        Track b = document.Tracks[1];
        Track bus = document.Tracks[2];

        Assert.True(MixRules.IsSoloAudible(document, a));
        Assert.False(MixRules.IsSoloAudible(document, b));
        Assert.True(MixRules.IsSoloAudible(document, bus));
    }

    [Fact]
    public void ABusFedOnlyThroughAMutedSendOrAnUnsoloedTrackIsSilent()
    {
        Send muted = new(_bus, 0.5, false, true);
        ProjectDocument document = Mix([Source("a", muted) with { Soloed = true }, Source("b", new Send(_bus, 0.5, false, false)), Bus()]);

        Assert.False(MixRules.IsBusFedBySolo(document, document.Tracks[2]));
        Assert.False(MixRules.IsSoloAudible(document, document.Tracks[2]));
    }

    [Fact]
    public void SoloingAReturnLetsTheSendsIntoItSpeak()
    {
        Send toBus = new(_bus, 0.5, false, false);
        ProjectDocument document = Mix([Source("a", toBus), Source("b"), Bus() with { Soloed = true }]);
        Track a = document.Tracks[0];

        Assert.Equal(0.5, MixRules.SendGain(document, a, toBus));
        Assert.True(MixRules.IsSoloAudible(document, document.Tracks[2]));
        Assert.False(MixRules.IsSoloAudible(document, a));
    }

    [Fact]
    public void ASendUnderSoloIsSilentUnlessItsSourceOrItsBusIsSoloedOrTheExportIgnoresSolo()
    {
        Send toBus = new(_bus, 0.5, false, false);
        ProjectDocument document = Mix([Source("a", toBus), Source("b") with { Soloed = true }, Bus()]);
        Track a = document.Tracks[0];

        Assert.Equal(0, MixRules.SendGain(document, a, toBus));
        Assert.Equal(0.5, MixRules.SendGain(document, a, toBus, ignoreSolo: true));
        Assert.Equal(0.5, MixRules.SendGain(document, a with { Soloed = true }, toBus));
    }

    [Fact]
    public void AMutedSendOrAMutedSourceIsSilentWhateverItsLevelOrPlacement()
    {
        Send pre = new(_bus, 0.8, true, false);
        Send mutedSend = pre with { Muted = true };
        ProjectDocument document = Mix([Source("a", pre), Bus()]);
        Track a = document.Tracks[0];

        Assert.Equal(0.8, MixRules.SendGain(document, a, pre));
        Assert.Equal(0, MixRules.SendGain(document, a, mutedSend));
        Assert.Equal(0, MixRules.SendGain(document, a with { Muted = true }, pre));
        Assert.Equal(0, MixRules.SendGain(document with { Groups = [Band(muted: true)] }, a with { Group = _band }, pre));
    }
}
