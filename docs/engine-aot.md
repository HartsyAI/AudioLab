# The engine in the page: ahead-of-time build

The engine that plays in the browser (DESIGN.md, D60) runs as a WebAssembly runtime in a web worker. A plain `dotnet build`, and CI, make it
**interpreted**, which plays a light project but not one with reverb, delay or saturation. A publish can compile the engine ahead of time:

```bash
dotnet workload install wasm-tools
dotnet publish src/AudioLab.Server -c Release -p:AudioLabWorkerAot=true -o artifacts/server
```

- `AudioLabWorkerAot` applies to the engine only (`src/AudioLab.Audio.Worker`). `-p:RunAOTCompilation=true` would compile the page's own runtime too,
  which makes the page download several times larger for no gain.
- The runners of CI have no `wasm-tools`, so CI never builds this; do not add `AudioLabWorkerAot` to a build or test step.
- The result is checked from the files: `artifacts/server/wwwroot/worker/_framework/dotnet.native.*.wasm` is about 11 MB with the switch and about 3 MB without.
- The running engine says which build it is: the status bar shows a caution when an interpreted engine is given a project it is unlikely to keep up with, and a
  dropout message names an interpreted build. The measurements behind this are in D60.
