using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MediaCacheSliceTests
{
    private const int Rate = 8000;

    private static Task Pause(CancellationToken cancellationToken) => Task.CompletedTask;

    [Fact]
    public async Task AddingDecodedAudioGivesTheAudioAPlanWouldGetAndPausesWhileItWorks()
    {
        PcmAudio source = Signals.Sine(300, 3, 11025);
        MediaCache whole = new(new MemoryMedia().Add("m", source), Rate);
        MediaCache sliced = new(new MemoryMedia(), Rate);
        int pauses = 0;

        PcmAudio expected = whole.Get(MediaId.From("m"))!;
        PcmAudio added = await sliced.AddAsync(MediaId.From("m"), source, _ => Task.FromResult(++pauses), CancellationToken.None);

        Assert.True(pauses > 2);
        Assert.Same(added, sliced.Get(MediaId.From("m")));
        Assert.Equal(expected.Frames, added.Frames);
        for (int channel = 0; channel < 2; channel++)
        {
            Assert.True(expected.Channel(channel).SequenceEqual(added.Channel(channel)));
        }
    }

    [Fact]
    public async Task AddingAudioWithASampleThatIsNotANumberKeepsSilenceInItsPlace()
    {
        float[] samples = [.. Enumerable.Repeat(0.25f, 100000)];
        samples[50000] = float.NaN;
        MediaCache cache = new(new MemoryMedia(), Rate);

        PcmAudio added = await cache.AddAsync(MediaId.From("m"), Signals.Stereo(samples, samples, Rate), Pause, CancellationToken.None);

        Assert.Equal(0, added.Channel(0)[50000]);
        Assert.Equal(0.25f, added.Channel(1)[49999]);
    }
}
