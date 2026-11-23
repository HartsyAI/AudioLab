using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Live;

/// <summary>The metronome: a short sine click on every beat of the meter, louder and higher on the downbeat, placed by the tempo map.</summary>
/// <remarks>Live only: an export never has it (D35). Each click comes from the absolute frame, so any block size sounds the same.</remarks>
public sealed class ClickGenerator
{
    private const double Level = 0.35;
    private const double BurstSeconds = 0.03;
    private const double DecaySeconds = 0.006;
    private const double DownbeatHertz = 1500;
    private const double BeatHertz = 1000;

    private readonly TempoMap _tempo;
    private readonly int _rate;
    private readonly int _burstFrames;
    private readonly double _unit;
    private readonly long _clicksPerBar;

    /// <summary>Creates the click of a document at a sample rate.</summary>
    public ClickGenerator(ProjectDocument document, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, AudioFormat.MinSampleRate);
        _tempo = TempoMap.From(document);
        _rate = sampleRate;
        _burstFrames = (int)(BurstSeconds * sampleRate);
        _unit = MeterRules.ClickUnit(document.TimeSignature);
        _clicksPerBar = Math.Max(1, (long)Math.Round(MeterRules.BeatsPerBar(document.TimeSignature) / _unit));
    }

    /// <summary>Adds the clicks that sound in the two buffers, which hold the frames from <paramref name="start"/> on.</summary>
    public void Mix(long start, Span<float> left, Span<float> right)
    {
        long end = start + left.Length;
        double from = Math.Max(0, (start - _burstFrames) / (double)_rate);
        long click = Math.Max(0, (long)Math.Floor(_tempo.BeatsAt(from) / _unit) - 1);
        while (true)
        {
            long at = FrameOf(click);
            if (at >= end)
            {
                return;
            }
            if (at + _burstFrames > start)
            {
                AddBurst(click, at, start, left, right);
            }
            click++;
        }
    }

    private long FrameOf(long click) => (long)Math.Round(_tempo.TimeAtBeat(click * _unit) * _rate, MidpointRounding.AwayFromZero);

    private void AddBurst(long click, long at, long start, Span<float> left, Span<float> right)
    {
        double hertz = click % _clicksPerBar == 0 ? DownbeatHertz : BeatHertz;
        double step = 2 * Math.PI * hertz / _rate;
        double decay = 1 / (DecaySeconds * _rate);
        int first = (int)Math.Max(0, start - at);
        int last = (int)Math.Min(_burstFrames, start + left.Length - at);
        for (int i = first; i < last; i++)
        {
            float sample = (float)(Level * Math.Sin(step * i) * Math.Exp(-i * decay));
            left[(int)(at + i - start)] += sample;
            right[(int)(at + i - start)] += sample;
        }
    }
}
