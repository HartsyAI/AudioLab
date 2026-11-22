using System.Diagnostics;
using System.Runtime.InteropServices;
#if HI_RESAMPLER || HI_WAV
using HartsyInference.Audio.Io;
#endif
#if HI_RESAMPLER
using HartsyInference.Audio.Streaming;
#endif
#if HI_FFT || HI_OTHER
using HartsyInference.Audio.Preprocessing;
#endif
#if HI_OTHER
using HartsyInference.Audio.Dsp;
#endif

namespace O9;

/// <summary>Runs every API on a known signal, asserts its output, and times it against the straightforward version, interleaved.</summary>
public sealed class Runner(Action<string> log, double seconds, int rounds)
{
    private static byte[]? _keep;

    public int Failures { get; private set; }

    public void RunAll()
    {
        Env();
#if OWN
        ResamplerCases();
        WavCases();
        FftCases();
        OtherCases();
#endif
        log($"DONE|{Failures} failed assertions");
    }

    private void Env()
    {
        log($"ENV|{RuntimeInformation.FrameworkDescription}|{RuntimeInformation.RuntimeIdentifier}|browser={OperatingSystem.IsBrowser()}"
            + $"|Vector.IsHardwareAccelerated={System.Numerics.Vector.IsHardwareAccelerated}|Vector<float>.Count={System.Numerics.Vector<float>.Count}"
            + $"|Vector128.IsHardwareAccelerated={System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated}"
            + $"|audioSeconds={seconds}|rounds={rounds}");
        long before = GC.GetAllocatedBytesForCurrentThread();
        _keep = new byte[1 << 20];
        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Check("allocation counter sees a 1 MiB array", delta >= 1 << 20 && delta < (1 << 20) + 4096, $"delta={delta}");
    }

    private void Check(string name, bool ok, string detail)
    {
        if (!ok)
        {
            Failures++;
        }
        log($"{(ok ? "PASS" : "FAIL")}|{name}|{detail}");
    }

