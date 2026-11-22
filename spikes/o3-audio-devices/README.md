# O3 spike: native audio device and MIDI input library

Evidence for DESIGN.md D29 (O3). Throw-away code, outside `AudioLab.slnx` and the style gate, with its own
`Directory.Build.props` and `Directory.Packages.props` so the repository's rules do not apply to it.

```bash
cd spikes/o3-audio-devices
dotnet build -c Release
dotnet bin/Release/net10.0-windows10.0.19041.0/O3.Spike.dll enum        # every device each library can see
dotnet bin/Release/net10.0-windows10.0.19041.0/O3.Spike.dll selftest    # the click detector against known software delays
dotnet bin/Release/net10.0-windows10.0.19041.0/O3.Spike.dll run --lib ma --mode exclusive --in "Yeti" --out "Yeti" \
    --buffer 128 --seconds 30 --emit                                    # loopback round trip (needs a physical or acoustic loop)
dotnet bin/Release/net10.0-windows10.0.19041.0/O3.Spike.dll run --lib asio --driver Realtek --seconds 180 --stress   # jitter, dropouts, GC
dotnet bin/Release/net10.0-windows10.0.19041.0/O3.Spike.dll midi --seconds 30
```

`--lib` is `asio` (NAudio.Asio 3.3.0), `pa` (PortAudioSharp2 1.0.6 with its portaudio.dll), `ma` (Hexa.NET.MiniAudio
1.0.1, miniaudio 0.11.25) or `xt` (Xt.Audio 2.1.0). `--stress` runs a thread that allocates garbage while the stream
runs. Without `--emit` the output is silent and only callback timing is recorded.

How a round trip is measured: the callback writes a 2 ms, 3 kHz burst (starting at full amplitude) to output channel 0
at a known global frame index and finds the first input sample above a threshold (six times the noise floor measured
before the first burst, at least 0.02) in channel 0. The difference of the two frame indices is the round trip, in the
frame domain of one duplex callback, so it needs no wall clock. `selftest` feeds the output through a software delay
and reads back delay plus one callback of 128 frames, as the fake loop is built.
