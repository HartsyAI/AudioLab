# O9 spike: HartsyInference packages on net10 and `HartsyInference.Audio` in WebAssembly

Throwaway code for open question O9 in [DESIGN.md](../../DESIGN.md). It has its own restore (`nuget.config` with
nuget.org only, `Directory.Packages.props` with exact pins, packages restored into `.packages/`) and is not part of
`AudioLab.slnx`. Nothing here enters the real solution.

| Folder | What it is |
|--------|------------|
| `restore-check/` | References all 14 published ids at one exact version, restores on net10, loads every assembly. |
| `bench/` | Shared code: known signals, the straightforward C# versions (`Own*.cs`) and `Runner.cs`, which asserts and times each API. |
| `console/` | The server run: `dotnet run -c Release --project console -- <audio seconds> <rounds>`. |
| `wasm/` | A Blazor WebAssembly page that runs the same `Runner`. `-p:Variant=` selects what is compiled in, `-p:Mode=` the build. |
| `static-host/` | A small Kestrel host that serves a published folder with its `.br` files and saves a results text. |
| `run-matrix.sh` | Publishes every variant and mode and logs each publish. |
| `results/` | The raw output of every run quoted in DESIGN.md. |

Variants: `None` (page only), `Own` (the straightforward code only), `Resampler`, `Wav`, `Fft`, `Other` (one group of
`HartsyInference.Audio` calls each) and `All`. Modes: `Default` (the SDK's trimming, which copies the HartsyInference
assemblies whole), `Trim` (`TrimMode=full`, every assembly trimmed, warnings per assembly) and `Aot`.

No model weights are downloaded or loaded by anything here.
