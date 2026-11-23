using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MixRenderTests
{
    private static readonly MemoryMedia _media = new MemoryMedia()
        .Add("low", Signals.Constant(0.2f, 1, Rendering.Rate))
        .Add("high", Signals.Constant(0.4f, 1, Rendering.Rate));

    private static Track Low() => TestProject.Track("t1", TrackKind.Audio, TestProject.Clip("c1", "low", 0, 1));

    private static Track High() => TestProject.Track("t2", TrackKind.Audio, TestProject.Clip("c2", "high", 0, 1));

    private static Track Bus() => TestProject.Track("b", TrackKind.Bus);

    private static Send To(string bus, double level = 0.5, bool pre = false, bool muted = false) =>
        new(TrackId.From(bus), level, pre, muted);

    private static float Heard(params Track[] tracks) =>
        Rendering.At(Rendering.Render(TestProject.Create(tracks), _media, end: 1), 0.5);

    [Fact]
    public void TracksAndTheirSendsSumAtTheMaster()
    {
        Assert.Equal(0.7f, Heard(Low() with { Sends = [To("b")] }, High(), Bus()), 5);
    }

    [Fact]
    public void ASoloSilencesTheOthersAndTheSendsOfThoseOthers()
    {
        Assert.Equal(0.4f, Heard(Low() with { Sends = [To("b")] }, High() with { Soloed = true }, Bus()), 5);
    }

    [Fact]
    public void ABusFedByASoloedTrackStaysAudible()
    {
        Assert.Equal(0.3f, Heard(Low() with { Soloed = true, Sends = [To("b")] }, High(), Bus()), 5);
    }

    [Fact]
    public void SoloingABusHearsWhatFeedsItAndNothingElse()
    {
        Assert.Equal(0.1f, Heard(Low() with { Sends = [To("b")] }, High(), Bus() with { Soloed = true }), 5);
    }

    [Fact]
    public void ASendFromATrackSilentUnderSoloStillSpeaksWhenItsBusIsSoloed()
    {
        float heard = Heard(Low() with { Sends = [To("b", 1)] }, High() with { Soloed = true }, Bus() with { Soloed = true });

        Assert.Equal(0.6f, heard, 5);
    }

    [Fact]
    public void APreFaderSendSkipsTheFaderAndAPostFaderSendFollowsIt()
    {
        float pre = Heard(Low() with { Volume = 0.5, Sends = [To("b", 1, pre: true)] }, Bus());
        float post = Heard(Low() with { Volume = 0.5, Sends = [To("b", 1)] }, Bus());

        Assert.Equal(0.3f, pre, 5);
        Assert.Equal(0.2f, post, 5);
    }

    [Fact]
    public void AMuteSilencesEvenAPreFaderSend()
    {
        Assert.Equal(0f, Heard(Low() with { Muted = true, Sends = [To("b", 1, pre: true)] }, Bus()), 5);
    }

    [Fact]
    public void AMutedBusIsSilentAndAMutedSendDeliversNothing()
    {
        Assert.Equal(0.2f, Heard(Low() with { Sends = [To("b")] }, Bus() with { Muted = true }), 5);
        Assert.Equal(0.2f, Heard(Low() with { Sends = [To("b", 0.5, muted: true)] }, Bus()), 5);
    }

    [Fact]
    public void AGroupSharesItsMuteSoloAndVolume()
    {
        GroupId group = GroupId.From("g");
        TrackGroup shared = new()
        {
            Id = group,
            Name = "g",
            Color = "#ffffff",
            Collapsed = false,
            Tint = false,
            Muted = false,
            Soloed = false,
            Volume = 0.5,
        };
        ProjectDocument document = TestProject.Create(Low() with { Group = group }, High()) with { Groups = [shared] };

        Assert.Equal(0.5f, Rendering.At(Rendering.Render(document, _media), 0.5), 5);
        Assert.Equal(0.4f, Rendering.At(Rendering.Render(document with { Groups = [shared with { Muted = true }] }, _media), 0.5), 5);
        Assert.Equal(0.1f, Rendering.At(Rendering.Render(document with { Groups = [shared with { Soloed = true }] }, _media), 0.5), 5);
    }

    [Fact]
    public void TheMasterVolumeScalesTheMix()
    {
        ProjectDocument document = TestProject.Create(Low(), High()) with { MasterVolume = 0.5 };

        Assert.Equal(0.3f, Rendering.At(Rendering.Render(document, _media), 0.5), 5);
    }
}
