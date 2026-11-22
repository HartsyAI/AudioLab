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
| D13 | The interface follows one design system: the rules, tokens and component catalogue in [section 7](#7-user-interface), pictured by [the session mock](docs/design/session-mock.html). One dark theme, graphite with a single accent, Inter, the same in every door. | Decided 2026-11-20 | The extension's stylesheet had no scale and drifted; a fixed, tested vocabulary keeps every screen consistent. The owner approved the look from the mock. |
| D14 | Dialogs use the native `<dialog>` element, driven from C#. Menus, select, combo box, popover, tooltip and toasts come from Bit.Bheadless 11.0.0-pre-23, a headless library with no styles, behind the catalogue wrappers in [7.5](#behaviour-library). MudBlazor behind the same wrappers is a candidate fallback, proven only for one dialog and with the caveats listed there. Proof pages: [spike/o10-primitives](https://github.com/HartsyAI/AudioLab/tree/spike/o10-primitives). | Proposed | For the controls it is used for, it supplied the behaviours checked in Chromium, WebKit and Firefox; the failures are listed in 7.5. It ships no CSS and sets no stacking value, and the native dialog needs no script. It is MIT. It is three weeks old, has no stable release and has one active author, so the wrapper boundary keeps it replaceable; the conditions for Decided and for leaving it are under 7.5, with the browser and axe-core results; a screen-reader pass is not done. |
| D15 | A project file with a member this version does not know is refused, so an older build cannot read a newer file; a change of format raises `version`. | Proposed | Guessing at an unknown member would lose it silently; clip metadata is the one free-form part and keeps its unknown fields. |
| D16 | A project document is immutable and equal by value: collections are `ValueList<T>`, unknown fields are `ExtraFields`. | Proposed | Commands replace parts of a document; equality is how history and the hub tell that something changed. |
| D17 | The timeline lanes, clips and waveforms are drawn as DOM with inline SVG waveforms, each clip a small component that redraws only when its own revision changes. A Canvas2D surface driven from C# through `[JSImport]` calls to browser built-ins (no script file) is the kept alternative, and the one to switch to if dragging in Firefox cannot be made to hold on the DOM. The Skia canvas is not adopted. See [the O2 evidence](#d17-evidence-the-o2-spike). | Proposed | Scrolling meets the pass line in Chromium and Firefox with no per-frame C#, native library or script, and the clips stay in the accessibility tree. Dragging in Firefox is unresolved: every candidate missed in the runs taken under CPU contention, and no quiet window was available to repeat them. |
| D18 | A command is a sealed immutable record that names its project and carries no id the server allocates; the registry maps a stable `snake_case` wire name to a pure handler `(document, command, IIdService) -> CommandResult`. A handler refuses by result (`CommandError`: code and message), never by exception, and every accepted document passes `ProjectValidator` and keeps its id, owner and version. History keeps whole documents (D16): undo and redo restore them, so redo keeps the ids; the gesture id is an argument of the history call, not a field of the command, and an open gesture belongs to the client that sent it (an opaque `ClientId` the hub supplies, one open gesture per client), so one client's commands never close another's gesture; a step is a whole-document snapshot, so undoing it also reverts what other clients changed after it; undo keeps 100 steps by default, where the old DAW kept 30. The command JSON context must be as strict as `ProjectJsonContext`, and the registry generates each command's JSON Schema from it. | Proposed | The command is the wire format, the undo unit and the MCP tool input (section 3), so its name, schema and errors are public. A refused edit is an ordinary outcome and an exception is a bug. Snapshots are cheap because unchanged parts are shared, and re-running a handler on redo would mint new ids. The old DAW's audit rows 33, 56, 62 and 64 are one rule here: a command that changes nothing adds no step and keeps redo. |

#### D17 evidence: the O2 spike

Code: [branch `spike/o2-drawing`](https://github.com/HartsyAI/AudioLab/tree/spike/o2-drawing). Machine: OFFICE-DESKTOP, Intel Core i7-9700K (8 logical cores), 40 GB, NVIDIA GeForce RTX 3060 (Chrome reports hardware-accelerated canvas, rasterisation, compositing and WebGL), Windows 11 Pro 26200, a 2560 x 1440 display at 100 Hz and 125% scaling, headed Chrome 157.0.8103.79 and Firefox 160.0.1, lane area about 1740 x 600 CSS pixels. The session screen was rebuilt in Blazor WebAssembly (.NET 10.0.14, interpreted unless stated) with 32 tracks and 500 clips with cached peaks at the mock's 68 pixel bars, about 57 clips in view. Scripted scenarios ran for 6 s each, three times: scroll across, scroll down, both, drag a clip across tracks, and pick a clip up and drop it. The pass line is read as: median frame interval 16.7 ms or less, and no one-second window under 45 fps.

| Candidate | Scrolling, Chrome and Firefox | Dragging, Chrome | Dragging, Firefox | JavaScript policy (section 8) | Brotli size | Accessibility |
|-----------|-------------------------------|------------------|-------------------|-------------------------------|-------------|---------------|
| DOM + SVG, every clip in the page | Pass: median 10 ms, worst second 99 fps or better, no frame over 22 ms | Pass: worst second 97 fps | Weakest: worst second 58 fps in the best batch, 18 to 37 in the loaded ones | None | 2.7 MB (baseline) | Clips are elements; names, roles and tab stops attach to them |
| Canvas2D through `[JSImport]` | Pass: 1.7 ms (Chrome) and 2.7 ms (Firefox) of C# a frame, about 1,400 calls; worst second 92 fps or better | Pass: worst second 85 fps | Worst second 91 fps in the best batch, 14 to 63 in the loaded ones | About 25 imports of built-ins in `Interop/`, no script file | Baseline | One canvas: no clips, no keyboard stops; a parallel DOM layer of the visible clips would be needed |
| SkiaSharp 4.157.0 on WebGL | Pass: worst second 99 fps or better | Pass: worst second 90 fps | Worst second 68 fps in the best batch, 14 to 36 in the loaded ones | Four package scripts (10.8 KB) for a by-package allowlist entry | +1.3 MB | As Canvas2D |
| SkiaSharp on the CPU | Miss: median 20 ms in Chrome vertical scroll (a 12 to 14 ms repaint at 100 Hz), worst second 56 fps; Firefox worst second 32 fps | Miss: worst second 44 fps | Miss: worst second 22 fps | As Skia | As Skia | As Canvas2D |

How to read the table. Scrolling is clean for the three passing candidates. Dragging is not: the drag batches ran while other sessions kept all 8 logical cores near 100%, and in the A/B repeats taken under that load every candidate fell under 45 fps in Firefox at some point, the DOM most often (mean 60 to 67 fps against 55 to 98 for Canvas2D). The earlier scripted drag, taken on a quieter machine and before pick-up and drop were inside the measured window, passed for all three in both browsers. So the pass line is met in Chromium, met for scrolling in Firefox, and **not shown to be met for dragging in Firefox**; a rerun on an idle machine is the first step of phase 5's timeline work. Real wheel and pointer input in Chrome (through the devtools protocol) completed for all five candidates, the DOM with one 100 ms frame on pick-up; it was a functional check, and its smooth Skia CPU result disagrees with the scripted run and is unexplained.

Flicker, checked late. Real wheel scrolling in headed Chrome, screen captures of the composited output compared with the DOM ruler (56 to 120 usable captures a candidate): DOM + SVG stayed aligned (1 of 63 captures off by more than 3 device pixels), but the canvases lagged the ruler by up to 22 CSS pixels (Canvas2D 5 of 76, Skia GL 5 of 56, Skia CPU 2 of 91), because a viewport-sized sticky canvas repainted on the next frame trails the compositor's scroll. That counts against "looks like the mock", so the canvas surfaces were changed: the canvas is 320 px wider than the view on every side, scrolls with the page, and is repainted and moved in the same task only when the view comes within 80 px of its edge; during a drag only the view is repainted. After: 0 of 88 captures off for DOM, 2 of 67 for Canvas2D (largest 12 device pixels, not explained), 0 of 71 for Skia GL, 0 of 73 for Skia CPU. Scripted Chrome runs after the change held (Canvas2D and Skia GL: worst second 96 fps or better, at most one frame over 22 ms); in Firefox a Canvas2D drag ran at 87 to 96 fps in three of four runs and 58 in one under load. The scroll and drag numbers in the table above were taken before this change.

Firefox, captured afterwards (Playwright's Firefox 159 for the flicker captures, stock Firefox 160 for the scripted runs). Flicker: DOM + SVG stayed aligned (0 of 120 captures off by more than 3 device pixels). Before the change the canvases lagged badly (Canvas2D 48 of 49 measurable captures, Skia GL 15 of 36, Skia CPU 11 of 27); after it they still lagged in 5 of 30, 8 of 35 and 0 of 12, so the overscan fix is not enough in Firefox and the canvases flicker there. The scripted scroll and drag runs on the final build ran while other sessions held the CPU near 100% (C# draw cost 5 to 10 times the earlier figures), and every candidate missed: worst second 59 to 66 fps scrolling and 9 to 14 in the drag-clip scenario for DOM + SVG, 31 to 46 and 0 to 9 for Canvas2D, 12 to 26 scrolling for Skia GL, 2 to 4 for Skia CPU. Those runs cannot separate the candidates, so they are reported as misses, not as a ranking; no quiet machine was available to repeat them.

Findings that change how the DOM is built. Dimming or hiding the origin of a lifted clip while the lifted clip carries the overlay shadow froze Firefox for seconds (no frame in 7 s); leaving the origin untouched removed the freeze. The lifted clip moves by `left` and `top`; moving it with a transform was not tried. Virtualising the SVG (only clips near the view) passes scrolling but costs 9 to 13 ms of C# a frame and frames over 22 ms, so every clip stays in the page. Looks: transport and ruler pixel-identical to the mock in headless Chromium at 1440 x 900, and in the first six tracks 0.3% of pixels differ for SVG and 2.3% to 2.7% for the canvases (text and edge antialiasing, Skia's header text visibly lighter); Firefox was not compared with the mock.

A harder view, outside the pass line: 2,000 clips at 17 pixel bars, about 215 in view. Chrome holds with all three (worst second 77 fps DOM, 83 Canvas2D, 100 Skia GL). Firefox holds only with Skia GL (81 fps; DOM 42, Canvas2D 31), and an AOT build does not change that, so Firefox's canvas and layout cost is the limit, not C#. Skia is the fallback if a real view needs that density.

Costs and limits. Skia links a native library into `dotnet.native.wasm`, needs the `wasm-tools` workload to build (CI has none), and its assembly cannot be AOT-compiled (the Mono AOT compiler crashes on `SkiaSharp.dll` with SDK 10.0.403). AOT of the other two works for +2 MB brotli and changed little (the Canvas2D frame cost fell from 1.7 to 1.2 ms in Chrome). Frame intervals come from `requestAnimationFrame`, so they show main-thread work, not frames the compositor alone dropped. Not run live: the piano roll (it uses the same primitives as clip notes), Skia's accessibility tree (inferred from its single canvas), and Firefox's graphics status. Early on, about one in five fresh-profile launches stayed on a blank page. Late in the work this was traced mostly to the spike's own test server, whose five-connection listen backlog refused some of the browser's parallel downloads; with the backlog raised, 150 concurrent requests succeed and Chrome starts first time. Firefox launches from the test harness still failed now and then, cause not found. Read the launch failures as harness noise, not as evidence about Blazor.

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
| `src/AudioLab.Core` | Project model, commands and their handlers, undo history, the pure rules (tempo map, sections, groups, clip split, lyrics). No ASP.NET, no UI, no audio I/O. | nothing in this solution | Exists (contract version, project model v4, id service, JSON contract, command registry and history) |
| `src/AudioLab.Audio` | The engine: DSP, mixer graph, real-time and offline rendering, synth and sampler, MIDI codec. Pure C#, no native dependencies. | Core | Planned |
| `src/AudioLab.Devices` | Native audio and MIDI device I/O behind `IAudioDevice` and `IMidiInput`. | Core, Audio | Planned |
| `src/AudioLab.Engine` | HartsyInference-backed services behind interfaces: providers, model install, TTS, STT, stems, alignment, music, voice, wake word. | Core | Planned |
| `src/AudioLab.Server` | ASP.NET Core host: thin routes, the event hub, open-project sessions, the store, MCP, and serving the UI. | all of the above | Planned |
| `src/AudioLab.Web` | The Blazor WebAssembly UI. | Core, Audio | Planned |
| `src/AudioLab.Desktop` | The desktop shell: starts the server in-process and opens the window. | Server | Planned |
| `tests/AudioLab.Conventions.Tests` | Fails the build on style and policy the compiler cannot check. | none | Exists |
| `tests/AudioLab.Core.Tests` | Model, ids, JSON round trips and rejection; command registry and history. | Core | Exists |
| `tests/<Project>.Tests` | One test project per other source project. | its project | Planned |

Rules:

- Dependencies point down that table, never up. `AudioLab.Core` and `AudioLab.Audio` must run unchanged on the server
  and in WebAssembly, so they take no dependency that cannot.
- The UI and the server share `AudioLab.Core`. The UI runs the same rule code to preview an edit that the server runs
  to apply it; neither keeps its own copy of a rule.
- Routes are thin: parse, call a service, return. Logic lives in a service that a test can call without HTTP.
- A new project, or a new reference between projects, is a change to this table.

## 5. The server-owned project

- **Model.** A typed project document (`version: 4`), the only project format, tested by round trips and by the
  rejection of invalid documents. Ids are allocated by the server. Time is seconds plus a tempo map.
  Ids are typed (`TrackId`, `ClipId`, `MediaId`, ...) and minted only by `IIdService`; the project and every media
  record carry the owner `UserId` (D12). JSON goes only through `ProjectJsonContext`, which refuses unknown members.
  Colours are `#rgb`, `#rrggbb` or `#rrggbbaa` until the UI maps them to the twelve track tokens.
  - Ids are 1 to 64 of `a-z0-9_-` and never a device name, so an id holds no separator and two ids never differ only
    by case. A `UserId` is `<door>-<subject>` in lower-case letters and digits, so one door cannot mint another's. The
    store maps an id to a name of its own and never puts a raw id in a path, and it takes the owner from the session
    that made the request, never from the `owner` a file names. A note id counts up inside its clip and
    `MidiClip.NextNoteId` never goes back, so the id of a deleted note is not reused.
  - Immutability and equality are D16. Enums are written by exact name and refuse numbers and variants of a name.
  - Compatibility is D15. `ProjectLimits` bounds numbers, list lengths, text lengths and the size of unknown fields,
    text holds no control or text-direction characters, and an error lists at most 100 problems of at most 300
    characters each.
- **Commands.** One registry, handlers grouped by domain (tempo, marker, section, group, track, mixer, clip, MIDI,
  effects, automation, beat, lyrics). A feature ships its commands, its undo, its UI and its tests together. Commands
  may land ahead of their UI when MCP (D11) needs them; the feature is complete only when its UI ships. The shape of a
  command, a handler, a refusal and the registry's listing is D18. A client never sends an id for something the
  command creates; the result lists the ids the handler allocated.
- **History.** Undo and redo on the server, coalesced by gesture: a drag is one step. History is in memory and is
  lost on restart. A gesture belongs to the client that sent it. Its commands each apply and show at once and share
  one undo step, recorded when the gesture closes. The same client sending another gesture or a single edit closes it,
  and so does undo, redo or the release that names it. A gesture that ends where it began leaves undo and redo exactly
  as they were. A client that previews locally (below) sends one command on release, a gesture of one.
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

### 7.1 Technology

- **Blazor WebAssembly**, served as static files by `AudioLab.Server`. Not Blazor Server: that would hold UI state on
  the server and send every pointer move over the network.
- **Markup and logic are separate.** A `.razor` file holds markup only. Its logic is a `.razor.cs` partial class, so
  the compiler, the analyzers and `dotnet format` see all of it. A test fails on an `@code` block.
- **Components are small and reusable.** State and behaviour that more than one component needs is a C# service or a
  rule in `AudioLab.Core`, not a copy in each component.
- **Typed end to end.** The UI talks to the server through one typed client built on the `AudioLab.Core` records.
  No hand-written JSON, no stringly-typed route names scattered through components.
- **Styling** is plain CSS and component-scoped CSS. There is no Node toolchain in the build. Fonts are files served
  by the app, not a hosted font service.
- **Dense surfaces** (timeline, waveforms, piano roll, automation) are drawn from C#, as [D17](#2-decisions) decides:
  DOM with inline SVG by default. They take their colours and sizes from the same tokens as the CSS: C# reads each
  token it draws with from the root element's computed style, once at start, so `tokens.css` stays the only place a
  value is declared.

### 7.2 Character

AudioLab looks like studio equipment, not a web page: graphite, dense, quiet and exact. The screen belongs to the
arrangement; everything around it recedes until it is needed.

**The picture of this section is [docs/design/session-mock.html](docs/design/session-mock.html)**: the session screen
at full fidelity, built only from the tokens in 7.4. Open it in a browser before any UI work, at 1440 pixels wide or
more. This section is the authority: where the mock and this text differ, the mock is wrong and gets fixed.

The layout comes from the extension's DAW, which already reads as a professional workstation. Its styling does not:
that stylesheet took every colour from SwarmUI's theme (675 theme-variable uses) and grew 32 font sizes, 22 corner
radii and 12 stacking values with no scale behind them. The rules below exist so that cannot happen again.

| Rule | What it means | Why |
|------|---------------|-----|
| **Controls are filled, not outlined** | A control is a filled shape one step lighter than the surface it sits on. A set of exclusive choices sits in a well, with the chosen one raised. Nothing gets an outline to mark it as a control. | Outlines on every control read as a wireframe. The first draft of this design did exactly that. |
| **Regions are split by dark lines** | Two surfaces meet at a `--line` seam, darker than both. Never a lighter border. | A light border draws a grid of boxes; a dark seam reads as two pieces of equipment. |
| **Depth is small and means something** | A raised control carries a one-pixel top highlight. A well (a counter, a field, the slot a fader runs in) is recessed. Only overlays, fader caps and a dragged clip cast a shadow. No decorative gradients: the only gradients are a meter's scale and the grid. | Depth that marks "pressable" or "holds a value" is information. Depth as decoration is noise. |
| **Colour says state** | Chrome is graphite. A colour appears only to say something: engaged, recording, playing, signal level, a warning, or which track a thing belongs to. | When everything is coloured, the one control that is armed looks like the others. |
| **Red means recording or destroying** | Record, arm, punch, clipping and delete. Nothing else is red; the playhead is white and turns red only while recording. | Red is the one colour a musician must never have to interpret. |
| **Selection is white, engagement is accent** | The selected clip takes a white inner ring and a stronger body; the selected track's header and lane lighten. Accent marks what is switched on: an engaged toggle, the loop range, the active tab, the primary action. | Accent can match a track colour, and a selected thing is not a switched-on thing. |
| **Ink on fills is dark** | Text on any coloured fill uses `--ink`. | White on these fills misses the contrast floor; one ink colour keeps every fill legible. |
| **One scale** | Every colour, font size, font family, radius, shadow and stacking value is a token from 7.4. No literals in component styles. | See the numbers above. |
| **Numbers do not move** | Every number that changes while you watch it (time, bars, dB, BPM, percent) uses tabular figures in the interface face. The mono face is only for values copied verbatim: paths, seeds, ids. | Digits that change width make a display jitter; a second typeface in the counters makes them look bolted on. |
| **The timeline never waits** | A busy state belongs to the thing that is busy: a clip, a track, a panel. Nothing covers the arrangement or disables the transport while a job runs. | Generation takes seconds to minutes; the session must stay playable. |
| **Fill the container** | The app sizes to its container (`100%`), never to the viewport (`vh`, `dvh`). | It runs inside a SwarmUI tab, under site chrome, and in a window; the viewport is not its space. |
| **Words on decisions, icons on reflexes** | Transport and M / S / R / I controls may be a glyph or a letter. Anything a user has to read to decide carries a text label. Every icon-only control has an accessible name and a tooltip with its shortcut. | A bare glyph asks the user to remember a legend. |
| **Sentence case** | Labels, buttons, tabs and menus are sentence case. Capitals only on micro captions (`TIME`, `BARS`, `TRACKS`) and single-letter track toggles. | Capitals on everything shouts; kept to captions they separate a label from its value. |
| **Same in every door** | One dark theme. Inter ships with the app as font files, so it looks the same on every OS. It does not inherit SwarmUI's theme or the host site's styles. | The product should be recognisable wherever it is opened, and inherited styles are how the last stylesheet lost its shape. |

### 7.3 Layout

One screen, the session, with fixed regions. Other screens (projects, model manager, settings) use the same tokens
and components and have no layout of their own beyond a centred column of panels.

| Region | Holds | Rules |
|--------|-------|-------|
| Transport bar | Project name and save state; transport (to start, stop, play, record) in one well; the counter (time, bars, tempo, meter, key); toggles (loop, click, punch, follow); tool and grid; who is in the session; export. | `--bar-height`, `--surface-panel`. Controls are grouped; groups wrap to a second row as a unit and never clip or scroll. |
| Overview | The whole song at a glance, with the visible window marked. | Optional, a viewer preference. Not in the mock. |
| Rulers | A corner cell (the `TRACKS` caption, Add track), then rows: sections and bars always; time, tempo and markers on request. The loop range and the playhead's cap sit in the bars row. | Rows of `--ruler-row`, sticky above the lanes. Only the visible slice is drawn. |
| Header column | One header per track: colour strip, number, name, M / S / R / I, source, volume with its dB readout, stereo meter. | `--header-column` wide, sticky at the left, `--surface-panel`. Each header is `--lane-height`. |
| Lanes | Clips, MIDI clips, automation, the selection, the loop range, the playhead. | `--surface-lane`, the darkest working surface, so clips carry the colour. A `--grid-line` per bar and a `--grid-line-strong` every four. Scrolls both ways under the rulers and headers. |
| Bottom panel | Tabs for the selection's tools: mixer, clip editor, piano roll, effects, instruments, stems, score, lyrics, voice agent. Generate is docked at its right edge and stays there whichever tab is open. | Resizable and collapsible. Tabs are `--tab-height`. A tab shows its own empty state when nothing is selected. |
| Status bar | Connection and save state, who else is in the project (including agents), running jobs with progress, sample rate and buffer, engine load, dropouts. | `--status-height`, `--surface-lane`. New in AudioLab: the server owns the project, so its state has to be visible. |
| Overlays | Menus, dialogs, tooltips, toasts. | The only regions with `--shadow-overlay`. Stacked by the `--z-*` tokens. |

Built for a pointer and a keyboard at 1280 pixels wide and up, usable down to 1024. Phones are out of scope.

### 7.4 Tokens

These are every value a style may use. The mock and `src/AudioLab.Web/wwwroot/css/tokens.css` each declare exactly
this list on `:root`, and tests fail if either differs. To change or add a value, edit it here and in those files in
the same pull request, and look at the mock afterwards.

<!-- tokens:start -->
```css
/* Surfaces, deepest to highest. One cool graphite, stepped by lightness. */
--surface-well: #0d0f12;
--surface-lane: #14171b;
--surface-panel: #1b1e23;
--surface-selected: #23272e;
--surface-raised: #282c34;
--surface-raised-active: #30353d;

/* Lines. Regions are split by a line darker than both sides, never a lighter outline. */
--line: #08090b;
--lane-line: rgba(0, 0, 0, 0.5);
--grid-line: rgba(255, 255, 255, 0.03);
--grid-line-strong: rgba(255, 255, 255, 0.08);
--tick: rgba(255, 255, 255, 0.22);
--highlight: rgba(255, 255, 255, 0.06);

/* Text: neutral, four steps. */
--text: #e7e9ec;
--text-soft: #c9ced6;
--text-dim: #a8aeb7;
--text-faint: #8a919b;

/* State: the only saturated colours in the chrome. --ink is the text on any of them. */
--accent: #4a9df0;
--record: #f0524a;
--go: #45d18f;
--caution: #f3c94f;
--mute: #f29a4c;
--ink: #0b0d10;
--playhead: #ffffff;

/* Control parts. */
--control-fill: #59606b;
--control-cap: #d5dae0;

/* Identity: twelve track colours of similar weight, in hue order. */
--track-1: #ee6d86;
--track-2: #ef7d67;
--track-3: #ee9a55;
--track-4: #e3ad4f;
--track-5: #a9cf5b;
--track-6: #5fcf95;
--track-7: #4fcdb8;
--track-8: #4fbfdc;
--track-9: #6c93f2;
--track-10: #a487ee;
--track-11: #cf7de0;
--track-12: #e07ab5;

/* Type: Inter, seven sizes, three weights. */
--font-ui: Inter, "Segoe UI", system-ui, sans-serif;
--font-mono: ui-monospace, "Cascadia Mono", Consolas, monospace;
--font-size-micro: 0.59375rem;
--font-size-label: 0.65625rem;
--font-size-control: 0.71875rem;
--font-size-body: 0.75rem;
--font-size-title: 0.8125rem;
--font-size-readout: 0.9375rem;
--font-size-heading: 1rem;
--font-weight-normal: 400;
--font-weight-medium: 500;
--font-weight-strong: 600;

/* Space: 2-pixel steps, named by their pixel size. */
--space-2: 0.125rem;
--space-4: 0.25rem;
--space-6: 0.375rem;
--space-8: 0.5rem;
--space-10: 0.625rem;
--space-12: 0.75rem;
--space-14: 0.875rem;
--space-16: 1rem;
--space-24: 1.5rem;

/* Size: control heights and the fixed parts of the layout. */
--control-xs: 1.125rem;
--control-sm: 1.375rem;
--control-md: 1.75rem;
--control-lg: 2rem;
--bar-height: 3.25rem;
--tab-height: 2.125rem;
--status-height: 1.5rem;
--ruler-row: 1.375rem;
--lane-height: 4rem;
--header-column: 15.5rem;
--strip-width: 6.5rem;

/* Shape and depth. */
--radius-xs: 0.125rem;
--radius-sm: 0.25rem;
--radius-md: 0.375rem;
--radius-lg: 0.5rem;
--radius-round: 999px;
--shadow-raised: inset 0 1px 0 var(--highlight);
--shadow-well: inset 0 0 0 1px var(--line), inset 0 2px 5px rgba(0, 0, 0, 0.55);
--shadow-cap: 0 2px 3px rgba(0, 0, 0, 0.65), inset 0 1px 0 rgba(255, 255, 255, 0.7), inset 0 -1px 0 rgba(0, 0, 0, 0.25);
--shadow-overlay: 0 0 0 1px var(--line), 0 12px 32px rgba(0, 0, 0, 0.6);
--focus-ring: 0 0 0 2px var(--accent);

/* Stacking, lowest to highest. */
--z-lane-overlay: 10;
--z-sticky: 20;
--z-popover: 100;
--z-dialog: 200;
--z-toast: 300;
--z-tooltip: 400;

/* Motion. */
--motion-fast: 80ms;
--motion-base: 160ms;
```
<!-- tokens:end -->

What each group is for:

| Tokens | Used for | Why these values |
|--------|----------|------------------|
| `--surface-well` | The counter, fields, the group behind a set of exclusive choices, meter and fader slots. Always with `--shadow-well`. | The deepest tone: a place a value sits in. |
| `--surface-lane` | Arrangement lanes, piano-roll and automation grids, the status bar, effect slots. | Dark enough that a clip in a track colour is the brightest thing on screen. |
| `--surface-panel` | Transport bar, header column, rulers, bottom panel. | The chrome. |
| `--surface-selected` | The selected track's header; the master strip. | One step up from the panel marks "this one" without colour. |
| `--surface-raised`, `--surface-raised-active` | Buttons, off toggles, selects, knobs; the chosen item in a well, and a raised control under the pointer. | Lighter than the panel says "this can be pressed". |
| `--line`, `--lane-line` | The seam between regions; the seam between lanes. | Darker than every surface. |
| `--grid-line`, `--grid-line-strong`, `--tick` | Bar lines; four-bar lines and minor ruler ticks; major ruler ticks. | White at low alpha, so the grid sits on any lane tint. |
| `--highlight` | The top edge of a raised control, through `--shadow-raised`. | The one-pixel cue that a surface is raised. |
| `--text`, `--text-soft`, `--text-dim`, `--text-faint` | Primary text and values; labels on raised controls; secondary text and off-toggle legends; captions, units, placeholders. | Neutral, so text never reads as tinted. Each meets 4.5:1 on every surface, except that `--text-faint` is not used on a raised surface. Nothing is fainter. |
| `--accent` | Engaged toggles, the loop range, the active tab, progress, the primary button. | One highlight colour. It is a single token so the brand can move it. |
| `--record` | Record, arm, punch, the playhead while recording, meter clipping, destructive actions. | See "Red means recording or destroying". |
| `--go` | Play while playing, input monitoring, the safe range of a meter, connected, success. | |
| `--caution` | Solo, the hot range of a meter, warnings. | Solo is yellow on most consoles. |
| `--mute` | Mute. | Orange, so mute and solo differ at a glance and neither is red. |
| `--ink`, `--playhead` | Text and glyphs on any coloured fill; the playhead and the selection ring. | |
| `--control-fill`, `--control-cap` | The filled part of a slider; a fader cap and a slider handle. | The cap is the lightest thing in the chrome because it is the thing you grab. |
| `--track-1` to `--track-12` | A track's colour, and so its clips, its mixer strip, its group. Markers and sections pick from the same twelve. | Similar weight: no track looks more important than another, and `--ink` is legible on all of them. |
| `--font-ui`, `--font-mono` | Everything; values copied verbatim. | Inter: built for dense interfaces, with tabular figures. |
| `--font-size-*` | micro: captions, meter scales. label: track-toggle letters, source lines, readouts beside controls, status bar. control: buttons, toggles, selects. body: names, tabs, fields. title: project name, panel titles. readout: the counter. heading: dialogs and empty states. | Seven sizes replace thirty-two. Nothing is smaller than micro (9.5 pixels), and micro is for captions and scales only. |
| `--font-weight-*` | normal: running text. medium: control labels and secondary values. strong: names, values, engaged states. | Emphasis comes from weight, not colour. |
| `--space-*` | Every gap and padding. | Two-pixel steps: a dense screen needs finer steps than a four-pixel grid. |
| `--control-*` | xs: track toggles. sm: small buttons in rulers and panels. md: transport, toggles, selects. lg: a panel's main action. | Dense, and still a reliable pointer target. |
| `--bar-height` to `--strip-width` | The fixed parts of the layout in 7.3. | |
| `--radius-*` | xs: meters and slider slots. sm: track toggles, clips, effect slots. md: buttons, selects, fields. lg: wells and groups. round: avatars, dots. | Small radii read as equipment; large ones read as a consumer app. |
| `--shadow-raised`, `--shadow-well`, `--shadow-cap`, `--shadow-overlay` | Raised controls; wells; fader caps and slider handles; menus, dialogs, tooltips. | The whole depth vocabulary. A shadow that is not one of these is not allowed. |
| `--motion-*` | fast: hover and press. base: a panel opening, a menu appearing. | Nothing animates in time with the music except the playhead and the meters, and those are drawn directly, not transitioned. Motion stops under `prefers-reduced-motion`. |

Tints are made at the point of use with `color-mix` over tokens, never with a new literal. A clip's body is its track
colour at 17% over the lane (30% when selected), the loop range is the accent at 9% over the lanes and 30% in the
ruler, and a section is its colour at 22%.

### 7.5 Components

The interface is built from this catalogue. Each row is one Razor component in `src/AudioLab.Web/Components/`, styled
by its own scoped CSS, configured by parameters. A screen composes them and adds layout only: it does not restyle a
component, and it does not hand-build a control the catalogue has. A new component, or a new variant of one, is a
change to this table.

No styled component library sets the look. Everything on the session screen is ours, because no library has it.
Where a library helps is behaviour that is hard to get right (focus trapping, menus, popover positioning, combo
boxes, tables); the catalogue component wraps it, so the look and the API stay ours: see [D14](#2-decisions) and
[the behaviour library](#behaviour-library).

| Component | Variants | Rules |
|-----------|----------|-------|
| Button | default, primary, danger, icon | `--surface-raised` with `--shadow-raised`, `--radius-md`, control size, medium weight. Primary is `--accent` with `--ink`: one per panel or dialog. Danger uses `--record` and is never the default action. |
| Toggle | labelled, track (M, S, R, I) | Strong weight in both states. Off: `--surface-raised` with `--text-dim`. On: filled with its state colour (`--accent`, or mute, caution, record, go for M, S, R, I) and `--ink`. Track toggles are `--control-xs` high letters with `--radius-sm`. |
| Segmented control | transport, tool, mode | A `--surface-well` group with `--shadow-well` and `--radius-lg`; items are `--radius-md`. The chosen item is `--surface-raised-active`; the rest are bare glyphs or labels. The transport uses raised items, and play fills with `--go` while playing. |
| Counter | | One well, cells split by a `--surface-panel` line. Each cell is a micro caption in `--text-faint` over its value. Time and bars are readout size; tempo, meter and key are body size in `--text-soft`. Click swaps or edits a cell. |
| Select and menu | dropdown, context | The closed select is a default button with its value and a chevron. A menu is `--surface-raised` with `--shadow-overlay`; every command shows its shortcut; destructive items sit last, in `--record`. |
| Popover | | `--surface-raised`, `--shadow-overlay`, `--radius-md`, `--space-12` padding. Opens from a default button, closes on Escape and on a click outside, and returns focus to the button. |
| Combo box | | A field whose list is a menu. Filters as you type, announces the result count, Enter picks, Escape closes. An empty result says "No matches". |
| Field | text, number, multi-line | `--surface-well`, `--shadow-well`, `--radius-md`, body size. A number field scrubs by vertical drag, steps with the arrow keys and the wheel, and resets on double-click. |
| Checkbox | | A `--radius-sm` box: `--surface-well` when off, `--accent` with an `--ink` tick when on. Its label is the click target too. |
| Slider | track volume | A `--surface-well` slot with `--control-fill` up to the value and a `--control-cap` handle. Its value sits beside it, label size. Same gestures as a number field. |
| Fader | channel | A vertical slot, a `--control-cap` cap with `--shadow-cap` and a centre line, a dB scale in micro at its left. Same gestures as a number field. |
| Knob | pan, send, parameter | A `--surface-raised` disc with a `--line` edge and a `--text` pointer. Its value sits beside it. Same gestures as a number field. |
| Meter | mono, stereo, input | A `--surface-well` slot. `--go` up to -12 dBFS, `--caution` to -3, `--record` above, as hard steps. A clip light latches at 0 dBFS until clicked. Drawn directly; no transition. |
| Tabs | bottom panel | Body size. The active tab is `--text`, strong, with a two-pixel `--accent` underline; the rest are `--text-dim`, medium. |
| Panel | | A titled region of `--surface-panel` split from its neighbours by `--line`. No shadow, no nested panels more than one deep. |
| Dialog | confirm, form | `--surface-raised`, `--radius-lg`, `--shadow-overlay`. Traps focus, closes on Escape, names the action on its button ("Delete project", not "OK"). |
| Tooltip | | Name, then shortcut. Appears after a pause, never over the thing it describes. |
| Toast | info, success, warning, error | Says what happened and offers Undo where there is one. An error stays until dismissed. |
| Empty state | lanes, panel tab, screen | A heading, one sentence, and the one to three actions that get the user moving. No illustration. |
| Progress | bar, inline spinner | Attached to the thing that is busy, with a cancel control. A known duration is an `--accent` bar in a well; an unknown one is a spinner with elapsed time. |
| Presence | | Overlapping round avatars for everyone in the session. An agent takes a track colour and the label AI. |
| Track header | audio, instrument, bus, group | Left to right: a four-pixel strip in the track colour, the number in `--text-faint`, then two rows (name with M / S / R / I; source with the volume slider and its dB), then the meter. Selected: `--surface-selected`. |
| Clip | audio, MIDI, recording, generating | `--radius-sm`. A title bar in the track colour with the name in `--ink`; the body is the track colour at 17% with a 50% inner ring, holding the waveform or notes in the track colour. Selected: a white inner ring and a 30% body. Muted: 40% opacity. Fade and trim handles appear on hover or selection. Generating shows progress inside its own bounds. |
| Ruler row | sections, bars, time, tempo, markers | Label size. Bar numbers sit right of their tick; every fourth is `--text-soft`, the rest `--text-faint`. A section is a `--radius-sm` block in its colour at 22%. |
| Playhead and ranges | playhead, selection, loop, punch | A one-pixel `--playhead` line with a cap in the bars row. Loop is an `--accent` band between two accent edges in the ruler and a wash over the lanes; punch is the same in `--record`. |
| Channel strip | track, bus, master | `--strip-width`, split by `--line`. Top to bottom: colour dot and name, effect slots, pan knob with its value, then scale, fader and meter side by side, then M / S and the dB readout. The master is wider and `--surface-selected`. |
| Effect slot | filled, empty | A `--surface-lane` slot with `--shadow-well`, label size. Empty reads "Add effect" in `--text-faint`. |
| Piano keys and grid | | Keys at the left of the piano roll; notes in the track colour, velocity as brightness. |
| Status item | state, presence, job, metric | Label size in `--text-faint`, with a six-pixel dot for state. A job shows its name, a bar and a percentage. |

#### Behaviour library

Evidence for [D14](#2-decisions), measured on OFFICE-DESKTOP (Windows 11, .NET SDK 10.0.403); figures that change over time
are as of 2026-11-21. Sizes are a trimmed `dotnet publish -c Release` of a page that uses each library's own dialog,
menu, popover, tooltip and select or combo box (and a toast where it has one), against an empty app made from
`dotnet new blazorwasm --empty -f net10.0` (9.09 MB, 2.70 MB brotli). The proof pages, which render each control from
`tokens.css` and pass the conventions tests, are the branch
[spike/o10-primitives](https://github.com/HartsyAI/AudioLab/tree/spike/o10-primitives).

| Library | Licence | Script it ships | Covers | Cost, and verdict |
|---------|---------|-----------------|--------|-------------------|
| Bit.Bheadless 11.0.0-pre-23 | MIT | 3 files (93,794 bytes), loaded by Blazor with no script tag | Dialog, alert dialog, menu, context menu, popover, tooltip, listbox, combo box, toasts, tabs, grid, tree, number field. No CSS. Sets no stacking value on the parts D14 uses; its dialog, alert dialog and drawer write an inline `z-index` of 50 or 51 unless given a class. | +0.85 MB, +0.27 MB brotli. Preferred (Proposed). |
| BlazorBlueprint.Primitives 4.2.0 | Apache-2.0 | 32 files, 293 KB | Everything but combo box and toast. Stacking is an integer (a `ZIndex` parameter, default 9999 on the popover; inline values of 40, 50 and 9999 and a computed one seen), not a CSS variable, so tokens cannot drive it. | +1.59 MB, +0.44 MB brotli. Second. |
| MudBlazor 9.14.0 | MIT | 1 file, 75 KB, and a 638 KB stylesheet that sets the look | Dialog, menu, popover, select, autocomplete, snackbar, table. | +2.45 MB, +0.55 MB brotli. Candidate fallback, see below. |

| Library | Accessibility evidence | Upkeep |
|---------|------------------------|--------|
| Bit.Bheadless | Its own tests (read, not run): 629 unit tests, 46 of them for its own audit helpers rather than the controls, and 75 Playwright tests in Chromium-based browsers only, including an axe-core audit (WCAG 2.2 A and AA) of every demo page. Here: driven by hand in Chromium 156, then scripted with axe-core in Chromium 159, WebKit 27.2 and Firefox 159 (results below). Not run in Safari itself, no screen reader. | The code entered the bitplatform monorepo on 2026-10-28 (the 1,381 stars belong to the monorepo) and the package was first published on 2026-10-30: 584 downloads, against 204,652 for BlazorBlueprint.Primitives and 43.2 million for MudBlazor. 19 commits since: 7 features and fixes by one person, 12 by a second committer, mostly dependency bumps and pipelines. Versions follow the monorepo's pre-release train; no stable version. The API moved inside the pinned window (namespaces simplified on 2026-11-18). |
| BlazorBlueprint.Primitives | Interaction tests in Chromium and WebKit; an axe-core run (WCAG 2.0 to 2.2 A and AA) over every demo in Chromium only; it says it is not a screen-reader certification (read, not run). | 54 versions since 2026-02, stable 4.2.0 on 2026-11-07, 700 stars, 7 open issues and 3 pull requests, one author with 883 commits (the next has 17). |
| MudBlazor | Not examined beyond a count: 33 open issues labelled accessibility, and no keyboard or axe suite found. | 225 versions, a release every one to three weeks (9.14.0 on 2026-11-16), 10.7k stars, 537 open issues and 91 open pull requests. |

Publishing the Bit.Bheadless page with trim warnings on gives eight distinct warnings, none in the library (all are in
the Blazor framework and the template). With `RunAOTCompilation` the same pages publish with no warnings: 4.59 MB
brotli for the empty app, +0.81 MB with Bit.Bheadless, +1.08 MB with BlazorBlueprint.Primitives. AOT output was built,
not opened in a browser.

**Why not native elements and C# alone (D4).** A dialog needs no script. The proof page has one on the native `<dialog>`
element, opened through `[JSImport]` calls to browser built-ins and a `close` listener added the same way (Blazor's own
handlers never see `close`, because it does not bubble): it opens modal, moves focus in, keeps Tab inside, closes on
Escape, returns focus and reopens, with no script file. That needs `AllowUnsafeBlocks` in the project and the interop in
`src/AudioLab.Web/Interop/`. So D14 does not use the library for dialogs. Popover has the same route in principle: the
Popover API is Baseline since January 2025 (Chrome 114, Firefox 125, Safari 17) and gives light dismiss, but the
placement a popover needs next to its button is CSS anchor positioning. MDN lists `position-area` as Baseline 2026, newly
available since January 2026, while caniuse still marks Chrome 125, Firefox 147 and Safari 26 as partial and full only
from Safari 27 (both pages read on 2026-11-21). `anchor-name` and `position-area` were present, and a native popover placed with them opened flush below its button, in
Chromium 159, WebKit 27.2 and Firefox 159 (see the run below); other versions were not checked.
Menus, select and combo box are the other part: the browser supplies no roving focus, typeahead, active-descendant
handling, filtering or result announcements for them, so native elements would mean writing and testing that logic
ourselves; Bit.Bheadless supplies it. That is the whole case for the library, and it narrows D14 to those controls.

**A native modal and the library's overlays.** A modal `<dialog>` makes everything outside it inert and paints above any
`z-index`, so the library's overlays must live inside it. Proven on the proof page, with real key presses in Chromium
156: a select, a combo box and a popover opened from a native form dialog render inside the dialog (the library's
default; the portal option is off) and answer pointer hits. Escape with one of those lists open closes the list and the
dialog together unless the dialog's `cancel` event is stopped; the proof stops it when a list was expanded at the key
press, and then the first Escape closes the list and the second the dialog. A toast raised while a modal is open is
different: its host is a manual popover, lifted into the top layer again whenever a modal opens, so it is painted above
the dialog, but it is inert there, so its Undo and close buttons cannot be pressed until the dialog closes. A toast
raised from inside a dialog therefore must not rely on Undo. The `--z-dialog` token is not used by native dialogs; it
stays for the library's dialog and the fallback. The scripted run below repeated the select, combo box and popover
checks inside a native dialog, the order of the two Escapes and the toast hit test in Chromium 159, WebKit 27.2 and
Firefox 159, with the same results; the differences found are the WebKit focus return and the Shift+F10 check, both
listed below.

**Browser and axe-core run, 2026-11-21.** On the trimmed publish of the proof page, in Playwright-managed Chromium
159.0.8154.6, WebKit 27.2 (Playwright's WebKit build for Windows, not Safari) and Firefox 159.0, on OFFICE-DESKTOP
(Windows 11 10.0.26200, Intel Core i7-9700K, 8 cores, 40 GB), with Playwright 1.65.1 and axe-core 4.14.1 (WCAG 2.0 to
2.2 A and AA). It ran 33 scored keyboard, focus and placement checks, one observation that is not scored, and axe on
ten states (the page at rest, each overlay open, a native form dialog with a list expanded). Chromium passed 32 of the
33 scored checks, WebKit 30 and Firefox 31. The Shift+F10 check uses a synthetic key press and is informational; it is
included in the count. The script, the raw results and this table are on the proof branch, in
[spike-evidence/o10-browser-checks](https://github.com/HartsyAI/AudioLab/tree/spike/o10-primitives/spike-evidence/o10-browser-checks)
([results.md](https://github.com/HartsyAI/AudioLab/blob/spike/o10-primitives/spike-evidence/o10-browser-checks/results.md)); the folder is throwaway and not part of the solution.

| Check | Chromium | WebKit | Firefox |
|-------|----------|--------|---------|
| native dialog: opened with the keyboard, opens modal and moves focus inside | pass | pass | pass |
| native dialog: Tab and Shift+Tab never reach the page behind it (10 presses each way) | pass | pass | pass |
| native dialog: Escape closes it and focus returns to the opener | pass | pass | pass |
| native dialog: opened with a mouse click, Escape returns focus to the opener | pass | **FAIL** | pass |
| native dialog: confirm runs the action and closes | pass | pass | pass |
| library dialog: opens, role=dialog/alertdialog, focus inside | pass | pass | pass |
| library dialog: Tab stays inside (10 presses) | pass | pass | pass |
| library dialog: Escape closes it and focus returns to the opener | pass | pass | pass |
| menu: Enter opens, role=menu, focus on the first item | pass | pass | pass |
| menu: ArrowDown/ArrowUp move the active item, Home/End jump | pass | pass | pass |
| menu: typeahead moves to a matching item | pass | pass | pass |
| menu: Escape closes and focus returns to the button | pass | pass | pass |
| menu: Enter on an item runs it and closes | pass | pass | pass |
| context menu: right-click opens, ArrowDown + Enter runs an item | pass | pass | pass |
| context menu: Shift+F10 on the focused area opens it (synthetic key, informational) | pass | **FAIL** | **FAIL** |
| select: Enter opens a listbox, ArrowDown + Enter selects, value shown | pass | pass | pass |
| select: Escape closes the list and focus returns to the button | pass | pass | pass |
| combobox: typing filters, ArrowDown + Enter selects | pass | pass | pass |
| combobox: Escape closes the list | pass | pass | pass |
| popover: Enter opens, Escape closes, focus returns to the button | pass | pass | pass |
| popover: click outside closes it | pass | pass | pass |
| popover (library): the panel opens below its button, start-aligned | pass | pass | pass |
| popover (native, CSS anchor positioning): placed below its button, start-aligned | pass | pass | pass |
| tooltip (trigger is the button): keyboard focus shows it, Escape hides it | pass | pass | pass |
| tooltip (trigger is the button): hover shows it | pass | pass | pass |
| tooltip (span wrapped around a button): keyboard focus shows it | **FAIL** | **FAIL** | **FAIL** |
| tooltip (span wrapped around a button): hover shows it | pass | pass | pass |
| toast: appears, announced (role status/alert or live region), F8 reaches it, Undo runs | pass | pass | pass |
| modal: select inside a native dialog opens, picks, and Escape closes the list before the dialog | pass | pass | pass |
| modal: combobox inside a native dialog, Escape closes the list before the dialog | pass | pass | pass |
| modal: popover inside a native dialog, Escape closes it before the dialog | pass | pass | pass |
| modal: axe state - native form dialog with a select list expanded | pass | pass | pass |
| modal: a toast raised from inside a dialog (observation, not scored) | observation: shown, not clickable | observation: shown, not clickable | observation: shown, not clickable |
| tabs: ArrowRight moves to the next tab and shows its panel | pass | pass | pass |

| axe-core state | Chromium | WebKit | Firefox |
|----------------|----------|--------|---------|
| page at rest | 0 violations | 0 violations | 0 violations |
| native dialog open | 0 violations | 0 violations | 0 violations |
| library dialog open | 0 violations | 0 violations | 0 violations |
| menu open | 1 violation (color-contrast) | 1 violation (color-contrast) | 1 violation (color-contrast) |
| select open | 0 violations | 0 violations | 0 violations |
| combobox open | 0 violations | 0 violations | 0 violations |
| popover open | 0 violations | 0 violations | 0 violations |
| tooltip visible | 0 violations | 0 violations | 0 violations |
| toast visible | 0 violations, 1 unresolved | 0 violations, 1 unresolved | 0 violations, 1 unresolved |
| native form dialog with select expanded | 0 violations, 1 unresolved | 0 violations, 1 unresolved | 0 violations, 1 unresolved |

- **axe-core:** one violation in each browser, the same one: `color-contrast` on the open menu, for the destructive
  item's text in `--record` on `--surface-raised` (4.0 to 1 at 12 px; 4.5 to 1 is needed). 7.5 asks for that colour, so
  this is a finding on the design, not on the library; it is [O11](#11-open-questions) and no token changes here. The
  menu shortcut text uses `--text-dim`: `--text-faint` is 4.40 to 1 on `--surface-raised` (3.88 to 1 on
  `--surface-raised-active`), under the 4.5 floor, and 7.4 forbids it there. Axe left two items unresolved (the toast
  close button's name against its visible ×, and the contrast of two elements behind an expanded list in a dialog).
- **Tooltip:** it opens on hover, and on keyboard focus only when the trigger is the focusable control itself. A trigger
  that is a span around a button never sees the focus, in all three engines. The catalogue tooltip must make the control
  the trigger (a wrapper rule, below); the proof page has both forms.
- **WebKit, native dialog:** opened with the mouse, it returns focus to the page body when it closes; Chromium and
  Firefox return it to the opener, and WebKit does too when the dialog was opened with the keyboard. The cause was not
  tested; WebKit may not focus a button on click, leaving nothing to return to. The library's dialog returned focus to
  its opener in all three. The native dialog wrapper therefore records the opener and restores focus itself.
- **Context menu:** Shift+F10 on its area opened it in Chromium and not in WebKit or Firefox; synthetic key presses do
  not raise `contextmenu` in every engine, so nothing is known yet about the Menu key on a real keyboard.
- **Anchor positioning:** `CSS.supports` for `anchor-name` and `position-area` was true in all three. A native popover
  placed with `position-anchor` and `position-area: bottom span-right` opened flush below its button and start-aligned
  (0 px offset on both axes) in all three, and the library popover opened below and start-aligned (0 px) in all three.
  Checked: one placement (below, start-aligned), unscrolled, at 1280 by 900. Not checked: flipping at a viewport edge,
  scrolling, a popover inside a dialog, other versions, touch.
- **Native dialog and focus:** focus moved into the modal, and Tab or Shift+Tab (20 presses) never reached the page
  behind it in all three (focus passes through the document body, where the browser's own UI sits: 6, 5 and 4 times).
- **Overlays inside a native dialog:** a select, a combo box and a popover opened from it; Escape closed the list first
  and the dialog second; a toast raised from inside the modal was shown but did not receive pointer hits, in all three.
- **Firefox:** Firefox 159.0 did not start from Playwright's default browser folder on this machine (Windows
  side-by-side error, `mozglue` not found); a copy of that folder at `C:\pwtest`, with
  `PLAYWRIGHT_BROWSERS_PATH` pointing there, started and ran every check. The cause was not found.
- **Not run:** a screen reader, touch, Safari itself, and the libraries' own test suites.

**MudBlazor as the fallback is a candidate, not a proven one.** A dialog wrapper on it, on the proof branch, passes the
conventions tests, but those do not read its stylesheet or a C# theme (the script gate does read the package, and
MudBlazor is on the proof allowlist). Measured live, loading `MudBlazor.min.css` after `tokens.css` and `app.css` turned
the page's background white and its text grey, switched the font to Roboto, removed link underlines and reset heading
margins, because the stylesheet has global rules (`*`, `body`, `a`, headings); a theme provider adds its own variables.
The `body` rule won only because of the load order; the dialog finding does not depend on it. Its dialog renders in a
provider outside the wrapper, so the wrapper's scoped CSS did not reach it: it kept a white fill, a 4-pixel radius, its
own shadow and stacking of 1400 to 1402 rather than the tokens. Its focus trap, Escape and focus return worked. Using
MudBlazor would therefore mean shipping its global stylesheet or cherry-picking from it, overriding it with global CSS,
and putting colours in a `MudTheme` in C#, which the tests do not scan; that conflicts with "Same in every door" unless
shown otherwise.

**To move D14 to Decided:** a stable Bit.Bheadless release, and the proof page run again on it in Chromium, Firefox and
WebKit with axe-core on the menu, select, combo box, popover, tooltip and toast, and a screen-reader pass on the menu
and combo box. **To leave it:** switch the wrappers to BlazorBlueprint.Primitives or MudBlazor if the package is
unpublished or has no release for 90 days, or if two pre-releases in a row break a wrapper. The wrappers are the only
code that names the library. These conditions are unchanged by the run above: Chromium, WebKit and Firefox with axe-core are
done on 11.0.0-pre-23; the screen-reader pass and a stable release are not.

**Rules for the catalogue wrappers over the library:**

- A wrapper has one root element of its own and styles the library's parts with `::deep` from it. The library's parts
  do not take Blazor's scoped-CSS attribute, and a part that renders in a portal under `<body>` needs the root inside
  its content.
- A wrapper always passes a class to a part and never relies on the library's stacking: the `--z-*` tokens apply to the
  wrapper's class.
- A closed part carries the `hidden` attribute, and a wrapper's own `display` beats it, so a part that sets `display`
  also gets a `[hidden] { display: none }` rule.
- A library overlay opened from a dialog renders inside it, never in a portal. The toast host is a top-layer popover
  that is shown again after a modal opens.
- A tooltip's trigger is the focusable control itself, never a wrapper around it, or keyboard focus does not show it.
- A wrapper over a native dialog records the element that opened it and returns focus there on close, because WebKit
  returned focus to the page body after a native dialog opened with a mouse click.
- Scripts are approved per exact version ([section 8](#8-javascript-policy)): every version bump of the library needs
  new owner-approved entries. Bit.Bheadless 11.0.0-pre-23 ships three, `staticwebassets/Bit.Bheadless.4mg12f5a83.lib.module.js`
  (its file name carries a content hash), `staticwebassets/bit-bheadless-csp.js` and `staticwebassets/bit-bheadless.js`.
- Not built: a table. Bit.Bheadless has a grid with cell navigation, but nothing in this catalogue is a table yet.

### 7.6 States, input and accessibility

- Every interactive control has six states and styles all of them: rest, hover, pressed, keyboard focus, disabled,
  and engaged where it is a toggle. Hover moves a raised control to `--surface-raised-active`. Focus is the
  `--focus-ring` on `:focus-visible`, always visible, and distinct from selection.
- Disabled is half opacity with no pointer response, and says why in its tooltip.
- Every command has a keyboard path. Shortcuts live in one C# registry that the menus, the tooltips and the key
  handler all read; a shortcut is never written into a component.
- Colour is never the only signal: an engaged toggle changes fill and ink, not hue alone; M / S / R / I keep their letters,
  a clipped meter shows its peak as a number.
- Text meets 4.5:1 against the surface it sits on; a test checks the tokens. A control is identified by its label or
  glyph, which meets that floor, not by an outline.
- Icons are inline SVG from one set (Lucide, proposed), wrapped as Razor components. No icon font, no emoji.
  Transport glyphs are filled; every other icon is a 1.5 to 1.8 pixel stroke in the current text colour.
- Dragging anything shows where it will land before release, and Escape cancels the drag.

### 7.7 What enforces it

| Rule | Check |
|------|-------|
| The mock declares exactly the tokens in 7.4 | `DesignTokenTests.MockTokensMatchTheDesignDocument` |
| The mock's screen uses tokens only | `DesignTokenTests.MockUsesTokensNotLiterals` |
| `tokens.css` declares exactly the tokens in 7.4 | `DesignTokenTests.StyleSheetTokensMatchTheDesignDocument` |
| Text tokens meet 4.5:1 on their surfaces, and `--ink` on every state and track colour | `DesignTokenTests.TextTokensMeetTheContrastFloor` |
| No colour literal outside `tokens.css`, in a style sheet or a `style` attribute | `DesignTokenTests.StylesUseTokensNotLiterals` |
| Font size, font family, radius, shadow and stacking values are tokens | `DesignTokenTests.StylesUseTokensNotLiterals` |
| No viewport units | `DesignTokenTests.StylesUseTokensNotLiterals` |
| Everything else in 7.2 to 7.6 | Review, against this section and the mock |

## 8. JavaScript policy

C# is the default for everything. JavaScript is allowed only where the browser offers no other way in: the audio
thread (an `AudioWorklet` processor) and browser APIs that have no .NET binding (microphone capture, Web MIDI). If
[D14](#2-decisions) is Decided, it also allows the keyboard, focus and positioning behaviour of menus, select, combo box,
popover, tooltip and toasts that its UI library ships; until then it does not. Before adding any script, check whether C#
can do it, including through `[JSImport]` calls to browser built-ins that need no script file: the native dialog in D14
is one.

When a script is unavoidable:

- It is as small as it can be: a shim that moves data across the boundary. Logic stays in C#.
  The UI library of D14 is larger than that and carries logic (roving focus, typeahead, positioning, live regions). It is
  an exception to this bullet that the owner approves with D14, and it is not approved until D14 is.
- It is listed in [`ScriptAllowlist`](tests/AudioLab.Conventions.Tests/ScriptAllowlist.cs) with the reason C# could
  not do the job, and the owner approves that line in the pull request. That list is the full inventory of the
  scripts the tests can see; the end of this section says what they cannot.
- All interop (`IJSRuntime`, `[JSImport]`, `[JSExport]`) lives in `src/AudioLab.Web/Interop/`, behind a C# interface
  the rest of the UI calls.
- A third-party script library is a script and follows the same rule. So is a script that arrives inside a NuGet
  package, even one a Razor class library serves as a static web asset. Each script is listed in
  `ScriptAllowlist.Packages` by package id, exact version and its path inside the package, with the same reason and
  approval. An entry covers that one script at that one version: a version bump, or a new script in a listed
  package, fails until its own entries are added and the old ones removed.

The convention tests, which `dotnet test` runs and CI requires, enforce this. They fail on an unlisted `.js`, `.ts`
or similar file, an inline `<script>` block, or interop outside that folder. They also read the `libraries` section
of `obj/project.assets.json` for every project under `src` and `tests`, and fail on a restored package, transitive
ones included, that ships a script file that is not listed by id, version and path, on a project with no restore
output, and on restore output they cannot read, including a repeated key. An entry for a package version that is not
restored, or for a script that version does not ship, fails too, so the list never outlives its reason. Matching is
exact: the id, the version and the path are compared character for character, so a different case, a glob, a folder, or
an unnormalised path such as `a/../b.js` approves nothing. Trailing dots and spaces on a file name are ignored when
deciding whether it is a script, as Windows ignores them.

What they do not see: scripts added at publish (the .NET runtime pack's `dotnet.js` and its files), scripts embedded in
an assembly as a resource, scripts inside an archive within a package, and compressed scripts (`.js.gz`). Inline
`<script>` blocks are looked for in `.razor`, `.cshtml`, `.html` and `.htm` files of the repository only, not in SVG or
XHTML, and not in a package. Nothing in the build prompts a check of these. The tests also assume that
each project keeps its restore output in `obj/`, and a package reference conditioned on the operating system can make
the "not restored" check fail on another CI leg, loudly rather than silently. A project listed in `AudioLab.slnx`
that the scan under `src` and `tests` does not find (for example one inside a `bin`, `obj`, `node_modules` or
`artifacts` folder) fails a test, so it cannot be skipped silently.

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
| O1 | Audio for a browser on another machine. Preferred: the same C# engine compiled to WebAssembly in the page, feeding a minimal `AudioWorklet` shim, so there is one engine and no mixer per listener on the server. Fallback: the server mixes and streams to the page. | A spike. **Pass:** zero dropouts over 10 minutes of 16 tracks with effects, at 100 ms of output buffering or less, while the UI is being dragged and scrolled, inside an iframe without cross-origin isolation. Measured with the engine on the main thread and in a web worker. |
| O3 | The native audio device and MIDI input library. | A spike that checks license and upkeep. **Pass:** round-trip latency of 12 ms or less on the owner's audio interface, and correct device enumeration. |
| O4 | The desktop window: an embedded webview, or the default browser at first. | Owner, after O2. |
| O5 | Score notation. The extension uses the abcjs library for rendering, playback and timing. Options: a C# renderer, or that library on the allowlist. | Owner. |
| O6 | Where a hosted instance runs: on the GPU machine with the engine in-process, or on a web host with a remote engine. | Owner. |
| O7 | How a child AudioLab process shares VRAM with SwarmUI's own HartsyInference backend. | Design work before the extension cutover. |
| O8 | The event transport: SignalR or raw WebSockets. | The first server work item. |
| O9 | Whether the HartsyInference packages restore, pin to exact versions and run on net10, and what using `HartsyInference.Audio` (`Resampler`, `WavFile`, `Fft`) in WebAssembly would cost. Default stays that `AudioLab.Audio` references only Core (section 4). | A spike that restores and runs each API on the server and in a Blazor WebAssembly page, and lists package ids and exact versions. |
| O11 | Whether text in `--record` (destructive menu items in 7.5, and the danger button if its label is `--record`) may sit on raised surfaces. 7.6 says text meets 4.5:1, but `--record` is 4.01:1 on `--surface-raised`, 3.53:1 on `--surface-raised-active` and 4.29:1 on `--surface-selected` (axe-core measured 4.0 on the menu). Options: a lighter text-only red token, or a non-text cue (an icon or the position) with the label in `--text`. `TextTokensMeetTheContrastFloor` checks only the `--text` tokens and `--ink` on fills, and adding `--record` as text on the raised surfaces would fail today, so it is not extended until this is decided. | Owner. |

A spike ends by editing this file: the question moves to the decisions table with its evidence. The pass line is
fixed before the spike runs. A spike that misses it reports the miss, the numbers and the fallback; it does not move
the line. Numbers name the machine they were measured on.

## 12. Roadmap

Each phase ships on its own. SwarmUI users stay on the extension's DAW until the cutover.

| Phase | Outcome |
|-------|---------|
| 0 | This repository: design, agent rules, style gates, CI. **Done.** |
| 1 | Spikes O1, O2, O3 and O9 each closed in this file, and the O10 evaluation recorded as D14. D14 and D17 (O2) stay Proposed until the owner decides; Proposed is not closed. D17 also waits on a Firefox drag rerun on an idle machine. |
| 2 | Core and server: model, commands, history, store, hub, routes, and a minimal UI that opens a project. |
| 3 | MCP on the command registry (D11). |
| 4 | The audio engine: offline render and export, then real-time playback. |
| 5 | The UI, one domain at a time: tempo, markers, sections and groups; tracks and mixer; clips and media; effects, automation, MIDI, piano roll and beats. |
| 6 | Engine services and the model manager. |
| 7 | Recording, native devices and the desktop shell. |
| 8 | Voice agent and wake word. |
| 9 | SwarmUI extension: child process, proxy, tab, then cutover; projects saved by the extension are not carried over, by owner decision. |
| 10 | Hosting: accounts, quotas, the job queue. |

## 13. Change log

| Date | Change |
|------|--------|
| 2026-11-20 | First version. UI changed from Avalonia to Blazor WebAssembly (D3); JavaScript by exception (D4); D9 to D12 proposed. |
| 2026-11-20 | Interface design added and approved from the session mock: character and rules, layout, tokens, component catalogue, states and the tests that hold them (7.2 to 7.7); D13 decided; O10 opened. |
| 2026-11-20 | Pass lines added to the O1, O2 and O3 spikes (section 11). |
| 2026-11-20 | O9 opened (HartsyInference on net10 and in WebAssembly) and added to phase 1; commands may land ahead of their UI when MCP needs them (section 5, D11). |
| 2026-11-20 | O10 evaluated: D14 proposes native dialogs and Bit.Bheadless for the rest, behind the 7.5 wrappers; Popover and Combo box rows added to 7.5. D14 is Proposed. |
| 2026-11-20 | Project document version 4 added to `AudioLab.Core` with its id service, JSON contract and `AudioLab.Core.Tests` (sections 4 and 5); D15 and D16 proposed. |
| 2026-11-21 | Migration from the extension's `version: 3` project JSON dropped by owner decision: new project format only (sections 5 and 12). |
| 2026-11-21 | The script allowlist can list a script that ships inside a NuGet package by package id, exact version and script path, and a test fails on a restored package that ships an unlisted script (section 8). Requested by the owner with O10; the owner chose exact scripts over whole packages. |
| 2026-11-21 | D14 evidence: keyboard, focus and axe-core checks in Chromium, WebKit and Firefox (7.5); O11 opened for the contrast of `--record` text. D14 stays Proposed. |
| 2026-11-21 | O2 recorded as D17 (Proposed, Firefox dragging not shown to hold): DOM and inline SVG for the lanes, with Canvas2D through built-ins as the alternative and Skia not adopted; evidence under D17; 7.1 states how C# reads tokens. |
| 2026-11-22 | Command registry and gesture-coalesced history added to `AudioLab.Core` with tests (sections 4 and 5); D18 proposed. No domain commands yet. |
