using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;
using NAudio.Wave;
using PortAudioSharp;
using Xt;

/// <summary>A running duplex stream of one library. Dispose stops it.</summary>
internal interface IBackend : IDisposable
{
    /// <summary>Human description of what was opened, with the latencies the library itself reports.</summary>
    string Describe { get; }
}

internal sealed class Options
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public Options(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                string key = args[i][2..];
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
                _values[key] = value;
            }
        }
    }

    public string Get(string key, string fallback) => _values.TryGetValue(key, out string? v) ? v : fallback;

    public int GetInt(string key, int fallback) => _values.TryGetValue(key, out string? v) ? int.Parse(v) : fallback;

    public bool GetBool(string key) => _values.TryGetValue(key, out string? v) && v != "false";
}

internal static class Pick
{
    public static string Match(IEnumerable<string> names, string needle, string what)
    {
        string[] hits = names.Where(n => n.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (hits.Length == 0)
        {
            throw new InvalidOperationException($"no {what} matching '{needle}'");
        }

        return hits[0];
    }
}

/// <summary>NAudio 3 AsioDevice duplex.</summary>
internal sealed class NAudioAsioBackend : IBackend
{
    private readonly AsioDevice _device;

    public NAudioAsioBackend(Options o, Workload w)
    {
        string name = Pick.Match(AsioDevice.GetDriverNames(), o.Get("driver", "ASIO"), "ASIO driver");
        _device = AsioDevice.Open(name);
        int rate = o.GetInt("rate", 48000);
        _device.InitDuplex(new AsioDuplexOptions
        {
            InputChannels = [o.GetInt("inch", 0)],
            OutputChannels = [o.GetInt("outch", 0)],
            SampleRate = rate,
            BufferSize = o.GetInt("buffer", 0) == 0 ? null : o.GetInt("buffer", 0),
            Processor = (in AsioProcessBuffers b) => w.OnAudio(b.GetInput(0), 1, b.GetOutput(0), 1, b.Frames),
        });
        _device.ResyncOccurred += (_, _) => w.NoteXrun();
        Describe = $"NAudio.Asio '{name}' rate={_device.CurrentSampleRate} buffer={_device.FramesPerBuffer} frames, driver-reported latency in/out={_device.InputLatencySamples}/{_device.OutputLatencySamples} samples";
        _device.Start();
    }

    public string Describe { get; }

