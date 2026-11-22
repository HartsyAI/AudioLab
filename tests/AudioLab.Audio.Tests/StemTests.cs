using System.IO.Compression;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class StemTests
{
    private const int Rate = 8000;

    private static readonly MemoryMedia _media = new MemoryMedia()
        .Add("a", Signals.Sine(300, 1, Rate, 0.2))
        .Add("b", Signals.Sine(450, 1, Rate, 0.2, channels: 1))
        .Add("hit", EffectRig.Impulses(Rate, 1, 0.05));

    private static DelayEffect Delay() =>
        ((DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("d"))) with { Time = 0.1, Feedback = 0.4, Mix = 0.5 };

    private static ReverbEffect Room() =>
        ((ReverbEffect)EffectRules.Create(EffectKind.Reverb, EffectId.From("r"))) with { Size = 0.3, Mix = 1 };

    private static Send To(string bus, double level = 0.5, bool pre = false) => new(TrackId.From(bus), level, pre, false);

    // Two tracks, one with a delay that sends to a bus with a reverb and one that sends to a second bus, and a limiter that never acts.
    private static ProjectDocument Project() =>
        TestProject.Create(
            TestProject.Track("t1", TrackKind.Audio, TestProject.Clip("c1", "a", 0, 1)) with
            {
                Pan = -0.3,
                Volume = 0.8,
                Effects = [Delay()],
                Sends = [To("rev"), To("echo", 0.3, pre: true)],
            },
            TestProject.Track("t2", TrackKind.Audio, TestProject.Clip("c2", "b", 0.2, 0.7)) with
            {
                Pan = 0.5,
                Sends = [To("rev", 0.25)],
            },
            TestProject.Track("rev", TrackKind.Bus) with { Effects = [Room()], Volume = 0.9 },
            TestProject.Track("echo", TrackKind.Bus)) with
        {
            MasterLimiter = true,
        };

    private static OfflineOptions Options() => new() { SampleRate = Rate };

    [Fact]
    public void TheStemsAddUpToTheMixSampleForSample()
    {
        ProjectDocument document = Project();

        RenderedAudio mix = OfflineRenderer.Render(document, _media, Options());
        IReadOnlyList<Stem> stems = StemExporter.Render(document, _media, Options());

        Assert.Equal(4, stems.Count);
        Assert.All(stems, stem => Assert.Equal(mix.Frames, stem.Audio.Frames));
        for (int i = 0; i < mix.Frames; i++)
        {
            Assert.Equal(mix.Left[i], stems.Sum(stem => stem.Audio.Left[i]), 1e-6);
            Assert.Equal(mix.Right[i], stems.Sum(stem => stem.Audio.Right[i]), 1e-6);
        }
    }

    [Fact]
    public void AStemIsTheTrackAloneAndDryAndABusStemIsWhatItsSendersFeedIt()
    {
        ProjectDocument document = Project();
        IReadOnlyList<Stem> stems = StemExporter.Render(document, _media, Options());
        Stem dry = stems.Single(stem => stem.Name == "t1");
        Stem echo = stems.Single(stem => stem.Name == "echo");

        // The pre-fader send of t1 into "echo" is 0.3 of the track after its delay, before its fader and pan.
        RenderedAudio alone = OfflineRenderer.Render(
            document with { Tracks = new ValueList<Track>([document.Tracks[0] with { Sends = [] }]) },
            _media,
            Options() with { TailFrames = dry.Audio.Frames - 8000 });

        Assert.Equal(alone.Left, dry.Audio.Left);
        Assert.True(Signals.Peak(echo.Audio.Left) > 0);
        Assert.Equal(dry.Audio.Frames, echo.Audio.Frames);
    }

    [Fact]
    public void ASoloOfAnotherTrackDoesNotSilenceAStemAndASoloedBusStillHasItsStem()
    {
        ProjectDocument document = Project();
        ProjectDocument soloed = document with
        {
            Tracks = new ValueList<Track>([document.Tracks[0], document.Tracks[1] with { Soloed = true }, document.Tracks[2], document.Tracks[3]]),
        };

        Stem first = StemExporter.Render(soloed, _media, Options(), [TrackId.From("t1")]).Single();
        Stem plain = StemExporter.Render(document, _media, Options(), [TrackId.From("t1")]).Single();
        ProjectDocument busSoloed = document with
        {
            Tracks = new ValueList<Track>([document.Tracks[0], document.Tracks[1], document.Tracks[2] with { Soloed = true }, document.Tracks[3]]),
        };
        Stem bus = StemExporter.Render(busSoloed, _media, Options(), [TrackId.From("rev")]).Single();

        Assert.Equal(plain.Audio.Left, first.Audio.Left);
        Assert.True(Signals.Peak(bus.Audio.Left) > 0);
    }

    [Fact]
    public void AMutedTrackHasNoStemAndTheMasterIsLeftOut()
    {
        ProjectDocument document = Project() with { MasterVolume = 0.5 };
        ProjectDocument muted = document with
        {
            Tracks = new ValueList<Track>([document.Tracks[0] with { Muted = true }, document.Tracks[1], document.Tracks[2], document.Tracks[3]]),
        };

        IReadOnlyList<TrackId> ids = StemExporter.StemTracks(muted);
        Stem t2 = StemExporter.Render(document, _media, Options(), [TrackId.From("t2")]).Single();
        Stem silent = StemExporter.Render(muted, _media, Options(), [TrackId.From("t1")]).Single();

        Assert.DoesNotContain(TrackId.From("t1"), ids);
        Assert.Equal(0, Signals.Peak(silent.Audio.Left.AsSpan(0, 8000)));
        // The track's own level, not the master's: a mono sine of 0.2 panned right by the mono law, untouched by the master volume of 0.5.
        Assert.Equal(0.2 * Math.Sin(0.75 * Math.PI / 2), Signals.Peak(t2.Audio.Right), 2);
    }

    [Fact]
    public void ABusStemRingsAfterTheEffectsOfTheTracksThatFeedItAndEveryStemRunsAsLong()
    {
        ProjectDocument document = TestProject.Create(
            TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "hit", 0, 1)) with
            {
                Effects = [Delay() with { Time = 0.5, Feedback = 0.5 }],
                Sends = [To("b", 1)],
            },
            TestProject.Track("b", TrackKind.Bus));

        IReadOnlyList<Stem> stems = StemExporter.Render(document, _media, Options());
        Stem bus = stems.Single(stem => stem.Name == "b");

        // The delay rings for ten repeats of half a second, and the bus carries them.
        Assert.True(bus.Audio.Frames > 5 * Rate);
        Assert.True(Signals.Peak(bus.Audio.Left.AsSpan(4 * Rate)) > 0);
        Assert.All(stems, stem => Assert.Equal(bus.Audio.Frames, stem.Audio.Frames));
    }

    [Fact]
    public void ThereIsAStemForEachTrackWithClipsAndEachBusThatIsFed()
    {
        ProjectDocument document = Project();
        Track unfed = TestProject.Track("lonely", TrackKind.Bus);
        Track empty = TestProject.Track("empty");
        ProjectDocument more = document with { Tracks = new ValueList<Track>([.. document.Tracks, unfed, empty]) };

        Assert.Equal(["t1", "t2", "rev", "echo"], StemExporter.StemTracks(more).Select(id => id.Value));
    }

    [Fact]
    public void TheZipHoldsAWavForEachStemNamedByItsPlaceAndItsTracksName()
    {
        ProjectDocument document = Project() with { MasterLimiter = false };
        using MemoryStream zip = new();

        StemExporter.WriteZip(document, _media, zip, new WavOptions { Format = WavFormat.Pcm16 }, Options());
        zip.Position = 0;
        using ZipArchive archive = new(zip, ZipArchiveMode.Read);

        Assert.Equal(["01 t1.wav", "02 t2.wav", "03 rev.wav", "04 echo.wav"], archive.Entries.Select(entry => entry.FullName));
        RenderedAudio expected = StemExporter.Render(document, _media, Options())[0].Audio;
        using Stream first = archive.Entries[0].Open();
        using MemoryStream bytes = new();
        first.CopyTo(bytes);
        WavFile file = new(bytes.ToArray());
        Assert.Equal(expected.Frames, file.Frames);
        Assert.Equal(16, file.Bits);
    }

    [Fact]
    public void EachStemInAZipIsDitheredWithNoiseOfItsOwn()
    {
        MemoryMedia silence = new MemoryMedia().Add("z", Signals.Constant(0, 1, Rate));
        ProjectDocument document = TestProject.Create(
            TestProject.Track("one", TrackKind.Audio, TestProject.Clip("c1", "z", 0, 1)),
            TestProject.Track("two", TrackKind.Audio, TestProject.Clip("c2", "z", 0, 1)));
        using MemoryStream zip = new();

        StemExporter.WriteZip(document, silence, zip, new WavOptions { Format = WavFormat.Pcm16, Dither = DitherKind.Triangular }, Options());
        zip.Position = 0;
        using ZipArchive archive = new(zip, ZipArchiveMode.Read);
        byte[][] data = [.. archive.Entries.Select(entry =>
        {
            using Stream stream = entry.Open();
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        })];

        Assert.Equal(data[0].Length, data[1].Length);
        Assert.NotEqual(data[0], data[1]);
    }

    [Fact]
    public void TheZipIsTheSameEveryTime()
    {
        ProjectDocument document = Project();
        using MemoryStream one = new();
        using MemoryStream two = new();

        StemExporter.WriteZip(document, _media, one, new WavOptions(), Options());
        StemExporter.WriteZip(document, _media, two, new WavOptions(), Options());

        Assert.Equal(one.ToArray(), two.ToArray());
    }

    [Theory]
    [InlineData(1, "Lead vocal", "01 Lead vocal.wav")]
    [InlineData(12, "a/b\\c:d*e?f\"g<h>i|j", "12 a_b_c_d_e_f_g_h_i_j.wav")]
    [InlineData(3, "   ", "03 Track 3.wav")]
    [InlineData(4, "tab\there", "04 tab_here.wav")]
    public void AFileNameLosesWhatAFileSystemCannotHold(int number, string name, string expected)
    {
        Assert.Equal(expected, StemExporter.FileName(number, name));
    }

    [Fact]
    public void AVeryLongNameIsCutShort()
    {
        Assert.Equal(100 + "01 .wav".Length, StemExporter.FileName(1, new string('x', 500)).Length);
    }

    [Fact]
    public void AStreamedExportIsByteForByteTheWavOfTheRender()
    {
        ProjectDocument document = Project();
        OfflineOptions options = Options();
        WavOptions wav = new() { Format = WavFormat.Pcm24, Dither = DitherKind.Triangular, Seed = 7 };
        using MemoryStream streamed = new();
        using MemoryStream buffered = new();

        WavExporter.Write(document, _media, streamed, wav, options);
        WavExporter.Write(OfflineRenderer.Render(document, _media, options), buffered, wav);

        Assert.Equal(buffered.ToArray(), streamed.ToArray());
    }

    [Fact]
    public void ASeparateStemOfATrackThatIsNotThereIsAnError()
    {
        Assert.Throws<ArgumentException>(() => StemExporter.Render(Project(), _media, Options(), [TrackId.From("nope")]));
    }
}
