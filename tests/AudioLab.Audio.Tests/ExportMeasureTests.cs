using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class ExportMeasureTests
{
    private const int Rate = 8000;

    private static readonly MemoryMedia _media = new MemoryMedia()
        .Add("a", Signals.Sine(300, 1, Rate, 0.2))
        .Add("b", Signals.Sine(450, 1, Rate, 0.2, channels: 1));

    // Effects with tails, so that a measure that did not build them would get the length wrong.
    private static ProjectDocument Project() =>
        TestProject.Create(
            TestProject.Track("t1", TrackKind.Audio, TestProject.Clip("c1", "a", 0, 1)) with
            {
                Effects = [((DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("d"))) with { Time = 0.1, Feedback = 0.4, Mix = 0.5 }],
                Sends = [new Send(TrackId.From("rev"), 0.5, false, false)],
            },
            TestProject.Track("t2", TrackKind.Audio, TestProject.Clip("c2", "b", 0.2, 0.7)),
            TestProject.Track("rev", TrackKind.Bus) with
            {
                Effects = [((ReverbEffect)EffectRules.Create(EffectKind.Reverb, EffectId.From("r"))) with { Size = 0.3, Mix = 1 }],
            });

    [Theory]
    [InlineData(WavFormat.Pcm16)]
    [InlineData(WavFormat.Pcm24)]
    [InlineData(WavFormat.Float32)]
    public async Task TheMeasureOfAMixIsTheLengthOfTheFileThatIsWritten(WavFormat format)
    {
        WavOptions wav = new() { Format = format };
        OfflineOptions options = new() { SampleRate = Rate };
        AsyncOnlyStream file = new();

        ExportSize size = WavExporter.Measure(Project(), _media, wav, options);
        await WavExporter.WriteAsync(Project(), _media, file, wav, options);

        Assert.Equal(file.Length, size.Bytes);
        Assert.Equal(1, size.Files);
        Assert.True(size.Frames > Rate, "the tail of the effects is in the length");
    }

    [Fact]
    public async Task TheMeasureOfARangeWithoutATailIsTheLengthOfTheFileThatIsWritten()
    {
        WavOptions wav = new();
        OfflineOptions options = new() { SampleRate = Rate, Start = 0.25, End = 0.75, IncludeTail = false };
        AsyncOnlyStream file = new();

        ExportSize size = WavExporter.Measure(Project(), _media, wav, options);
        await WavExporter.WriteAsync(Project(), _media, file, wav, options);

        Assert.Equal(file.Length, size.Bytes);
        Assert.Equal(Rate / 2, size.Frames);
    }

    [Fact]
    public async Task TheMeasureOfStemsIsTheLengthOfTheArchiveThatIsWritten()
    {
        WavOptions wav = new() { Format = WavFormat.Pcm16 };
        OfflineOptions options = new() { SampleRate = Rate };
        AsyncOnlyStream archive = new();

        ExportSize size = StemExporter.Measure(Project(), _media, wav, options);
        await StemExporter.WriteZipAsync(Project(), _media, archive, wav, options);

        Assert.Equal(archive.Length, size.Bytes);
        Assert.Equal(3, size.Files);
    }

    [Fact]
    public void AnExportThatNoFormatCanHoldIsTooLargeBeforeAnythingIsPlayed()
    {
        MemoryMedia silence = new MemoryMedia().Add("z", Signals.Constant(0, 1, 48000));
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "z", 0, 1)));
        OfflineOptions huge = new() { SampleRate = 48000, End = 200000 };
        WavOptions wav = new() { Format = WavFormat.Pcm24 };

        Assert.Throws<ExportTooLargeException>(() => WavExporter.Measure(document, silence, wav, huge));
        Assert.Throws<ExportTooLargeException>(() => StemExporter.Measure(document, silence, wav, huge));
    }

    [Fact]
    public void MissingMediaFailsTheMeasureAsItFailsTheWrite()
    {
        Assert.Throws<MissingMediaException>(() => WavExporter.Measure(Project(), new MemoryMedia(), new WavOptions()));
    }

    [Theory]
    [InlineData("Song", "Song")]
    [InlineData("  padded  ", "padded")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("line\r\nbreak‮evil", "line__break_evil")]
    [InlineData("", "")]
    public void AUsersTextBecomesAFileNameThatHoldsNoPathOrControl(string text, string expected)
    {
        Assert.Equal(expected, ExportFileName.Clean(text, 100));
    }

    [Fact]
    public void ALongNameIsCutAtAWholeCharacter()
    {
        string name = new string('a', 98) + "\U0001F3B5\U0001F3B5";

        string cut = ExportFileName.Clean(name, 99);

        Assert.Equal(new string('a', 98), cut);
    }
}
