using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class PanAndAutomationTests
{
    private static readonly MemoryMedia _media = new MemoryMedia().Add("m", Signals.Constant(1, 2, Rendering.Rate, channels: 2));

    private static Track Track(double pan = 0) =>
        TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 2)) with { Pan = pan };

    private static RenderedAudio Render(Track track) => Rendering.Render(TestProject.Create(track), _media);

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 2, 0)]
    [InlineData(1, 0, 2)]
    [InlineData(0.5, 0.70710678, 1.70710678)]
    [InlineData(-0.5, 1.70710678, 0.70710678)]
    public void ThePanFollowsTheEqualPowerLawOfTheWebAudioStereoPanner(double pan, float left, float right)
    {
        RenderedAudio audio = Render(Track(pan));

        Assert.Equal(left, audio.Left[100], 5);
        Assert.Equal(right, audio.Right[100], 5);
    }

    [Fact]
    public void AVolumeLaneMultipliesTheFaderAndIsAStraightLineBetweenItsPoints()
    {
        Track track = Track() with
        {
            Volume = 0.5,
            Automation = new TrackAutomation
            {
                Mode = AutomationMode.Read,
                Volume = new ValueList<AutomationPoint>([new(0, 0), new(1, 1), new(2, 0)]),
                Pan = [],
            },
        };

        RenderedAudio audio = Render(track);

        Assert.Equal(0.25f, audio.Left[Rendering.Rate / 2], 4);
        Assert.Equal(0.5f, audio.Left[Rendering.Rate], 4);
        Assert.Equal(0.25f, audio.Left[Rendering.Rate * 3 / 2], 4);
    }

    [Fact]
    public void APanLaneOffsetsTheTrackPanAndIsClamped()
    {
        Track track = Track(0.5) with
        {
            Automation = new TrackAutomation
            {
                Mode = AutomationMode.Read,
                Volume = [],
                Pan = new ValueList<AutomationPoint>([new(0, 0), new(1, 1)]),
            },
        };

        RenderedAudio audio = Render(track);

        Assert.Equal(0.70710678f, audio.Left[0], 4);
        Assert.Equal(0f, audio.Left[Rendering.Rate * 3 / 2], 4);
        Assert.Equal(2f, audio.Right[Rendering.Rate * 3 / 2], 4);
    }

    [Fact]
    public void WithAutomationOffTheLanesAreIgnored()
    {
        Track track = Track() with
        {
            Automation = new TrackAutomation
            {
                Mode = AutomationMode.Off,
                Volume = new ValueList<AutomationPoint>([new(0, 0)]),
                Pan = new ValueList<AutomationPoint>([new(0, 1)]),
            },
        };

        RenderedAudio audio = Render(track);

        Assert.Equal(1f, audio.Left[100], 5);
        Assert.Equal(1f, audio.Right[100], 5);
    }

    [Theory]
    [InlineData(0, 0.70710678, 0.70710678)]
    [InlineData(-1, 1, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(-0.5, 0.92387953, 0.38268343)]
    public void AMonoTrackIsSharedOutByAConstantPowerLawThreeDecibelsDownAtTheCentre(double pan, float left, float right)
    {
        MemoryMedia mono = new MemoryMedia().Add("m", Signals.Constant(1, 2, Rendering.Rate, channels: 1));

        RenderedAudio audio = Rendering.Render(TestProject.Create(Track(pan)), mono);

        Assert.Equal(left, audio.Left[100], 5);
        Assert.Equal(right, audio.Right[100], 5);
    }
}
