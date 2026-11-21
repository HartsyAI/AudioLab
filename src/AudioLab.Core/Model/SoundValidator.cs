namespace AudioLab.Core.Model;

/// <summary>The instrument and effect rules. The ranges are the ranges of the knobs that edit them.</summary>
internal static class SoundValidator
{
    /// <summary>Checks a synth or sampler against the ranges of the knobs that edit it.</summary>
    public static void CheckInstrument(Instrument instrument, string path, ProblemList problems)
    {
        switch (instrument)
        {
            case SynthInstrument synth:
                CheckSynth(synth, path, problems);
                break;
            case SamplerInstrument sampler:
                CheckSampler(sampler, path, problems);
                break;
            default:
                problems.Add($"{path}: {instrument.GetType().Name} is not an instrument version 4 knows.");
                break;
        }
    }

    /// <summary>Checks a track's insert chain: unique ids and every parameter inside its knob range.</summary>
    public static void CheckEffects(
        ValueList<Effect> effects,
        string trackPath,
        HashSet<string> effectIds,
        ProblemList problems)
    {
        foreach (Effect effect in effects)
        {
            string path = $"{trackPath}.effects[{effect.Id.Value}]";
            problems.Unique(effectIds, effect.Id.Value, $"{path}.id");
            switch (effect)
            {
                case EqEffect eq:
                    CheckEq(eq, path, problems);
                    break;
                case CompressorEffect compressor:
                    CheckCompressor(compressor, path, problems);
                    break;
                case ReverbEffect reverb:
                    CheckReverb(reverb, path, problems);
                    break;
                case DelayEffect delay:
                    CheckDelay(delay, path, problems);
                    break;
                case SaturationEffect saturation:
                    CheckSaturation(saturation, path, problems);
                    break;
                default:
                    problems.Add($"{path}: {effect.GetType().Name} is not an effect version 4 knows.");
                    break;
            }
        }
    }

    private static void CheckSynth(SynthInstrument synth, string path, ProblemList problems)
    {
        problems.Text(synth.Preset, ProjectLimits.MaxLabel, $"{path}.preset");
        problems.Defined(synth.Osc1, $"{path}.osc1");
        problems.Defined(synth.Osc2, $"{path}.osc2");
        problems.Positive(synth.Ratio2, 16, $"{path}.ratio2");
        problems.Range(synth.Mix2, 0, 1, $"{path}.mix2");
        problems.Range(synth.Detune, 0, 50, $"{path}.detune");
        problems.Range(synth.Cutoff, 100, 12000, $"{path}.cutoff");
        problems.Range(synth.Resonance, 0.3, 15, $"{path}.resonance");
        problems.Range(synth.FilterEnv, 0, 3, $"{path}.filterEnv");
        problems.Range(synth.Attack, 0.001, 2, $"{path}.attack");
        problems.Range(synth.Decay, 0.01, 3, $"{path}.decay");
        problems.Range(synth.Sustain, 0, 1, $"{path}.sustain");
        problems.Range(synth.Release, 0.01, 4, $"{path}.release");
        problems.Range(synth.Gain, 0, 1, $"{path}.gain");
    }

    private static void CheckSampler(SamplerInstrument sampler, string path, ProblemList problems)
    {
        problems.Text(sampler.Name, ProjectLimits.MaxName, $"{path}.name");
        problems.Text(sampler.SampleName, ProjectLimits.MaxName, $"{path}.sampleName", required: false);
        if (sampler.Sample is { } sample)
        {
            problems.Id(sample.Value, $"{path}.sample");
        }
        problems.Range(sampler.RootNote, 0, 127, $"{path}.rootNote");
        problems.Range(sampler.Attack, 0.001, 2, $"{path}.attack");
        problems.Range(sampler.Release, 0.01, 4, $"{path}.release");
        problems.Range(sampler.Gain, 0, 1, $"{path}.gain");
    }

    private static void CheckEq(EqEffect eq, string path, ProblemList problems)
    {
        problems.Range(eq.HpFreq, 20, 1000, $"{path}.hpFreq");
        problems.Range(eq.LowGain, -12, 12, $"{path}.lowGain");
        problems.Range(eq.LowFreq, 40, 600, $"{path}.lowFreq");
        problems.Range(eq.MidGain, -12, 12, $"{path}.midGain");
        problems.Range(eq.MidFreq, 200, 8000, $"{path}.midFreq");
        problems.Range(eq.MidQ, 0.3, 4, $"{path}.midQ");
        problems.Range(eq.HighGain, -12, 12, $"{path}.highGain");
        problems.Range(eq.HighFreq, 1000, 16000, $"{path}.highFreq");
        problems.Range(eq.LpFreq, 1000, 20000, $"{path}.lpFreq");
    }

    private static void CheckCompressor(CompressorEffect compressor, string path, ProblemList problems)
    {
        problems.Range(compressor.Threshold, -60, 0, $"{path}.threshold");
        problems.Range(compressor.Knee, 0, 40, $"{path}.knee");
        problems.Range(compressor.Ratio, 1, 20, $"{path}.ratio");
        problems.Range(compressor.Attack, 0.001, 1, $"{path}.attack");
        problems.Range(compressor.Release, 0.01, 1, $"{path}.release");
        problems.Range(compressor.Makeup, 0, 24, $"{path}.makeup");
    }

    private static void CheckReverb(ReverbEffect reverb, string path, ProblemList problems)
    {
        problems.Range(reverb.Size, 0.1, 8, $"{path}.size");
        problems.Range(reverb.Decay, 0.5, 6, $"{path}.decay");
        problems.Range(reverb.Predelay, 0, 0.2, $"{path}.predelay");
        problems.Range(reverb.Mix, 0, 1, $"{path}.mix");
    }

    private static void CheckDelay(DelayEffect delay, string path, ProblemList problems)
    {
        problems.Range(delay.Time, 0.02, 2, $"{path}.time");
        problems.Text(delay.Division, 8, $"{path}.division");
        problems.Range(delay.Feedback, 0, 0.9, $"{path}.feedback");
        problems.Range(delay.Tone, 500, 15000, $"{path}.tone");
        problems.Range(delay.Mix, 0, 1, $"{path}.mix");
    }

    private static void CheckSaturation(SaturationEffect saturation, string path, ProblemList problems)
    {
        problems.Range(saturation.Drive, 0, 1, $"{path}.drive");
        problems.Range(saturation.Trim, -12, 6, $"{path}.trim");
        problems.Range(saturation.Mix, 0, 1, $"{path}.mix");
    }
}