    public void Dispose()
    {
        _device.Stop();
        _device.Dispose();
    }
}

/// <summary>PortAudio through PortAudioSharp2, full duplex callback stream.</summary>
internal sealed class PortAudioBackend : IBackend
{
    [DllImport("portaudio", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_GetHostApiCount();

    [DllImport("portaudio", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetHostApiInfo(int i);

    private readonly PortAudioSharp.Stream _stream;
    private readonly PortAudioSharp.Stream.Callback _callback;

    public PortAudioBackend(Options o, Workload w)
    {
        PortAudio.LoadNativeLibrary();
        PortAudio.Initialize();
        string api = o.Get("api", "WASAPI");
        string[] apiNames = Enumerable.Range(0, Pa_GetHostApiCount()).Select(i => Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(Pa_GetHostApiInfo(i), 8)) ?? "?").ToArray();
        int inDev = Find(apiNames, api, o.Get("in", ""), true);
        int outDev = Find(apiNames, api, o.Get("out", ""), false);
        DeviceInfo di = PortAudio.GetDeviceInfo(inDev);
        DeviceInfo dout = PortAudio.GetDeviceInfo(outDev);
        int rate = o.GetInt("rate", 48000);
        int frames = o.GetInt("buffer", 0);
        double lowIn = o.GetBool("high") ? di.defaultHighInputLatency : di.defaultLowInputLatency;
        double lowOut = o.GetBool("high") ? dout.defaultHighOutputLatency : dout.defaultLowOutputLatency;
        StreamParameters pin = new() { device = inDev, channelCount = 2, sampleFormat = SampleFormat.Float32, suggestedLatency = lowIn, hostApiSpecificStreamInfo = IntPtr.Zero };
        StreamParameters pout = new() { device = outDev, channelCount = 2, sampleFormat = SampleFormat.Float32, suggestedLatency = lowOut, hostApiSpecificStreamInfo = IntPtr.Zero };
        _callback = (IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo t, StreamCallbackFlags flags, IntPtr user) =>
        {
            unsafe
            {
                int n = (int)frameCount;
                w.OnAudio(new ReadOnlySpan<float>((void*)input, n * 2), 2, new Span<float>((void*)output, n * 2), 2, n);
            }

            if ((flags & (StreamCallbackFlags.InputOverflow | StreamCallbackFlags.InputUnderflow | StreamCallbackFlags.OutputOverflow | StreamCallbackFlags.OutputUnderflow)) != 0)
            {
                w.NoteXrun();
            }

            w.NoteDacMinusAdc(t.outputBufferDacTime - t.inputBufferAdcTime);
            return StreamCallbackResult.Continue;
        };
        _stream = new PortAudioSharp.Stream(pin, pout, rate, frames == 0 ? PortAudio.FramesPerBufferUnspecified : (uint)frames, StreamFlags.ClipOff, _callback, null!);
        Describe = $"PortAudio [{apiNames[di.hostApi]}] in='{di.name}' out='{dout.name}' rate={rate} framesPerBuffer={(frames == 0 ? "unspecified" : frames)} suggestedLatency in/out={lowIn * 1000:F1}/{lowOut * 1000:F1} ms";
        _stream.Start();
    }

    public string Describe { get; }

    public void Dispose()
    {
        _stream.Stop();
        _stream.Dispose();
        PortAudio.Terminate();
    }

    private static int Find(string[] apiNames, string api, string needle, bool input)
    {
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            DeviceInfo d = PortAudio.GetDeviceInfo(i);
            bool dirOk = input ? d.maxInputChannels >= 1 : d.maxOutputChannels >= 1;
            if (dirOk && apiNames[d.hostApi].Contains(api, StringComparison.OrdinalIgnoreCase) && d.name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"PortAudio: no {(input ? "input" : "output")} '{needle}' in API '{api}'");
    }
}

/// <summary>miniaudio through the generated Hexa.NET.MiniAudio binding: native callback into an UnmanagedCallersOnly method.</summary>
internal sealed unsafe class MiniaudioBackend : IBackend
{
    private static Workload? s_workload;

    private readonly MaContext* _ctx;
    private readonly MaDevice* _dev;
    private readonly MaDeviceId* _idIn;
    private readonly MaDeviceId* _idOut;

    public MiniaudioBackend(Options o, Workload w)
    {
        s_workload = w;
        _ctx = (MaContext*)NativeMemory.AllocZeroed((nuint)sizeof(MaContext));
        MaBackend[] order = { MaBackend.Wasapi };
        fixed (MaBackend* b = order)
        {
            Check(MiniAudio.ContextInit(b, 1, (MaContextConfig*)null, _ctx), "context");
        }

        MaDeviceInfo* play; uint pc; MaDeviceInfo* cap; uint cc;
        Check(MiniAudio.ContextGetDevices(_ctx, &play, &pc, &cap, &cc), "devices");
        string inNeedle = o.Get("in", "");
        string outNeedle = o.Get("out", "");
        _idIn = (MaDeviceId*)NativeMemory.AllocZeroed((nuint)sizeof(MaDeviceId));
        _idOut = (MaDeviceId*)NativeMemory.AllocZeroed((nuint)sizeof(MaDeviceId));
        string inName = Copy(cap, cc, inNeedle, _idIn);
        string outName = Copy(play, pc, outNeedle, _idOut);
        bool exclusive = !o.Get("mode", "exclusive").Equals("shared", StringComparison.OrdinalIgnoreCase);
        MaShareMode share = exclusive ? MaShareMode.Exclusive : MaShareMode.Shared;
        MaDeviceConfig cfg = MiniAudio.DeviceConfigInit(MaDeviceType.Duplex);
        cfg.SampleRate = (uint)o.GetInt("rate", 48000);
        cfg.PeriodSizeInFrames = (uint)o.GetInt("buffer", 128);
        cfg.Periods = (uint)o.GetInt("periods", 2);
        cfg.PerformanceProfile = MaPerformanceProfile.LowLatency;
        cfg.NoFixedSizedCallback = 1;
        cfg.Capture.PDeviceID = _idIn;
        cfg.Capture.Format = MaFormat.F32;
        cfg.Capture.Channels = 2;
        cfg.Capture.ShareMode = share;
        cfg.Playback.PDeviceID = _idOut;
        cfg.Playback.Format = MaFormat.F32;
        cfg.Playback.Channels = 2;
        cfg.Playback.ShareMode = share;
        cfg.DataCallback = (void*)(delegate* unmanaged[Cdecl]<MaDevice*, void*, void*, uint, void>)&Callback;
        _dev = (MaDevice*)NativeMemory.AllocZeroed((nuint)sizeof(MaDevice));
        Check(MiniAudio.DeviceInit(_ctx, &cfg, _dev), "device init");
        Describe = $"miniaudio {Marshal.PtrToStringAnsi((IntPtr)MiniAudio.VersionString())} WASAPI {(exclusive ? "exclusive" : "shared")} in='{inName}' out='{outName}' rate={_dev->SampleRate} requestedPeriod={cfg.PeriodSizeInFrames} periods={cfg.Periods}; internal capture period/periods={_dev->Capture.InternalPeriodSizeInFrames}/{_dev->Capture.InternalPeriods} playback={_dev->Playback.InternalPeriodSizeInFrames}/{_dev->Playback.InternalPeriods}";
        Check(MiniAudio.DeviceStart(_dev), "device start");
    }

    public string Describe { get; }

    public void Dispose()
    {
        MiniAudio.DeviceUninit(_dev);
        MiniAudio.ContextUninit(_ctx);
        NativeMemory.Free(_dev);
        NativeMemory.Free(_ctx);
        NativeMemory.Free(_idIn);
        NativeMemory.Free(_idOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(MaDevice* dev, void* output, void* input, uint frames)
    {
        int n = (int)frames;
        s_workload!.OnAudio(new ReadOnlySpan<float>(input, n * 2), 2, new Span<float>(output, n * 2), 2, n);
    }

    private static string Copy(MaDeviceInfo* list, uint count, string needle, MaDeviceId* dest)
    {
        for (uint i = 0; i < count; i++)
        {
            string name = Marshal.PtrToStringUTF8((IntPtr)(&list[i].Name_0)) ?? "";
            if (name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                *dest = list[i].Id;
                return name;
            }
        }

        throw new InvalidOperationException($"miniaudio: no device '{needle}'");
    }

    private static void Check(MaResult r, string what)
    {
        if (r != MaResult.Success)
        {
            throw new InvalidOperationException($"miniaudio {what}: {r}");
        }
    }
}

/// <summary>Xt.Audio full duplex: one ASIO device, or an aggregate of an exclusive WASAPI input and output.</summary>
internal sealed class XtBackend : IBackend
{
    private static Workload? s_workload;

    private readonly XtPlatform _platform;
    private readonly XtStream _stream;
    private readonly List<IDisposable> _owned = new();

    public XtBackend(Options o, Workload w)
    {
        s_workload = w;
        _platform = XtAudio.Init("o3", IntPtr.Zero);
        string api = o.Get("api", "ASIO");
        int rate = o.GetInt("rate", 48000);
        XtMix mix = new(rate, XtSample.Float32);
        XtOnXRun xrun = (XtStream s, int index, object? user) => s_workload!.NoteXrun();
        if (api.Equals("ASIO", StringComparison.OrdinalIgnoreCase))
        {
            XtService svc = _platform.GetService(XtSystem.ASIO);
            XtDeviceList list = svc.OpenDeviceList(XtEnumFlags.All);
            string id = FindId(list, o.Get("driver", ""));
            XtDevice dev = svc.OpenDevice(id);
            _owned.Add(dev);
            XtChannels ch = new(2, 0, 2, 0);
            XtFormat fmt = new(mix, ch);
            XtBufferSize bs = dev.GetBufferSize(in fmt);
            double buf = o.GetInt("buffer", 0) == 0 ? bs.current : o.GetInt("buffer", 0) * 1000.0 / rate;
            XtStreamParams sp = new(true, OnBuffer, xrun, null!);
            XtDeviceStreamParams dsp = new(in sp, in fmt, buf);
            _stream = dev.OpenStream(in dsp, null!);
            Describe = $"Xt.Audio ASIO '{list.GetName(id)}' rate={rate} buffer={_stream.GetFrames()} frames (range {bs.min:F2}-{bs.max:F2} ms), reported latency in/out={_stream.GetLatency().input:F2}/{_stream.GetLatency().output:F2} ms";
        }
        else
        {
            XtService svc = _platform.GetService(XtSystem.WASAPI);
            XtDeviceList list = svc.OpenDeviceList(XtEnumFlags.All);
            string suffix = o.Get("mode", "exclusive").Equals("shared", StringComparison.OrdinalIgnoreCase) ? "(Shared)" : "(Exclusive)";
            string inId = FindId(list, o.Get("in", "") + "|" + suffix, true);
            string outId = FindId(list, o.Get("out", "") + "|" + suffix, false);
            XtDevice din = svc.OpenDevice(inId);
            XtDevice dout = svc.OpenDevice(outId);
            _owned.Add(din);
            _owned.Add(dout);
            XtFormat fin = new(mix, new XtChannels(2, 0, 0, 0));
            XtFormat fout = new(mix, new XtChannels(0, 0, 2, 0));
            XtBufferSize bin = din.GetBufferSize(in fin);
            XtBufferSize bout = dout.GetBufferSize(in fout);
            double bufMs = o.GetInt("buffer", 0) == 0 ? Math.Max(bin.min, bout.min) : o.GetInt("buffer", 0) * 1000.0 / rate;
            XtStreamParams sp = new(true, OnBuffer, xrun, null!);
            XtAggregateDeviceParams[] ds = { new(din, ref fin.channels, bufMs), new(dout, ref fout.channels, bufMs) };
            XtAggregateStreamParams asp = new(in sp, ds, 2, in mix, dout);
            _stream = svc.AggregateStream(in asp, null!);
            Describe = $"Xt.Audio WASAPI {suffix} aggregate in='{list.GetName(inId)}' out='{list.GetName(outId)}' rate={rate} buffer={bufMs:F2} ms (min in/out {bin.min:F2}/{bout.min:F2}), frames={_stream.GetFrames()}, reported latency in/out={_stream.GetLatency().input:F2}/{_stream.GetLatency().output:F2} ms";
        }

        _stream.Start();
    }

    public string Describe { get; }

    public void Dispose()
    {
        _stream.Stop();
        _stream.Dispose();
        foreach (IDisposable d in _owned)
        {
            d.Dispose();
        }

        _platform.Dispose();
    }

    private static string FindId(XtDeviceList list, string needle, bool? input = null)
    {
        string[] parts = needle.Split('|');
        for (int i = 0; i < list.GetCount(); i++)
        {
            string id = list.GetId(i);
            string name = list.GetName(id);
            XtDeviceCaps caps = list.GetCapabilities(id);
            bool dirOk = input is null || (input.Value ? caps.HasFlag(XtDeviceCaps.Input) : caps.HasFlag(XtDeviceCaps.Output));
            if (dirOk && parts.All(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return id;
            }
        }

        throw new InvalidOperationException($"Xt: no device '{needle}'");
    }

    private static int OnBuffer(XtStream stream, in XtBuffer buffer, object? user)
    {
        unsafe
        {
            int n = buffer.frames;
            s_workload!.OnAudio(new ReadOnlySpan<float>((void*)buffer.input, n * 2), 2, new Span<float>((void*)buffer.output, n * 2), 2, n);
        }

        return 0;
    }
}
