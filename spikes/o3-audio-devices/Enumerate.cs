using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;
using NAudio.CoreAudioApi;
using NAudio.Midi;
using NAudio.Wave;
using PortAudioSharp;
using Xt;

/// <summary>Lists every audio and MIDI device each candidate library can see, with what it reports about modes and periods.</summary>
internal static class Enumerate
{
    public static async Task RunAsync()
    {
        NAudioWasapi();
        NAudioAsio();
        PortAudioList();
        MiniaudioList();
        XtList();
        await MidiListAsync();
    }

    private static void NAudioWasapi()
    {
        Console.WriteLine("== NAudio.Wasapi (MMDevice endpoints)");
        using MMDeviceEnumerator en = new();
        foreach (MMDevice d in en.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
        {
            string periods = "";
            try
            {
                AudioClient ac = d.AudioClient;
                periods = $"mix={ac.MixFormat.SampleRate}Hz/{ac.MixFormat.Channels}ch default={ac.DefaultDevicePeriod / 10000.0:F2}ms min={ac.MinimumDevicePeriod / 10000.0:F2}ms";
            }
            catch (Exception e) { periods = "period query failed: " + e.GetType().Name; }
            Console.WriteLine($"  [{d.DataFlow}] {d.FriendlyName} | {periods}");
        }
    }

    private static void NAudioAsio()
    {
        Console.WriteLine("== NAudio.Asio (installed ASIO drivers)");
        string[] names = AsioDevice.GetDriverNames();
        Console.WriteLine($"  count={names.Length}");
        foreach (string n in names)
        {
            try
            {
                using AsioDevice dev = AsioDevice.Open(n);
                var c = dev.Capabilities;
                Console.WriteLine($"  {n}: in={c.NbInputChannels} out={c.NbOutputChannels} rate={c.SampleRate} buf min/max/pref/gran={c.BufferMinSize}/{c.BufferMaxSize}/{c.BufferPreferredSize}/{c.BufferGranularity} lat in/out={c.InputLatency}/{c.OutputLatency}");
            }
            catch (Exception e) { Console.WriteLine($"  {n}: open failed: {e.GetType().Name}: {e.Message}"); }
        }
    }

    [DllImport("portaudio", CallingConvention = CallingConvention.Cdecl)] private static extern int Pa_GetHostApiCount();
    [DllImport("portaudio", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr Pa_GetHostApiInfo(int i);

    private static void PortAudioList()
    {
        Console.WriteLine("== PortAudioSharp2 + portaudio.dll (k2-fsa build)");
        PortAudio.LoadNativeLibrary();
        PortAudio.Initialize();
        Console.WriteLine($"  {PortAudio.VersionInfo.versionText}");
        int apis = Pa_GetHostApiCount();
        string[] apiNames = new string[apis];
        for (int i = 0; i < apis; i++)
        {
            IntPtr p = Pa_GetHostApiInfo(i);
            apiNames[i] = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(p, 8)) ?? "?";
        }
        Console.WriteLine("  host APIs: " + string.Join(", ", apiNames));
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            DeviceInfo d = PortAudio.GetDeviceInfo(i);
            Console.WriteLine($"  #{i} [{apiNames[d.hostApi]}] {d.name} in={d.maxInputChannels} out={d.maxOutputChannels} lowLat in/out={d.defaultLowInputLatency * 1000:F1}/{d.defaultLowOutputLatency * 1000:F1}ms {d.defaultSampleRate}Hz");
        }
        PortAudio.Terminate();
    }

    private static unsafe void MiniaudioList()
    {
        Console.WriteLine("== Hexa.NET.MiniAudio 1.0.1 (miniaudio " + Marshal.PtrToStringAnsi((IntPtr)MiniAudio.VersionString()) + ")");
        MaContext* ctx = (MaContext*)NativeMemory.AllocZeroed((nuint)sizeof(MaContext));
        MaBackend[] order = { MaBackend.Wasapi, MaBackend.Dsound, MaBackend.Winmm };
        fixed (MaBackend* b = order)
        {
            MaResult r = MiniAudio.ContextInit(b, (uint)order.Length, (MaContextConfig*)null, ctx);
            Console.WriteLine($"  context init: {r}, backend={ctx->Backend}");
        }
        MaDeviceInfo* play; uint pc; MaDeviceInfo* cap; uint cc;
        MiniAudio.ContextGetDevices(ctx, &play, &pc, &cap, &cc);
        for (uint i = 0; i < pc; i++) Console.WriteLine($"  [playback] {Marshal.PtrToStringUTF8((IntPtr)(&play[i].Name_0))} default={play[i].IsDefault}");
        for (uint i = 0; i < cc; i++) Console.WriteLine($"  [capture]  {Marshal.PtrToStringUTF8((IntPtr)(&cap[i].Name_0))} default={cap[i].IsDefault}");
        MiniAudio.ContextUninit(ctx);
        NativeMemory.Free(ctx);
    }

    private static void XtList()
    {
        Console.WriteLine("== Xt.Audio 2.1.0");
        using XtPlatform platform = XtAudio.Init("o3", IntPtr.Zero);
        foreach (XtSystem sys in platform.GetSystems())
        {
            XtService svc = platform.GetService(sys);
            Console.WriteLine($"  system {sys} caps={svc.GetCapabilities()}");
            try
            {
                using XtDeviceList list = svc.OpenDeviceList(XtEnumFlags.All);
                for (int i = 0; i < list.GetCount(); i++)
                {
                    string id = list.GetId(i);
                    Console.WriteLine($"    {list.GetName(id)} caps={list.GetCapabilities(id)}");
                }
            }
            catch (Exception e) { Console.WriteLine("    list failed: " + e.Message); }
        }
    }

    private static async Task MidiListAsync()
    {
        Console.WriteLine("== MIDI input devices");
        var winrt = await WinRTMidiIn.GetDevicesAsync();
        Console.WriteLine($"  NAudio WinRTMidiIn: {winrt.Count} [{string.Join("; ", winrt.Select(d => d.Name))}]");
        Console.WriteLine($"  NAudio WinMM MidiIn: {MidiIn.NumberOfDevices}");
        var rt = RtMidi.Net.MidiManager.GetAvailableDevices();
        Console.WriteLine($"  RtMidi.Net devices: {rt.Count} apis=[{string.Join(",", RtMidi.Net.MidiManager.GetAvailableApis())}] [{string.Join("; ", rt.Select(d => d.Name + "/" + d.Type))}]");
    }
}
