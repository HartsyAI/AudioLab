# Environment and checks not captured in the other result files

Machine: OFFICE-DESKTOP, Intel Core i7-9700K (8 logical cores), 40 GB, NVIDIA GeForce RTX 3060, Windows 11 Pro 26200.
.NET SDK 10.0.403, runtime 10.0.14, workload `wasm-tools` 10.0.114.

Browser: the built-in browser pane of the Claude desktop app, user agent
`Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Claude/5.11492.5 Chrome/156.0.8078.75 Safari/537.36`,
`navigator.hardwareConcurrency` 8, `crossOriginIsolated` false, `document.visibilityState` "visible". The tool that
shows the pane reported that the session was not open in any window, and the pane reported itself hidden, so the page
ran as a background page.

Machine CPU (`\Processor(_Total)\% Processor Time`, one sample a second, other sessions' work included):

| When | Samples, % |
|------|-----------|
| before the first server run | 99 100 100 98 100 |
| before the page runs (trimmed, AOT, default) | 100 100 100 100 100 100 (and 100 100 100 100 100 100 100 100 before the hosts started) |
| before the final server run | 85 80 69 65 |
| after the final server run | 91 77 81 |
| after the 30 s AOT page run | 80 64 65 62 |

Restore checks, run in a scratch project against nuget.org only:

- `Version="[2.0.0-alpha.445]"` (an exact bracket) restored `HartsyInference.Audio` with its closure.
- `Version="2.0.0-alpha.*"` restored and `obj/project.assets.json` resolved `HartsyInference.Audio/2.0.0-alpha.445`.
- The central pin in `Directory.Packages.props` restored all 14 ids (`restore-check`).

Trim warnings, counted in the re-published `publish-All-Trim.log` and `publish-All-Aot.log` (the default build prints
none): IL2065 x1, IL2072 x4, IL2111 x1 in each, all in `Microsoft.AspNetCore.Components` and `Microsoft.JSInterop`. The
earlier publishes of the other variants (`publish-*-Trim.log`) show the same six, including `None` and `Own`, which
reference no HartsyInference assembly.

The sizes in `sizes.md` come from the first publish of each variant. The `All` publishes were repeated once more after
the page gained an HTTP call to post its results (11.96 MiB trimmed, 22.49 default, 13.82 AOT).
