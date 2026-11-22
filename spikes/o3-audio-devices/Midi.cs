using NAudio.Midi;

/// <summary>Opens the first MIDI input of each library and prints message timestamps until the time is up.</summary>
internal static class Midi
{
    public static async Task<int> Run(Options o)
    {
        int seconds = o.GetInt("seconds", 15);
        var winrt = await WinRTMidiIn.GetDevicesAsync();
        Console.WriteLine($"WinRT MIDI inputs: {winrt.Count}; RtMidi inputs: {RtMidi.Net.MidiManager.GetAvailableDevices().Count(d => d.Type == RtMidi.Net.MidiDeviceType.Input)}");
        if (winrt.Count == 0)
        {
            Console.WriteLine("no MIDI input device is attached; timestamps not measured");
            return 2;
        }

        IMidiInput input = await WinRTMidiIn.CreateAsync(winrt[0].Id);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        input.MessageReceived += (_, e) =>
        {
            double host = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Console.WriteLine($"NAudio WinRT ts={e.Timestamp.TotalMilliseconds:F3} ms host={host:F3} ms raw=0x{e.RawMessage:X8}");
        };
        input.Start();
        await Task.Delay(seconds * 1000);
        input.Stop();
        input.Dispose();
        return 0;
    }
}
