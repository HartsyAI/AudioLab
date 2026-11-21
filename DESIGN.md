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
  effects, automation, beat, lyrics). A feature ships its commands, its undo, its UI and its tests together. Commands
  may land ahead of their UI when MCP (D11) needs them; the feature is complete only when its UI ships.
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
- **Dense surfaces** (timeline, waveforms, piano roll, automation) are drawn from C#: see
  [O2](#11-open-questions). They take their colours and sizes from the same tokens as the CSS; how C# reads them is
  settled with O2.

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
boxes, tables); the catalogue component wraps it, so the look and the API stay ours: see [O10](#11-open-questions).

| Component | Variants | Rules |
|-----------|----------|-------|
| Button | default, primary, danger, icon | `--surface-raised` with `--shadow-raised`, `--radius-md`, control size, medium weight. Primary is `--accent` with `--ink`: one per panel or dialog. Danger uses `--record` and is never the default action. |
| Toggle | labelled, track (M, S, R, I) | Strong weight in both states. Off: `--surface-raised` with `--text-dim`. On: filled with its state colour (`--accent`, or mute, caution, record, go for M, S, R, I) and `--ink`. Track toggles are `--control-xs` high letters with `--radius-sm`. |
| Segmented control | transport, tool, mode | A `--surface-well` group with `--shadow-well` and `--radius-lg`; items are `--radius-md`. The chosen item is `--surface-raised-active`; the rest are bare glyphs or labels. The transport uses raised items, and play fills with `--go` while playing. |
| Counter | | One well, cells split by a `--surface-panel` line. Each cell is a micro caption in `--text-faint` over its value. Time and bars are readout size; tempo, meter and key are body size in `--text-soft`. Click swaps or edits a cell. |
| Select and menu | dropdown, context | The closed select is a default button with its value and a chevron. A menu is `--surface-raised` with `--shadow-overlay`; every command shows its shortcut; destructive items sit last, in `--record`. |
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

C# is the default for everything. JavaScript is allowed only where the browser offers no other way in, which today
means the audio thread (an `AudioWorklet` processor) and browser APIs that have no .NET binding (microphone capture,
Web MIDI). Before adding any script, check whether C# can do it, including through `[JSImport]` calls to browser
built-ins that need no script file.

When a script is unavoidable:

- It is as small as it can be: a shim that moves data across the boundary. Logic stays in C#.
- It is listed in [`ScriptAllowlist`](tests/AudioLab.Conventions.Tests/ScriptAllowlist.cs) with the reason C# could
  not do the job, and the owner approves that line in the pull request. That list is the full inventory of the
  scripts the tests can see; the end of this section says what they cannot.
- All interop (`IJSRuntime`, `[JSImport]`, `[JSExport]`) lives in `src/AudioLab.Web/Interop/`, behind a C# interface
  the rest of the UI calls.
- A third-party script library is a script and follows the same rule. So is a script that arrives inside a NuGet
  package, even one a Razor class library serves as a static web asset: the package is listed by id in
  `ScriptAllowlist.Packages`, with the same reason and approval. An id covers every script the package ships, at any
  version: a bump that adds a script stays green. Nothing in the build catches it; the version bump in
  `Directory.Packages.props` is the only cue. Whether an entry should instead name exact package-relative script paths is
  **Open** with the owner; do not settle it in a pull request.

The convention tests, which `dotnet test` runs and CI requires, enforce this. They fail on an unlisted `.js`, `.ts`
or similar file, an inline `<script>` block, or interop outside that folder. They also read the `libraries` section
of `obj/project.assets.json` for every project under `src` and `tests`, and fail on a restored package, transitive
ones included, that ships a script file and is not listed, on a project with no restore output, and on restore output
they cannot read. A listed package that is no longer restored, or no longer ships a script, fails too, so the list
never outlives its reason.

What they do not see: scripts added at publish (the .NET runtime pack's `dotnet.js` and its files), scripts embedded in
an assembly as a resource, scripts inside an archive within a package, and compressed scripts (`.js.gz`). Inline
`<script>` blocks are looked for in `.razor`, `.cshtml`, `.html` and `.htm` files of the repository only, not in SVG or
XHTML, and not in a package. Nothing in the build prompts a check of these. The tests also assume that each project is
a `.csproj` in `AudioLab.slnx` (the root restore does not restore any other) and keeps its restore output in `obj/`,
and a package reference conditioned on the operating system can make the "not restored" check fail on another CI leg,
loudly rather than silently.

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
| O2 | How the timeline, waveforms and piano roll are drawn from C#: a Skia canvas in Blazor, or DOM and SVG. | A spike that rebuilds the mock's session screen in Blazor with 32 tracks and 500 clips with waveforms. **Pass:** 60 frames a second while scrolling and dragging, never below 45, in Chromium and Firefox, and it looks like the mock. |
| O3 | The native audio device and MIDI input library. | A spike that checks license and upkeep. **Pass:** round-trip latency of 12 ms or less on the owner's audio interface, and correct device enumeration. |
| O4 | The desktop window: an embedded webview, or the default browser at first. | Owner, after O2. |
| O5 | Score notation. The extension uses the abcjs library for rendering, playback and timing. Options: a C# renderer, or that library on the allowlist. | Owner. |
| O6 | Where a hosted instance runs: on the GPU machine with the engine in-process, or on a web host with a remote engine. | Owner. |
| O7 | How a child AudioLab process shares VRAM with SwarmUI's own HartsyInference backend. | Design work before the extension cutover. |
| O8 | The event transport: SignalR or raw WebSockets. | The first server work item. |
| O9 | Whether the HartsyInference packages restore, pin to exact versions and run on net10, and what using `HartsyInference.Audio` (`Resampler`, `WavFile`, `Fft`) in WebAssembly would cost. Default stays that `AudioLab.Audio` references only Core (section 4). | A spike that restores and runs each API on the server and in a Blazor WebAssembly page, and lists package ids and exact versions. |
| O10 | Where generic control behaviour comes from: dialogs with focus trapping, menus, popover positioning, combo boxes, toasts, tables. Preferred: a headless (unstyled) primitives library wrapped by the catalogue components in 7.5. Fallback: MudBlazor used only for those, behind the same wrappers. No library styles the session screen either way. | A short evaluation: licence, .NET 10 and WebAssembly support, the JavaScript it ships, accessibility, upkeep. Its pull request also teaches the script allowlist to list package scripts. |

A spike ends by editing this file: the question moves to the decisions table with its evidence. The pass line is
fixed before the spike runs. A spike that misses it reports the miss, the numbers and the fallback; it does not move
the line. Numbers name the machine they were measured on.

## 12. Roadmap

Each phase ships on its own. SwarmUI users stay on the extension's DAW until the cutover.

| Phase | Outcome |
|-------|---------|
| 0 | This repository: design, agent rules, style gates, CI. **Done.** |
| 1 | Spikes O1, O2, O3 and O9, and the O10 evaluation, each closed in this file. |
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
| 2026-11-20 | Interface design added and approved from the session mock: character and rules, layout, tokens, component catalogue, states and the tests that hold them (7.2 to 7.7); D13 decided; O10 opened. |
| 2026-11-20 | Pass lines added to the O1, O2 and O3 spikes (section 11). |
| 2026-11-20 | O9 opened (HartsyInference on net10 and in WebAssembly) and added to phase 1; commands may land ahead of their UI when MCP needs them (section 5, D11). |
| 2026-11-20 | The script allowlist can list a script that ships inside a NuGet package by package id, and a test fails on a restored package that ships an unlisted script (section 8). Requested by the owner with O10. |
