using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

public sealed class TrackTextTests
{
    [Fact]
    public void TheSourceLineSaysWhereAnAudioTrackRecordsAndWhatAnInstrumentPlays()
    {
        Track audio = Song.NewTrack("a");
        Track stereo = audio with { Input = new TrackInput(null, InputChannelMode.Stereo) };
        Track single = audio with { Input = new TrackInput(null, InputChannelMode.Single, 3) };
        Track bus = audio with { Kind = TrackKind.Bus };
        Track keys = Song.WithMidi("k");

        Assert.Equal("In mono", TrackText.Source(audio));
        Assert.Equal("In stereo", TrackText.Source(stereo));
        Assert.Equal("In 3", TrackText.Source(single));
        Assert.Equal("Aux bus", TrackText.Source(bus));
        Assert.Equal(((SynthInstrument)keys.Instrument!).Preset, TrackText.Source(keys));
    }

    [Theory]
    [InlineData(0.0, "C")]
    [InlineData(-0.28, "L28")]
    [InlineData(1.0, "R100")]
    [InlineData(0.004, "C")]
    public void PanIsCentreOrASideAndAPercent(double pan, string expected)
    {
        Assert.Equal(expected, TrackText.Pan(pan));
    }

    [Fact]
    public void TheTimeRulerLabelsStayAtLeastSixtyFourPixelsApartAtAnyScale()
    {
        Assert.Equal(2, TimeTicks.Step(TimelineGeometry.Default));
        Assert.Equal(1, TimeTicks.Step(new TimelineGeometry(100)));
        Assert.Equal(3600, TimeTicks.Step(new TimelineGeometry(0.001)));
        Assert.Equal([0, 2, 4, 6], TimeTicks.Times(TimelineGeometry.Default, 6));
    }
}