    private void Time(string api, string caseName, double audioSeconds, params (string Impl, Action Run)[] impls)
    {
        foreach ((string _, Action run) in impls)
        {
            run();
            run();
        }
        long[] alloc = new long[impls.Length];
        for (int i = 0; i < impls.Length; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            impls[i].Run();
            alloc[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        List<double>[] ms = Enumerable.Range(0, impls.Length).Select(_ => new List<double>()).ToArray();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < impls.Length; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                impls[i].Run();
                ms[i].Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
        }
        double ownMedian = Median(ms[0]);
        for (int i = 0; i < impls.Length; i++)
        {
            double med = Median(ms[i]);
            double perSecond = audioSeconds > 0 ? med / audioSeconds : double.NaN;
            log($"RESULT|{api}|{caseName}|{impls[i].Impl}|median_ms={med:F3}|min_ms={ms[i].Min():F3}|ms_per_audio_s={perSecond:F3}"
                + $"|alloc_bytes_per_run={alloc[i]}|ratio_vs_first={med / ownMedian:F2}");
        }
    }

    private static double Median(List<double> v)
    {
        double[] s = v.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    private void ResamplerCases()
    {
        (int In, int Out, int Taps)[] cases = [(48000, 44100, 64), (44100, 48000, 64), (48000, 44100, 256), (48000, 16000, 64)];
        foreach ((int inRate, int outRate, int taps) in cases)
        {
            string name = $"{inRate}->{outRate} taps={taps}";
            float[] input = Signals.Mix(inRate, seconds);
            OwnResampler own = new(inRate, outRate, taps);
            float[] ownOut = new float[own.OutputLength(input.Length)];
            own.Resample(input, ownOut);
            float[] tone = Signals.Sine(inRate, 1.0, 1000);
            float[] ideal = Signals.Sine(outRate, 1.0, 1000);
            float[] ownTone = new float[own.OutputLength(tone.Length)];
            own.Resample(tone, ownTone);
            int edge = taps * 4;
            // The ideal is the true sine at the output rate. With an integer decimation (up == 1) both filters are centred half an
            // input sample early, so that check cannot pass: it is reported as a finding, and the delay is fitted and checked.
            bool integerDecimation = inRate % outRate == 0;
            float ownErr = Signals.MaxAbsDiff(ownTone, ideal, edge, ownTone.Length - edge);
            if (integerDecimation)
            {
                CheckDelay($"own resample {name}", ownTone, outRate, inRate, edge, ownErr);
            }
            else
            {
                Check($"own resample {name} 1 kHz tone matches the true ideal sine", ownErr < 5e-3f, $"max error {ownErr:E2}");
            }
            List<(string, Action)> impls = [("own", () => own.Resample(input, ownOut))];
#if HI_RESAMPLER
            Resampler hi = Resampler.Create(inRate, outRate, taps);
            float[] hiOut = new float[hi.OutputLength(input.Length)];
            hi.Resample(input, hiOut);
            Check($"HI Resampler {name} output length", hiOut.Length == ownOut.Length, $"{hiOut.Length} vs {ownOut.Length}");
            float diff = Signals.MaxAbsDiff(hiOut, ownOut);
            Check($"HI Resampler {name} equals own within 1e-5", diff < 1e-5f, $"max diff {diff:E2}");
            float[] hiTone = hi.Resample(tone);
            float hiErr = Signals.MaxAbsDiff(hiTone, ideal, edge, hiTone.Length - edge);
            if (integerDecimation)
            {
                CheckDelay($"HI Resampler {name}", hiTone, outRate, inRate, edge, hiErr);
            }
            else
            {
                Check($"HI Resampler {name} 1 kHz tone matches the true ideal sine", hiErr < 5e-3f, $"max error {hiErr:E2}");
            }
            if (taps == 64 && inRate == 48000 && outRate == 44100)
            {
                float[] slice = new float[2000];
                hi.ResampleRange(input, 1000, slice);
                float rd = Signals.MaxAbsDiff(slice, hiOut.AsSpan(1000, 2000));
                Check("HI Resampler.ResampleRange equals Resample on the slice", rd < 1e-5f, $"max diff {rd:E2}");
            }
            impls.Add(("HartsyInference", () => hi.Resample(input, hiOut)));
#endif
            if (taps == 64 && inRate == 48000 && outRate == 44100)
            {
                float[] lin = new float[ownOut.Length];
                impls.Add(("linear (not the same operation)", () => OwnResampler.Linear(input, lin, inRate, outRate)));
            }
            Time("Resampler.Resample", name, seconds, impls.ToArray());
        }
#if HI_RESAMPLER
        Time("Resampler.Create", "48000->44100 taps=64", 0,
            ("own", () => _ = new OwnResampler(48000, 44100, 64)), ("HartsyInference", () => _ = Resampler.Create(48000, 44100, 64)));
        StreamingCase();
#else
        Time("Resampler.Create", "48000->44100 taps=64", 0, ("own", () => _ = new OwnResampler(48000, 44100, 64)));
#endif
    }

#if HI_RESAMPLER
    private void StreamingCase()
    {
        const int frame = 480;
        const int outFrame = 441;
        float[] input = Signals.Mix(48000, seconds);
        int frames = input.Length / frame;
        float[] outAll = new float[frames * outFrame];
        StreamingResampler sr = new(48000, 44100, frame);
        for (int f = 0; f < frames; f++)
        {
            sr.Process(input.AsSpan(f * frame, frame), outAll.AsSpan(f * outFrame, outFrame));
        }
        float[] whole = Resampler.Create(48000, 44100, 64).Resample(input);
        float worst = 0;
        for (int f = 3; f < frames - 3; f++)
        {
            worst = Math.Max(worst, Signals.MaxAbsDiff(outAll.AsSpan(f * outFrame, outFrame), whole.AsSpan((f - 1) * outFrame, outFrame)));
        }
        Check("HI StreamingResampler 480-frame blocks equal the whole-buffer output, one frame late", worst < 1e-5f, $"max diff {worst:E2}");
        long before = GC.GetAllocatedBytesForCurrentThread();
        sr.Process(input.AsSpan(0, frame), outAll.AsSpan(0, outFrame));
        Check("HI StreamingResampler.Process allocates nothing", GC.GetAllocatedBytesForCurrentThread() - before == 0,
            $"{GC.GetAllocatedBytesForCurrentThread() - before} bytes");
        float[] whole2 = new float[whole.Length];
        Resampler hi = Resampler.Create(48000, 44100, 64);
        OwnResampler own = new(48000, 44100, 64);
        float[] ownOut = new float[whole.Length];
        Time("StreamingResampler.Process", "48000->44100 480-frame blocks", seconds,
            ("own (whole buffer, for scale)", () => own.Resample(input, ownOut)),
            ("HartsyInference streaming", () =>
            {
                for (int f = 0; f < frames; f++)
                {
                    sr.Process(input.AsSpan(f * frame, frame), outAll.AsSpan(f * outFrame, outFrame));
                }
            }),
            ("HartsyInference whole buffer", () => hi.Resample(input, whole2)));
    }
#endif

    private void WavCases()
    {
        const int rate = 44100;
        float[][] stereo = [Signals.Sine(rate, seconds, 440, 0.6), Signals.Sine(rate, seconds, 660, 0.4)];
        byte[] pcm16 = OwnWav.Build(stereo, rate, 16, false);
        (float[][] ownCh, int ownRate) = OwnWav.Read(pcm16);
        Check("own wav reads back the rate", ownRate == rate, $"{ownRate}");
        float[] mono = Signals.Sine(rate, 1.0, 1000, 0.7);
        byte[] pcm24 = OwnWav.Build([mono], rate, 24, false);
        byte[] f32 = OwnWav.Build([mono], rate, 32, true);
        Check("own wav duration", OwnWav.TryDuration(pcm16, out double d0) && Math.Abs(d0 - seconds) < 1e-9, $"{d0}");
        List<(string, Action)> read = [("own", () => OwnWav.Read(pcm16))];
        List<(string, Action)> write = [("own", () => OwnWav.WriteMono16(mono.AsSpan(), rate))];
#if HI_WAV
        WavFile.DecodedAudio hi = WavFile.Read(new MemoryStream(pcm16));
        Check("HI WavFile.Read PCM16 stereo: rate and channels", hi.SampleRate == rate && hi.Channels.Length == 2, $"{hi.SampleRate} Hz {hi.Channels.Length} ch");
        float rd = Math.Max(Signals.MaxAbsDiff(hi.Channels[0], ownCh[0]), Signals.MaxAbsDiff(hi.Channels[1], ownCh[1]));
        Check("HI WavFile.Read PCM16 equals own sample for sample", rd == 0f, $"max diff {rd:E2}, {hi.Length} frames");
        float d24 = Signals.MaxAbsDiff(WavFile.Read(new MemoryStream(pcm24)).Channels[0], mono);
        Check("HI WavFile.Read PCM24 matches the source within 24-bit rounding", d24 < 2e-7f, $"max diff {d24:E2}");
        float d32 = Signals.MaxAbsDiff(WavFile.Read(new MemoryStream(f32)).Channels[0], mono);
        Check("HI WavFile.Read float32 matches the source exactly", d32 == 0f, $"max diff {d32:E2}");
        Check("HI WavFile.TryReadDurationSeconds", WavFile.TryReadDurationSeconds(pcm16, out double d1) && Math.Abs(d1 - seconds) < 1e-9, $"{d1} s");
        MemoryStream ms = new();
        WavFile.WriteMono16(ms, mono, rate);
        byte[] hiBytes = ms.ToArray();
        byte[] ownBytes = OwnWav.WriteMono16(mono, rate);
        Check("HI WavFile.WriteMono16 bytes equal own", hiBytes.AsSpan().SequenceEqual(ownBytes), $"{hiBytes.Length} vs {ownBytes.Length} bytes");
        float rt = Signals.MaxAbsDiff(WavFile.Read(new MemoryStream(hiBytes)).Channels[0], mono);
        Check("HI WavFile write then read is within 2 least significant bits (writes x32767, reads /32768)", rt < 2f / 32768f, $"max diff {rt:E2} = {rt * 32768f:F2} LSB");
        bool refused = false;
        try
        {
            WavFile.Read(new MemoryStream(new byte[64]));
        }
        catch (InvalidDataException)
        {
            refused = true;
        }
        Check("HI WavFile.Read refuses a non-WAV by InvalidDataException", refused, "an exception, not a result");
        read.Add(("HartsyInference", () => WavFile.Read(new MemoryStream(pcm16))));
        write.Add(("HartsyInference", () =>
        {
            MemoryStream m = new();
            WavFile.WriteMono16(m, mono, rate);
        }));
        const int loops = 100_000;
        Time("WavFile.TryReadDurationSeconds", $"{loops} header reads", 0,
            ("own", () =>
            {
                for (int i = 0; i < loops; i++)
                {
                    OwnWav.TryDuration(pcm16, out double _);
                }
            }),
            ("HartsyInference", () =>
            {
                for (int i = 0; i < loops; i++)
                {
                    WavFile.TryReadDurationSeconds(pcm16, out double _);
                }
            }));
#endif
        Time("WavFile.Read", $"PCM16 stereo {rate} Hz, {pcm16.Length / 1048576.0:F1} MiB", seconds, read.ToArray());
        Time("WavFile.WriteMono16", "1 s mono", 1.0, write.ToArray());
    }

    private void FftCases()
    {
        foreach (int n in new[] { 512, 2048 })
        {
            int hop = n / 4;
            float[] sig = Signals.Mix(48000, seconds);
            int frames = (sig.Length - n) / hop;
            float[] windowed = new float[frames * n];
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < n; i++)
                {
                    windowed[f * n + i] = sig[f * hop + i] * (0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / n));
                }
            }
            double framesPerSecond = frames / seconds;
            OwnFft own = new(n);
            float[] re = new float[n], im = new float[n], oRe = new float[n / 2 + 1], oIm = new float[n / 2 + 1];
            float[] tone = Signals.Sine(48000, 1.0, 100.0 * 48000 / n);
            own.RealTransform(tone.AsSpan(0, n), oRe, oIm);
            Check($"own real FFT n={n}: a tone centred on bin 100 peaks at bin 100", PeakBin(oRe, oIm) == 100, $"peak bin {PeakBin(oRe, oIm)}");
            List<(string, Action)> complex = [("own", () => Loop(frames, f =>
            {
                windowed.AsSpan(f * n, n).CopyTo(re);
                Array.Clear(im);
                own.Transform(re, im);
            }))];
            List<(string, Action)> real = [("own", () => Loop(frames, f => own.RealTransform(windowed.AsSpan(f * n, n), oRe, oIm)))];
#if HI_FFT
            float[] hRe = new float[n], hIm = new float[n], hoRe = new float[n / 2 + 1], hoIm = new float[n / 2 + 1];
            Fft.RealTransform(tone.AsSpan(0, n), hoRe, hoIm, n);
            Check($"HI Fft.RealTransform n={n}: a tone centred on bin 100 peaks at bin 100", PeakBin(hoRe, hoIm) == 100, $"peak bin {PeakBin(hoRe, hoIm)}");
            bool longInputRefused = false;
            try
            {
                Fft.RealTransform(tone, hoRe, hoIm, n);
            }
            catch (ArgumentException)
            {
                longInputRefused = true;
            }
            log($"INFO|HI Fft.RealTransform n={n} given an input longer than n (documented only as 'at least n'): {(longInputRefused ? "throws ArgumentException" : "accepted")}");
            windowed.AsSpan(0, n).CopyTo(hRe);
            Array.Clear(hIm);
            Fft.Transform(hRe, hIm, n);
            windowed.AsSpan(0, n).CopyTo(re);
            Array.Clear(im);
            own.Transform(re, im);
            float scale = re.Max(x => Math.Abs(x));
            float fd = Math.Max(Signals.MaxAbsDiff(hRe, re), Signals.MaxAbsDiff(hIm, im)) / scale;
            Check($"HI Fft.Transform n={n} equals own within 1e-4 of the peak magnitude", fd < 1e-4f, $"relative max diff {fd:E2}");
            complex.Add(("HartsyInference", () => Loop(frames, f =>
            {
                windowed.AsSpan(f * n, n).CopyTo(hRe);
                Array.Clear(hIm);
                Fft.Transform(hRe, hIm, n);
            })));
            real.Add(("HartsyInference", () => Loop(frames, f => Fft.RealTransform(windowed.AsSpan(f * n, n), hoRe, hoIm, n))));
#endif
            Time("Fft.Transform (complex, in place)", $"n={n} hop={hop}, {framesPerSecond:F1} frames per audio second", seconds, complex.ToArray());
            Time("Fft.RealTransform", $"n={n} hop={hop}, {framesPerSecond:F1} frames per audio second", seconds, real.ToArray());
        }
        ExactnessAndOtherSizes();
    }

    private void ExactnessAndOtherSizes()
    {
        const int n = 256;
        Random rng = new(7);
        float[] re = new float[n], im = new float[n];
        for (int i = 0; i < n; i++)
        {
            re[i] = (float)(rng.NextDouble() * 2 - 1);
            im[i] = (float)(rng.NextDouble() * 2 - 1);
        }
        double[] rr = new double[n], ri = new double[n];
        OwnFft.NaiveDft(re, im, rr, ri);
        double peak = rr.Concat(ri).Max(x => Math.Abs(x));
        float[] ownRe = (float[])re.Clone(), ownIm = (float[])im.Clone();
        new OwnFft(n).Transform(ownRe, ownIm);
        Check("own FFT n=256 equals a double-precision DFT within 1e-5 of the peak", MaxRel(ownRe, ownIm, rr, ri, peak) < 1e-5, $"{MaxRel(ownRe, ownIm, rr, ri, peak):E2}");
#if HI_FFT
        float[] hRe = (float[])re.Clone(), hIm = (float[])im.Clone();
        Fft.Transform(hRe, hIm, n);
        Check("HI Fft.Transform n=256 equals a double-precision DFT within 1e-5 of the peak", MaxRel(hRe, hIm, rr, ri, peak) < 1e-5, $"{MaxRel(hRe, hIm, rr, ri, peak):E2}");
        foreach (int m in new[] { 400, 1000 })
        {
            float[] xr = new float[m], xi = new float[m];
            for (int i = 0; i < m; i++)
            {
                xr[i] = (float)(rng.NextDouble() * 2 - 1);
            }
            double[] nr = new double[m], ni = new double[m];
            OwnFft.NaiveDft(xr, xi, nr, ni);
            float[] yr = (float[])xr.Clone(), yi = (float[])xi.Clone();
            Fft.Transform(yr, yi, m);
            double p = nr.Concat(ni).Max(x => Math.Abs(x));
            Check($"HI Fft.Transform n={m} (not a power of two) equals a double-precision DFT within 1e-4 of the peak", MaxRel(yr, yi, nr, ni, p) < 1e-4, $"{MaxRel(yr, yi, nr, ni, p):E2}");
            float[] br = new float[m], bi = new float[m];
            Time("Fft.Transform (Bluestein, not a power of two)", $"n={m}, 1000 frames", 0,
                ("HartsyInference", () => Loop(1000, _ =>
                {
                    xr.CopyTo(br, 0);
                    Array.Clear(bi);
                    Fft.Transform(br, bi, m);
                })));
        }
#endif
    }

    private void OtherCases()
    {
        float[] sig = Signals.Mix(48000, seconds);
        float[] own = (float[])sig.Clone();
        float[] hiBuf = (float[])sig.Clone();
        List<(string, Action)> norm = [("own", () =>
        {
            sig.CopyTo(own, 0);
            float max = 0;
            foreach (float v in own)
            {
                max = Math.Max(max, Math.Abs(v));
            }
            float g = 0.99f / max;
            for (int i = 0; i < own.Length; i++)
            {
                own[i] *= g;
            }
        })];
        const int hann = 2048;
        float[] w = new float[hann];
        for (int i = 0; i < hann; i++)
        {
            w[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / hann);
        }
        const int lookups = 1_000_000;
        float sink = 0;
        List<(string, Action)> hannImpls = [("own (array held by the caller)", () =>
        {
            for (int i = 0; i < lookups; i++)
            {
                sink += w[i & (hann - 1)];
            }
        })];
#if HI_OTHER
        float[] hw = HannWindow.Get(hann);
        Check("HI HannWindow.Get(2048) equals the periodic Hann window", Signals.MaxAbsDiff(hw, w) < 1e-7f, $"max diff {Signals.MaxAbsDiff(hw, w):E2}");
        hannImpls.Add(("HartsyInference HannWindow.Get (a lock per call)", () =>
        {
            for (int i = 0; i < lookups; i++)
            {
                sink += HannWindow.Get(hann)[i & (hann - 1)];
            }
        }));
        sig.CopyTo(hiBuf, 0);
        LoudnessNormalizer.PeakNormalize(hiBuf, 0.99f);
        sig.CopyTo(own, 0);
        norm[0].Item2();
        Check("HI LoudnessNormalizer.PeakNormalize equals own", Signals.MaxAbsDiff(hiBuf, own) < 1e-6f, $"max diff {Signals.MaxAbsDiff(hiBuf, own):E2}");
        norm.Add(("HartsyInference", () =>
        {
            sig.CopyTo(hiBuf, 0);
            LoudnessNormalizer.PeakNormalize(hiBuf, 0.99f);
        }));
        float[] rmsBuf = Signals.Sine(48000, 1.0, 440, 0.05);
        LoudnessNormalizer.RmsNormalize(rmsBuf, 0.1f, 0.99f);
        double rms = Math.Sqrt(rmsBuf.Sum(x => (double)x * x) / rmsBuf.Length);
        Check("HI LoudnessNormalizer.RmsNormalize reaches the target RMS", Math.Abs(rms - 0.1) < 1e-3, $"rms {rms:F4}");
        float[][] waves = [Signals.Sine(48000, 1.0, 300), Signals.Sine(48000, 1.0, 300), Signals.Sine(48000, 1.0, 300)];
        float[] joined = WaveCrossFade.Join(waves, 4800);
        Check("HI WaveCrossFade.Join length is the sum minus two overlaps", joined.Length == 3 * 48000 - 2 * 4800, $"{joined.Length}");
        Time("WaveCrossFade.Join", "3 x 1 s, 4800-sample cross-fade", 3.0, ("HartsyInference", () => WaveCrossFade.Join(waves, 4800)));
#endif
        Time("PeakNormalize", $"{seconds} s mono (copy included)", seconds, norm.ToArray());
        Time("HannWindow lookup", $"{lookups} lookups of n=2048", 0, hannImpls.ToArray());
        if (sink == float.MaxValue)
        {
            log("unreachable");
        }
    }

    /// <summary>
    /// Fits A sin(2 pi f (t - d)) to a tone by projecting it on a sine and a cosine, and returns d in seconds. Positive d
    /// is a late output, negative an early one. The amplitude and the error left after removing d come back as well.
    /// </summary>
    private static (double DelaySeconds, double Amplitude, double ResidualMax) FitTone(float[] x, int rate, double freq, int from, int to)
    {
        double s = 0, c = 0;
        double w = 2 * Math.PI * freq / rate;
        for (int i = from; i < to; i++)
        {
            s += x[i] * Math.Sin(w * i);
            c += x[i] * Math.Cos(w * i);
        }
        double n = to - from;
        double amp = 2 * Math.Sqrt(s * s + c * c) / n;
        double phase = Math.Atan2(-c, s);
        double residual = 0;
        for (int i = from; i < to; i++)
        {
            residual = Math.Max(residual, Math.Abs(x[i] - amp * Math.Sin(w * i - phase)));
        }
        return (phase / (2 * Math.PI * freq), amp, residual);
    }

    /// <summary>
    /// The true-ideal error of an integer decimation, as a finding, and a check that the output is the ideal tone with
    /// one constant delay of half an input sample early and nothing else wrong.
    /// </summary>
    private void CheckDelay(string who, float[] tone, int outRate, int inRate, int edge, float trueIdealError)
    {
        (double delay, double amp, double residual) = FitTone(tone, outRate, 1000, edge, tone.Length - edge);
        double delayInputSamples = delay * inRate;
        log($"FINDING|{who}: error against the true ideal 1 kHz sine is {trueIdealError:E2} (it does not meet 5e-3); fitted delay {delayInputSamples:F4} input samples, amplitude {amp:F4}");
        Check($"{who} 1 kHz tone is the ideal sine delayed by -0.5 input sample, and only that", Math.Abs(delayInputSamples + 0.5) < 0.02 && residual < 5e-3 && Math.Abs(amp - 0.5) < 5e-3,
            $"delay {delayInputSamples:F4} input samples, amplitude {amp:F4}, residual {residual:E2}");
    }

    private static void Loop(int count, Action<int> body)
    {
        for (int i = 0; i < count; i++)
        {
            body(i);
        }
    }

    private static int PeakBin(float[] re, float[] im)
    {
        int best = 0;
        float bestMag = 0;
        for (int i = 0; i < re.Length; i++)
        {
            float m = re[i] * re[i] + im[i] * im[i];
            if (m > bestMag)
            {
                bestMag = m;
                best = i;
            }
        }
        return best;
    }

    private static double MaxRel(float[] re, float[] im, double[] rr, double[] ri, double peak)
    {
        double max = 0;
        for (int i = 0; i < re.Length; i++)
        {
            max = Math.Max(max, Math.Max(Math.Abs(re[i] - rr[i]), Math.Abs(im[i] - ri[i])));
        }
        return max / peak;
    }
}
