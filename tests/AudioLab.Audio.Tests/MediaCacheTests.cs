using System.Diagnostics.CodeAnalysis;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MediaCacheTests
{
    private const int Rate = 8000;

    private sealed class CountingSource(MemoryMedia inner) : IMediaSource
    {
        public int Asked { get; private set; }

        public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio)
        {
            Asked++;
            return inner.TryGet(id, out audio);
        }
    }

    private static MediaId Id(string value) => MediaId.From(value);

    [Fact]
    public void AFileIsAskedForOnceAndResampledOnceWhateverHowManyPlansUseIt()
    {
        CountingSource source = new(new MemoryMedia().Add("m", Signals.Sine(300, 1, 11025)));
        MediaCache cache = new(source, Rate);

        PcmAudio? first = cache.Get(Id("m"));
        PcmAudio? second = cache.Get(Id("m"));

        Assert.Same(first, second);
        Assert.Equal(1, source.Asked);
        Assert.Equal(Rate, first!.SampleRate);
        Assert.Equal(Rate * 4L * 2, cache.Bytes);
    }

    [Fact]
    public void AMissingFileIsNotRememberedSoAFileThatArrivesLaterIsFound()
    {
        MemoryMedia media = new();
        MediaCache cache = new(media, Rate);

        Assert.Null(cache.Get(Id("late")));
        media.Add("late", Signals.Constant(1, 1, Rate));

        Assert.NotNull(cache.Get(Id("late")));
    }

    [Fact]
    public void TheLeastRecentlyUsedFilesGoWhenTheBoundIsPassedAndTheNewestStays()
    {
        MemoryMedia media = new MemoryMedia()
            .Add("a", Signals.Constant(1, 1, Rate))
            .Add("b", Signals.Constant(1, 1, Rate))
            .Add("c", Signals.Constant(1, 1, Rate));
        CountingSource source = new(media);
        long oneFile = Rate * 4L * 2;
        MediaCache cache = new(source, Rate, (2 * oneFile) + 1);

        cache.Get(Id("a"));
        cache.Get(Id("b"));
        cache.Get(Id("a"));
        cache.Get(Id("c"));
        int before = source.Asked;
        cache.Get(Id("a"));
        cache.Get(Id("b"));

        Assert.True(cache.Bytes <= (2 * oneFile) + 1);
        Assert.Equal(before + 1, source.Asked);
    }

    [Fact]
    public void ANewestFileStaysEvenWhenItAloneIsOverTheBound()
    {
        MediaCache cache = new(new MemoryMedia().Add("big", Signals.Constant(1, 2, Rate)), Rate, 10);

        Assert.NotNull(cache.Get(Id("big")));
        Assert.True(cache.Bytes > 10);
    }

    [Fact]
    public void RemoveMakesTheNextPlanAskTheSourceAgain()
    {
        CountingSource source = new(new MemoryMedia().Add("m", Signals.Constant(1, 1, Rate)));
        MediaCache cache = new(source, Rate);

        cache.Get(Id("m"));
        cache.Remove(Id("m"));
        cache.Get(Id("m"));

        Assert.Equal(2, source.Asked);
        Assert.Equal(Rate * 4L * 2, cache.Bytes);
    }

    [Fact]
    public void SamplesThatAreNotNumbersBecomeSilenceAndTheRestIsUntouched()
    {
        float[] samples = [0.25f, float.NaN, float.PositiveInfinity, -0.5f, float.NegativeInfinity];
        MediaCache cache = new(new MemoryMedia().Add("m", new PcmAudio(Rate, [samples])), Rate);

        PcmAudio audio = cache.Get(Id("m"))!;

        Assert.Equal([0.25f, 0, 0, -0.5f, 0], audio.Channel(0).ToArray());
    }

    [Fact]
    public void CleanAudioAtTheCacheRateIsKeptAsItIsWithoutACopy()
    {
        PcmAudio audio = Signals.Constant(0.5f, 1, Rate);

        Assert.Same(audio, new MediaCache(new MemoryMedia().Add("m", audio), Rate).Get(Id("m")));
    }

    [Fact]
    public void APlanRefusesACacheAtAnotherRate()
    {
        MemoryMedia media = new();
        MediaCache cache = new(media, 44100);

        Assert.Throws<ArgumentException>(() =>
            RenderPlan.Create(TestProject.Create(), media, new PlanOptions { SampleRate = Rate, Cache = cache }));
    }

    [Fact]
    public void TwoPlansThatShareACacheAskTheSourceOnce()
    {
        CountingSource source = new(new MemoryMedia().Add("m", Signals.Constant(1, 1, 11025)));
        MediaCache cache = new(source, Rate);
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)));

        RenderPlan.Create(document, source, new PlanOptions { SampleRate = Rate, Cache = cache });
        RenderPlan.Create(document, source, new PlanOptions { SampleRate = Rate, Cache = cache });

        Assert.Equal(1, source.Asked);
    }
}
