using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class ClipPlacementTests
{
    private const int Rate = Rendering.Rate;

    private static RenderedAudio Render(AudioClip clip, PcmAudio audio, double? end = null) =>
        Rendering.Render(TestProject.Create(TestProject.Track("t", TrackKind.Audio, clip)), new MemoryMedia().Add("m", audio), Rate, end);

    [Fact]
    public void AClipStartsWhereItIsPlacedAndEndsWhereItsVisiblePartEnds()
    {
        RenderedAudio audio = Render(TestProject.Clip("c", "m", 0.25, 0.5), Signals.Constant(1, 1, Rate));

        Assert.Equal(0f, audio.Left[1999]);
        Assert.Equal(1f, audio.Left[2000]);
        Assert.Equal(1f, audio.Left[5999]);
        Assert.Equal(6000, audio.Frames);
    }

    [Fact]
    public void AnOffsetStartsInsideTheFileAndATrimEndLeavesTheEndOut()
    {
        float[] ramp = Enumerable.Range(0, Rate).Select(i => i * 1e-4f).ToArray();
        AudioClip clip = TestProject.Clip("c", "m", 0, 0.5, offset: 0.25) with { TrimEnd = 0.25 };

        RenderedAudio audio = Render(clip, Signals.Stereo(ramp, ramp, Rate));

        Assert.Equal(2000 * 1e-4f, audio.Left[0], 6);
        Assert.Equal(2100 * 1e-4f, audio.Left[100], 6);
        Assert.Equal(2000, audio.Frames);
    }

    [Fact]
    public void TheClipGainScalesAndFadesRampLinearlyOverTheirLength()
    {
        AudioClip clip = TestProject.Clip("c", "m", 0, 1, gain: 0.5) with { FadeIn = 0.1, FadeOut = 0.1 };

        RenderedAudio audio = Render(clip, Signals.Constant(1, 1, Rate));

        Assert.Equal(0f, audio.Left[0]);
        Assert.Equal(0.25f, audio.Left[400], 6);
        Assert.Equal(0.5f, audio.Left[800], 6);
        Assert.Equal(0.5f, audio.Left[4000], 6);
        Assert.Equal(0.5f / 800, audio.Left[7999], 6);
    }

    [Fact]
    public void AMutedClipPlaysNothing()
    {
        RenderedAudio audio = Render(TestProject.Clip("c", "m", 0, 1) with { Muted = true }, Signals.Constant(1, 1, Rate), end: 1);

        Assert.Equal(0, Signals.Peak(audio.Left));
    }

    [Fact]
    public void MediaShorterThanTheClipClaimsIsSilentPastItsEnd()
    {
        RenderedAudio audio = Render(TestProject.Clip("c", "m", 0, 1), Signals.Constant(1, 0.5, Rate));

        Assert.Equal(1f, audio.Left[3999]);
        Assert.Equal(0f, audio.Left[4000]);
        Assert.Equal(Rate, audio.Frames);
    }

    [Fact]
    public void AMonoFileSpeaksOnBothChannelsAndAStereoFileKeepsItsChannels()
    {
        RenderedAudio mono = Render(TestProject.Clip("c", "m", 0, 1), Signals.Constant(0.5f, 1, Rate, channels: 1));
        float[] left = Enumerable.Repeat(0.25f, Rate).ToArray();
        RenderedAudio stereo = Render(TestProject.Clip("c", "m", 0, 1), Signals.Stereo(left, new float[Rate], Rate));

        Assert.Equal(0.5f * (float)Math.Cos(Math.PI / 4), mono.Left[100], 6);
        Assert.Equal(mono.Left[100], mono.Right[100]);
        Assert.Equal(0.25f, stereo.Left[100], 6);
        Assert.Equal(0f, stereo.Right[100]);
    }

    [Fact]
    public void ARenderCanStartPartWayAndEndBeforeTheContent()
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)));
        MemoryMedia media = new MemoryMedia().Add("m", Signals.Sine(300, 1, Rate));

        RenderedAudio whole = Rendering.Render(document, media);
        RenderedAudio part = OfflineRenderer.Render(
            document,
            media,
            new OfflineOptions { SampleRate = Rate, Start = 0.5, End = 0.75, IncludeTail = false });

        Assert.Equal(2000, part.Frames);
        Assert.Equal(whole.Left.AsSpan(4000, 2000).ToArray(), part.Left);
    }
}
