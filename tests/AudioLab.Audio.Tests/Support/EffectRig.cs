using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>Runs one effect on a signal through the whole engine, so a test sees what an export would.</summary>
public static class EffectRig
{
    public static RenderedAudio Run(
        Effect effect,
        PcmAudio input,
        bool tail = false,
        double bpm = 120,
        params TempoChange[] tempoChanges)
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, (double)input.Frames / input.SampleRate)) with
        {
            Effects = [effect],
        };
        ProjectDocument document = TestProject.Create(track) with { Bpm = bpm, TempoMap = new ValueList<TempoChange>(tempoChanges) };

        return OfflineRenderer.Render(
            document,
            new MemoryMedia().Add("m", input),
            new OfflineOptions { SampleRate = input.SampleRate, IncludeTail = tail });
    }

    /// <summary>The level, in decibels, of a frequency in the second half of a render.</summary>
    public static double SettledDb(RenderedAudio audio, double hertz)
    {
        ReadOnlySpan<float> settled = audio.Left.AsSpan(audio.Frames / 2);
        return 20 * Math.Log10(Signals.Level(settled, hertz, audio.Format.SampleRate));
    }

    /// <summary>A sine at a level that is easy to read back: amplitude 0.1.</summary>
    public static PcmAudio Probe(double hertz, int rate = 48000, double seconds = 1) => Signals.Sine(hertz, seconds, rate, 0.1);

    /// <summary>The gain, in decibels, that an effect gives a probe tone.</summary>
    public static double GainDb(Effect effect, double hertz, int rate = 48000) =>
        SettledDb(Run(effect, Probe(hertz, rate)), hertz) - (20 * Math.Log10(0.1));

    /// <summary>A silent clip with unit impulses at the given times.</summary>
    public static PcmAudio Impulses(int rate, double seconds, params double[] times)
    {
        float[] samples = new float[(int)Math.Round(seconds * rate)];
        foreach (double time in times)
        {
            samples[(int)Math.Round(time * rate)] = 1;
        }
        return new PcmAudio(rate, [samples, samples]);
    }
}
