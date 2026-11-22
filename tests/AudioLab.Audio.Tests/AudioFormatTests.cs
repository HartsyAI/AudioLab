namespace AudioLab.Audio.Tests;

public sealed class AudioFormatTests
{
    [Theory]
    [InlineData(7999, 2)]
    [InlineData(384001, 2)]
    [InlineData(48000, 0)]
    [InlineData(48000, 9)]
    public void RefusesARateOrChannelCountTheEngineCannotRender(int sampleRate, int channels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFormat(sampleRate, channels));
    }

    [Fact]
    public void KeepsAValidRateAndChannelCount()
    {
        AudioFormat format = new(44100, 2);

        Assert.Equal(44100, format.SampleRate);
        Assert.Equal(2, format.Channels);
    }
}
