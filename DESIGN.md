# AudioLab design

This file is the single source of truth for what AudioLab is and how it is built. [AGENTS.md](AGENTS.md) is the single
source of truth for how work is done in this repository. Nothing else is: a plan in a chat, a pull request description
or a comment is a proposal until it is merged into one of these two files.

- Code that contradicts this file is a bug in the code or in this file. Fix one of them in the same pull request.
- A decision changes only through a pull request that edits this file, approved by the owner. Add a line to the
  [change log](#13-change-log) when you do.
- Each fact lives in one place. Link to it; do not copy it.
- **Decided** means the owner chose it. **Proposed** means it is the current working assumption and the owner has not
  confirmed it; build on it, but say so when a task depends on it. **Open** means nobody has chosen yet, and the
  named spike or question decides it.

## 1. What AudioLab is

AudioLab is a digital audio workstation with AI audio built in: text to speech, speech to text, stem separation,
lyric alignment, music generation, score transcription, a voice agent and wake words, all on the
[HartsyInference](https://github.com/HartsyAI/HartsyInference) engine. It is a standalone C# application. It runs
on its own, inside a SwarmUI tab, and as a hosted site, from one codebase.

It replaces the DAW inside the [SwarmUI-AudioLab](https://github.com/HartsyAI/SwarmUI-AudioLab) extension, which
keeps the whole project in a private browser object and plays through Web Audio. That design means nothing but its own
UI can drive it (no agent, no MCP), browser audio caps real-time work, and every change needs a SwarmUI rebuild.

The extension is the behaviour specification, not a code source. Its `Assets/audio-daw*.js` say what each feature
does, and the 79 rows of its `DAW_AUDIT.md` are the regression list: each row that still applies becomes a test here.

## 2. Decisions

| # | Decision | Status | Why |
|---|----------|--------|-----|
| D1 | Own application, own repository (`HartsyAI/AudioLab`), public, MIT. Every feature works with no SwarmUI running. | Decided | The DAW outgrew an extension. |
| D2 | The server owns the project: the document, every edit, undo and redo, persistence and export. Clients send commands and render state. | Decided | A second client or an agent sees and edits the same live document. |
| D3 | C# everywhere. The UI is Blazor WebAssembly. | Decided 2026-11-20, replacing Avalonia | The browser is the main target (SwarmUI tab, hosted site). Avalonia in a browser is one canvas with no DOM, and it cannot be built up next to anything else. |
| D4 | JavaScript only by listed exception, each one second-guessed. See [JavaScript policy](#8-javascript-policy). | Decided | One typed, modular, reusable language. |
| D5 | One audio engine, written in C#. Live playback and export run the same code. | Decided | Two engines drift: the old DAW's export and playback differed (audit rows 9 and 23). |
| D6 | One UI behind three doors: a SwarmUI tab, a hosted URL, and a desktop app. See [Deployment](#9-deployment-three-doors). | Decided | The requirement is that it displays in a browser from a URL. |
| D7 | Every model runs through the HartsyInference NuGet packages, pinned to an exact version. Models are chosen by the user and routed by capability. | Decided | One engine across Hartsy projects; a floating version made builds depend on the day. |
| D8 | Agent control is an MCP server generated from the command registry. | Decided | Commands are already the complete, typed list of what can be done to a project. |
| D9 | Audio plays where the listener is. See [Audio](#6-audio). | Proposed | Streaming a server-side mix to every browser costs a mixer per listener and loses live monitoring. |
| D10 | The desktop app is the server plus a window that shows the same web UI. There is no second UI. | Proposed | The native part of a desktop DAW is the audio, and that lives in the server process, not the window. |
| D11 | MCP ships right after the command layer, before the UI is complete. | Proposed | It needs no UI, and agent control is the first reason this project exists. |
| D12 | Projects and media belong to a user from the first commit of the store. | Proposed | The hosted door is multi-user; retrofitting ownership is a rewrite. |

## 3. Architecture

```
  Blazor WebAssembly UI          MCP client          SwarmUI extension
  (AudioLab.Web)                 (agent)             (generate tab, history)
        |  commands, state, events   |                      |
        +----------------------------+----------------------+
                                     |
                           AudioLab.Server  (ASP.NET Core)
              routes - hub - sessions - store - MCP - serves the UI
                 |                   |                      |
           AudioLab.Core       AudioLab.Audio         AudioLab.Engine
           model, commands,    DSP, mixer, renderer,  HartsyInference services:
           history, rules      synth, MIDI            TTS, STT, stems, music, voice
```

One path for every edit, whoever makes it:

1. A client sends a command: a typed record from `AudioLab.Core`.
2. The server validates it, applies it to the open project under that project's lock, and records the undo step.
3. The server broadcasts the resulting change to every client of that project, and schedules an autosave.

The command record is the wire format, the undo unit and the MCP tool input. It is defined once.

## 4. Solution layout

| Project | Holds | May reference | Status |
|---------|-------|---------------|--------|
| `src/AudioLab.Core` | Project model, commands and their handlers, undo history, the pure rules (tempo map, sections, groups, clip split, lyrics). No ASP.NET, no UI, no audio I/O. | nothing in this solution | Exists (contract version only) |
| `src/AudioLab.Audio` | The engine: DSP, mixer graph, real-time and offline rendering, synth and sampler, MIDI codec. Pure C#, no native dependencies. | Core | Planned |
| `src/AudioLab.Devices` | Native audio and MIDI device I/O behind `IAudioDevice` and `IMidiInput`. | Core, Audio | Planned |
| `src/AudioLab.Engine` | HartsyInference-backed services behind interfaces: providers, model install, TTS, STT, stems, alignment, music, voice, wake word. | Core | Planned |
| `src/AudioLab.Server` | ASP.NET Core host: thin routes, the event hub, open-project sessions, the store, MCP, and serving the UI. | all of the above | Planned |
| `src/AudioLab.Web` | The Blazor WebAssembly UI. | Core, Audio | Planned |
| `src/AudioLab.Desktop` | The desktop shell: starts the server in-process and opens the window. | Server | Planned |
| `tests/AudioLab.Conventions.Tests` | Fails the build on style and policy the compiler cannot check. | none | Exists |
| `tests/<Project>.Tests` | One test project per source project. | its project | Planned |

Rules:

- Dependencies point down that table, never up. `AudioLab.Core` and `AudioLab.Audio` must run unchanged on the server
  and in WebAssembly, so they take no dependency that cannot.
- The UI and the server share `AudioLab.Core`. The UI runs the same rule code to preview an edit that the server runs
  to apply it; neither keeps its own copy of a rule.
- Routes are thin: parse, call a service, return. Logic lives in a service that a test can call without HTTP.
- A new project, or a new reference between projects, is a change to this table.

## 5. The server-owned project

- **Model.** A typed project document (`version: 4`), with a migration from the extension's `version: 3` JSON, tested
  by round-tripping real saved projects. Ids are allocated by the server. Time is seconds plus a tempo map.
- **Commands.** One registry, handlers grouped by domain (tempo, marker, section, group, track, mixer, clip, MIDI,
  effects, automation, beat, lyrics). A feature ships its commands, its undo, its UI and its tests together.
- **History.** Undo and redo on the server, coalesced by gesture: a drag is one step. History is in memory and is
  lost on restart.
- **Store.** Open documents, a lock per project, debounced autosave, atomic writes. Projects and media are scoped to
  the user who owns them (D12).
- **Hub.** Every open client of a project receives every change, including changes made by an agent.
- **Gestures.** A continuous gesture (dragging a clip, drawing automation) is previewed locally with the shared rule
  code and committed as one command on release. The UI never waits on the network to move something under the pointer.

## 6. Audio

One engine (`AudioLab.Audio`), one processing graph, used for live playback and for export (D5). Where it runs
depends on where the listener is (D9):

| Listener | Engine runs | Output |
|----------|-------------|--------|
| Desktop app, or a browser on the machine that runs the server | In the server process | The machine's audio device, through `AudioLab.Devices`. Low-latency monitoring, recording and MIDI devices live here. |
| A browser on another machine (hosted site, remote SwarmUI) | Open: see [O1](#11-open-questions) | The page |
| Export, bounce, stems | In the server process, offline | A file |

A browser Web Audio node graph written in JavaScript is rejected: it is a second engine (D5) and a large body of
JavaScript (D4).

Real-time rules, for any code reachable from the audio callback: no managed allocation, no locks, no logging, no
LINQ, no exceptions for control flow, no I/O. Buffers are pooled and sized up front. A dropout counter is part of the
engine, and a stress test reads it.

## 7. User interface

- **Blazor WebAssembly**, served as static files by `AudioLab.Server`. Not Blazor Server: that would hold UI state on
  the server and send every pointer move over the network.
- **Markup and logic are separate.** A `.razor` file holds markup only. Its logic is a `.razor.cs` partial class, so
  the compiler, the analyzers and `dotnet format` see all of it. A test fails on an `@code` block.
- **Components are small and reusable.** State and behaviour that more than one component needs is a C# service or a
  rule in `AudioLab.Core`, not a copy in each component.
- **Typed end to end.** The UI talks to the server through one typed client built on the `AudioLab.Core` records.
  No hand-written JSON, no stringly-typed route names scattered through components.
- **Styling** is plain CSS and component-scoped CSS. There is no Node toolchain in the build.
- **Dense surfaces** (timeline, waveforms, piano roll, automation) are drawn from C#: see
  [O2](#11-open-questions).

## 8. JavaScript policy

C# is the default for everything. JavaScript is allowed only where the browser offers no other way in, which today
means the audio thread (an `AudioWorklet` processor) and browser APIs that have no .NET binding (microphone capture,
Web MIDI). Before adding any script, check whether C# can do it, including through `[JSImport]` calls to browser
built-ins that need no script file.

When a script is unavoidable:

- It is as small as it can be: a shim that moves data across the boundary. Logic stays in C#.
- It is listed in [`ScriptAllowlist`](tests/AudioLab.Conventions.Tests/ScriptAllowlist.cs) with the reason C# could
  not do the job, and the owner approves that line in the pull request. That list is the full inventory.
- All interop (`IJSRuntime`, `[JSImport]`, `[JSExport]`) lives in `src/AudioLab.Web/Interop/`, behind a C# interface
  the rest of the UI calls.
- A third-party script library is a script and follows the same rule.

Tests enforce this: an unlisted `.js` or `.ts` file, an inline `<script>` block, or interop outside that folder fails
the build.

## 9. Deployment: three doors

`AudioLab.Server` serves the UI and the API from one base path, and works under any path prefix.

| Door | How it is reached | Sign-in |
|------|-------------------|---------|
| Desktop | The server runs in-process on the loopback interface; a window or a browser tab shows the UI. | None; one local user. |
| SwarmUI tab | The extension starts AudioLab as a child process and proxies it under a SwarmUI path, shown in an iframe. Same origin as SwarmUI. | SwarmUI's session and a permission, checked by the proxy. |
| Hosted | A reverse proxy maps a path on the site (for example `hartsy.ai/audiolab`) to the server. Same origin as the site. | The site's own sign-in. |

- The server resolves a user for every request from the door it came through. It never trusts an identity header that
  did not come from its own proxy.
- The child process listens on loopback only, takes a per-launch secret, and exits when its parent dies.
- Showing a hosted instance inside another site's iframe (a different origin) is out of scope for now. It needs
  `frame-ancestors`, a token handoff and a microphone and MIDI permissions policy, and nothing requires it yet.

The extension shrinks to an installer, the child process, the proxy, the tab, and the SwarmUI generate-tab
integration. Its own plan lives in that repository.

## 10. Engine services

The extension's Swarm-independent services move here behind small interfaces (logging, model download, ffmpeg
location, key storage, application paths, LLM source), so they run with no SwarmUI present. Existing weights and
installed-engine state are reused, not downloaded again. Generation that the DAW starts creates its clips on the
server, as commands.

## 11. Open questions

| # | Question | Decided by |
|---|----------|------------|
| O1 | Audio for a browser on another machine. Preferred: the same C# engine compiled to WebAssembly in the page, feeding a minimal `AudioWorklet` shim, so there is one engine and no mixer per listener on the server. Fallback: the server mixes and streams to the page. | A spike that measures dropouts and latency for the WebAssembly engine, inside an iframe without cross-origin isolation. |
| O2 | How the timeline, waveforms and piano roll are drawn from C#: a Skia canvas in Blazor, or DOM and SVG. | A spike that measures scroll and drag frame rate on a dense project. |
| O3 | The native audio device and MIDI input library. | A spike that measures round-trip latency and device enumeration, and checks license and upkeep. |
| O4 | The desktop window: an embedded webview, or the default browser at first. | Owner, after O2. |
| O5 | Score notation. The extension uses the abcjs library for rendering, playback and timing. Options: a C# renderer, or that library on the allowlist. | Owner. |
| O6 | Where a hosted instance runs: on the GPU machine with the engine in-process, or on a web host with a remote engine. | Owner. |
| O7 | How a child AudioLab process shares VRAM with SwarmUI's own HartsyInference backend. | Design work before the extension cutover. |
| O8 | The event transport: SignalR or raw WebSockets. | The first server work item. |

A spike ends by editing this file: the question moves to the decisions table with its evidence.

## 12. Roadmap

Each phase ships on its own. SwarmUI users stay on the extension's DAW until the cutover.

| Phase | Outcome |
|-------|---------|
| 0 | This repository: design, agent rules, style gates, CI. **Done.** |
| 1 | Spikes O1, O2 and O3, each closed in this file. |
| 2 | Core and server: model with v3 migration, commands, history, store, hub, routes, and a minimal UI that opens a project. |
| 3 | MCP on the command registry (D11). |
| 4 | The audio engine: offline render and export, then real-time playback. |
| 5 | The UI, one domain at a time: tempo, markers, sections and groups; tracks and mixer; clips and media; effects, automation, MIDI, piano roll and beats. |
| 6 | Engine services and the model manager. |
| 7 | Recording, native devices and the desktop shell. |
| 8 | Voice agent and wake word. |
| 9 | SwarmUI extension: child process, proxy, tab, then cutover. |
| 10 | Hosting: accounts, quotas, the job queue. |

## 13. Change log

| Date | Change |
|------|--------|
| 2026-11-20 | First version. UI changed from Avalonia to Blazor WebAssembly (D3); JavaScript by exception (D4); D9 to D12 proposed. |
