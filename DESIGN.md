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
| D18 | A command is a sealed immutable record that names its project and carries no id the server allocates; the registry maps a stable `snake_case` wire name to a pure handler `(document, command, IIdService) -> CommandResult`. A handler refuses by result (`CommandError`: code and message), never by exception, and every accepted document passes `ProjectValidator` and keeps its id, owner and version. History keeps whole documents (D16): undo and redo restore them, so redo keeps the ids; the gesture id is an argument of the history call, not a field of the command, and an open gesture belongs to the client that sent it (an opaque `ClientId` the hub supplies, one open gesture per client), so one client's commands never close another's gesture; the hub must call `ReleaseClient` when a connection ends and may pass only client ids it issued to connections, never one a client names; at most 64 gestures are open in a project and the next is refused with a `limit` error; a step is a whole-document snapshot, so when another client edits while a gesture is open, that edit is its own step with a snapshot that includes the partial gesture, and undoing the gesture's step reverts the other client's edit too (clients send a gesture's commands at its release, as section 5 says, so a partial gesture is rare); undo keeps 100 steps by default, where the old DAW kept 30. The command JSON context must be as strict as `ProjectJsonContext`, and the registry generates each command's JSON Schema from it, which allows extra members only where the model keeps unknown ones (D15). | Proposed | The command is the wire format, the undo unit and the MCP tool input (section 3), so its name, schema and errors are public. A refused edit is an ordinary outcome and an exception is a bug. Snapshots are cheap because unchanged parts are shared, and re-running a handler on redo would mint new ids. The old DAW's audit rows 33, 56, 62 and 64 are one rule here: a command that changes nothing adds no step and keeps redo. |
| D19 | The domain commands (tempo, marker and section first) are named `domain_verb` in lower-case with underscores, one command per user action, each editing one thing and listing the ids it allocated in `Created`; `CoreCommands.CreateRegistry()` is the one registry. The old DAW's rules live as pure code in `AudioLab.Core.Rules` (tempo map, meter, sections), called by the handlers and by the UI preview. Numbers are bounded by the model's validator (tempo 1 to 1000), not by the old UI's 20 to 400. Errors fall in three classes: `invalid` is a value out of range, found by a command (a time before the start, a tempo change before 0.01 s, a section shorter than 0.1 s, a number that is not finite) or by the validator; `rejected` is a value in range that a rule of the operation forbids (a marker or tempo change within 0.001 s of another, a section with no room, a blank name on a rename, no marker to make sections from); `not_found` is a missing target. An add with no name or a blank one takes a default ("Marker N", "Section N"). A move or drag clamps to its floor (a marker to 0, a tempo change to 0.01 s, a section to its free gap, because the UI previews it with the same rule), but a move that lands within 0.001 s of another marker or tempo change is refused, not clamped: in a drag that frame fails and the gesture goes on, which is acceptable because the UI previews with the same rule, sends no blocked frame, and the last accepted frame stays. A new section is cut short at the next section, as in the old DAW. Things placed in seconds (markers, sections, clips) stay put when the tempo, the time signature or a tempo change changes; only bars and beats move. `section_from_markers` replaces every section and allocates new ids each time it runs, and is refused when there is no marker. A command that changes nothing succeeds and adds no undo step. The generated schema describes every parameter and states its limits (range, allowed values, length, colour pattern); a move states its clamp in its description instead of a minimum. | Proposed | One verb per command gives MCP a tool per action and undo a label per step. Naming the error class lets a client tell a bad value from a conflict. Refusing in place of silently clamping tells an agent why its edit did nothing, while the drag rule keeps the preview and the result identical. Rule code in one place is what section 4 asks for, and a schema that says what a parameter may be is what an MCP client reads (D11). |
| D20 | The event transport is SignalR over WebSockets only, with the .NET client from the `Microsoft.AspNetCore.SignalR.Client` NuGet package (no script file, so section 8 is untouched). A client joins a project it owns, sends a command by its wire name, and every client of the project, an agent included, is sent each revision or a later one, with the whole document, in increasing order, and always the latest; the sender also gets a reply with the ids the command allocated or the reasons it was refused. Undo and redo are the same: one step for everyone. The user is the one the door resolved when the connection was made and the client id is the connection id the server issued, never a value the client names; a user cannot join another user's project. The hub is behind the same door checks as the routes, including `Origin` and `Sec-Fetch-Site`, which a browser sends on a WebSocket. Messages are strict source-generated JSON (`HubJsonContext`) and a document travels as `ProjectJson` text, so the client reads it as strictly as the server. The hub accepts a command of up to 2 Mi characters (a longer one gets a `limit` reply and the connection stays; the message limit is six bytes a character, the worst JSON escaping, so no command within it closes a connection), a connection joins at most 16 projects, a command that would make the project larger than the file limit is refused with `limit`, and a failed autosave is retried with delays of 15 s doubling to 5 min, until it succeeds or the next edit. | Proposed | SignalR gives groups, a typed C# client and reconnection that raw WebSockets would make us write; server-sent events are one way, and the hub is two way. WebSockets only, because identity is checked once at the upgrade and the polling transports would need it on every request. Whole documents because a snapshot is cheap in memory (D16) and a client then has no merge to get wrong; patches are the next step if a measured document size needs them. A change is announced under the project's order and put in each client's own outbox, which keeps only the newest unsent change of a project (a change holds the whole document, so an older one is of no use) and never drops a deletion; a writer per connection sends it, so a client that reads slowly or not at all delays no edit and no other client, and one that takes nothing for 10 s is disconnected. A join is made under the same order, so a joining client is told the revision it starts from and hears only later ones. A deletion is announced under that order too, so it never overtakes a change. |
| D21 | The group, track and mixer commands keep the old DAW's rules in `AudioLab.Core.Rules` (`GroupRules`, `MixRules`) and its starting values in `ProjectDefaults.NewTrack` and `InstrumentPresets`. A group's members sit together in the track list where its first member was and a group with no members does not exist; every command that could break that puts the list back in order. The validator does not require this, so a file may arrive with scattered members or an empty group, and only the commands maintain it. A track has one kind for its life, chosen when it is added (audio, instrument or bus); an instrument track starts with a synth preset or an empty sampler. Removing a bus removes the sends into it, and removing a track clears the score parent and stem links of other clips that pointed at its clips, as a track leaves a group that is gone. A track or group moves one step: inside a group a track swaps with its neighbour, outside it steps over the next track or whole group, and at the end of the list the command succeeds and changes nothing. Mute, solo and gain through groups and aux buses are `MixRules`, which the audio engine must call, so playback and export agree (D5): a soloed track silences the others except a bus it feeds through an unmuted send, a send into a soloed bus still speaks, and a pre-fader send skips the fader but never a mute. Fader, pan, send level and group volume are separate commands so a drag is one gesture. The send commands are named `mixer_send_*`, in the mixer domain. An empty track list is out of range (`invalid`), and the schema says `minItems: 1`. A group's folded state is part of the document and an undo step, where the old DAW kept it out of undo. | Proposed | Keeping the old rules in one place lets the engine, the UI preview and the commands agree. A fixed kind avoids a command that would have to drop clips or an instrument. A group folded away is the one view value the model keeps, and making it undoable is simpler than a second kind of command; the UI can revisit it. |
| D22 | The clip commands keep the old DAW's clip rules in `AudioLab.Core.Rules.ClipRules`. Audio and MIDI clips share one id space, so `clip_move`, `clip_delete`, `clip_duplicate`, `clip_rename`, `clip_set_color`, `clip_set_muted` and `clip_reorder` take either kind; `clip_add`, `clip_trim`, `clip_split`, `clip_set_gain` and `clip_set_fades` are audio only, and `midi_clip_add` and `midi_clip_set_length` MIDI only. A clip's media is a `MediaId`, and `clip_add` carries the duration of the whole file. Until the media store exists, `clip_add` accepts any well-formed `MediaId` and any duration the client sends: Core holds no media records and cannot check either. The server MUST verify that the media exists, belongs to the session's user (D12) and has that duration before it applies `clip_add`, and before the media routes ship; a wrong duration defeats every check that is bound to the source file (the trim limits and the fades). This is an item of the phase 6 media work in section 12. A split keeps the original clip as the left piece, with its id and all its metadata, and gives the right piece a new id and only the lyrics (the old DAW made two new clips, which would orphan the score parent and stem links that point at the clip). A copy carries only the lyrics too, and keeps the mute and the colour of the original (audit row 35). A move, a trim and a fade are drag commands whose values the schema bounds, and a value outside it is refused (`invalid`), unlike a marker or a section (D19), because the schema says so and the UI preview never sends one: a time or a fade is from 0 to the project limit, so a clip is not moved before the start. Inside those bounds the rule limits the result as a drag does: a trim stays inside the file, off the start of the project and at least 0.05 s long, the fade in keeps to the clip and the fade out to what it leaves (so the fades never overlap, and the result does not depend on the old fades), and a trim shortens fades that no longer fit. A split point too close to an end, and a trim or split of a MIDI clip, are refused (`rejected`). A clip moved to another track lands over the clips there, and only onto a track of its kind. A delete in shuffle edit mode slides the later clips of the same kind on its track left by its length (audit row 36); the link rule of D21 applies to a deleted clip. A MIDI clip length below 0.05 s is refused (`invalid`), and one shorter than a note it holds is refused (`rejected`). A rename to a blank name is refused (`rejected`, as for other names), and a copy of a long name is cut so it fits the name limit. A command that changes nothing adds no undo step (rows 56, 62 and 64). Not here: replacing a clip's media (conform, stems), range operations (delete or split a time range, crossfade, paste) and the notes of a MIDI clip. | Proposed | Duration has to come from somewhere, and the media upload is where it is measured, so the route checks it and the document stays free of a store. Keeping the left piece's id keeps score and stem links valid. Drag commands clamp so the UI preview and the result agree; adds and sets refuse, so an agent learns why an edit did nothing. |
| D23 | The MIDI and effect commands keep the old DAW's piano-roll and effect-chain rules in `AudioLab.Core.Rules` (`NoteRules`, `GridRules`, `EffectRules`, `EffectPresets`). A note is addressed by its `NoteId` inside its clip; a command that edits notes names them in `notes`, and leaves the list out to mean every note of the clip, as the piano roll acts on the selection or all. Ids count up in the clip and are never reused. A move and a resize carry each note's absolute target (a list of note, start and pitch, or note and length), not a delta, as a clip drag does (D22), so a drag that was clamped at an edge and comes back lands exactly on its target and a multi-note drag keeps the shape its client sends; the old DAW moved by a delta. Transposing by semitones is its own command, as in the old toolbar. Values outside the schema (a start below 0, a pitch outside 0 to 127) are refused; inside it the rule keeps a note's start from 0 to the latest start, which is 0.02 s before the clip ends and is the same limit for add, move and quantize, and a length at least 0.02 s. A note must start inside its clip: the model forbids the old DAW's notes past the clip end, so a move stops short of it. A list of notes is 1 to 1,000,000 long, an empty list is refused, and a note id the clip does not hold (a hand-built 0 included) is `not_found`. Quantize snaps to the project grid (`ProjectView.Grid`: a note value, a triplet, a bar or a time in seconds) at the tempo where the clip starts, not at the playhead (audit row 38); a grid of "auto" or an unknown value, which depends on the zoom the server does not know, means a sixteenth note. A tie rounds up, as the old DAW's `Math.round` did. A controller event is identified by its time and controller number, and is kept in time order. A clip of 130,000 notes edits as any other (audit row 47). An effect is added at the end of a track's chain (a bus too) from its kind's starting settings or a preset, and the chain runs in list order, before the fader. Its numeric settings are set by name, and each setting's range is the validator's, which a test holds equal. A preset is data, a name and values laid over the kind's defaults, and applying one keeps the effect's id, place and bypass; a preset name that is not one of the kind's is `rejected` whether it is added or applied. A delay division is one of six, which the validator also checks. A user's saved chains are library data of the store, not the project, and are not commands. The schema lists every setting name and preset name and states each setting's range in the description of the value, and the tests hold those equal to the rules'. Not here: range operations that cut notes (delete or split a time range), note import and export, and recording. | Proposed | The piano roll's edits are all selections of notes with a delta, so one command shape serves the UI, an agent and a gesture. Keeping the rule tables as data lets the engine, the UI and the schema read one list. |
| D24 | In a browser on another machine, the C# engine, compiled to WebAssembly, runs in a web worker in the page and feeds a minimal `AudioWorklet` shim by message passing; the page's main thread does not host it. A server-side mix streamed to the page is the fallback for a client that cannot hold this. See [the O1 evidence](#d24-evidence-the-o1-spike). | Proposed | In the worker, 16 tracks with effects played 10 minutes with zero dropouts in Chrome and Firefox at 93 ms of total output buffering, while the page was dragged and scrolled, inside an iframe with no cross-origin isolation, on a machine at 24 to 34% CPU. On the main thread Firefox dropped out and Chrome's margin was 5 ms, so the worker is the host. Firefox's margin is thin (one 2.7 ms dropout with 11 ms less ring), and under 60 to 70% machine CPU the worker dropped out too, so a busy or slow client would need the fallback, which was not built or measured. The reruns on a quiet machine (Chrome main thread, Firefox worker, Firefox main thread, with the Chrome worker) are all finished and in the table; none is pending. Quiet here means other work held the CPU under 35% on average, not an idle machine. One script file with two roles needs the owner's approval. |
| D25 | The automation and drum pattern commands keep the old DAW's rules in `AudioLab.Core.Rules` (`AutomationRules`, `BeatRules`). A lane (volume or pan) is a list of points by time and a point has no id, so a command names it by its index in the lane before the command runs; the lane stays in time order, a point added goes behind the points at its time, and a point moved keeps the place nearest to where it was among the points at its new time, so a point moved inside a step does not swap the step's values. The handlers answer with the document only, since the result carries ids and a point has none: the rule that says where a point landed (`LaneEdit.Index`) is in Core, and a client that drags a point computes the index with the same rule it previews with. A move and a segment drag (`automation_points_set_values`, which sets the two end points `Segment` names) carry absolute targets, as D22 and D23 do, so a drag that went past a neighbour and came back restores exactly, unless the point crossed other points at its own time, which can swap a step's two values. The value range depends on the lane and is the validator's: volume 0 to 16 (the fader's maximum; the old lane drew 0 to 2, which a view may still do with `VolumeDisplayMax`) and pan -1 to 1, so a point a file holds above 2 can be dragged and brought back. The schema bounds the value by the union of both and states each in the description; an add or a recorded pass with a value outside its lane is `invalid`, a dragged value inside the union is kept inside its lane, and an index that names no point of the lane is `not_found`. Points have no ids, so an index can go stale between clients: a client that did not make the last edit must read the lane again before it names a point. `automation_write_commit` folds a recorded pass into a lane by the old rule: the old curve leads in, the written points replace what was under them, Touch hands back to the old curve just after, of two points of the pass less than 0.1 ms apart the later stays (it is what was played) while two at one time are a step and both stay, and a held value of the pass keeps only the ends of its run; the old DAW tidied the whole lane after a pass, and this tidies only the span of the pass, so a step or a held run elsewhere is not lost. A pass that wrote what was already there changes nothing and adds no undo step. The live writer (grab, touch, latch and the tick while playing, audit rows 29, 43, 44 and 66) is the client's; a Write pass that ends is its commits and `automation_set_mode` to touch in one gesture. Rows 24 and 69 are about rendering the lane under a drag. The lane view (open, and which lane) is a command because the document holds it, as a group's collapsed state is. The drum pattern is edited by lane (`beat_lane_add`, `_remove`, `_rename`, `_set_gain`, `_set_media`, `_set_steps`) and by pattern (`beat_pattern_set_steps`, `beat_pattern_set_swing`). Steps are set on or off by index, not toggled, so a paint across the grid is a gesture and a repeat changes nothing; an index past the pattern's step count is `invalid`, as a note start past its clip is. The step count is 1 to 256 (the old DAW offered 16 and 32): shrinking drops trailing steps and growing adds off steps, so every lane keeps the pattern's length. Swing is 0 to 1 (the old slider stopped at 0.6) and a lane's gain 0 to 16 (the old slider at 1.5). The lane limit is checked before the id is minted. `BeatRules` holds the step timing: a sixteenth note at the tempo where the pattern lands, and odd steps late by the swing times a step. A lane's media is a `MediaId` under the same statement as D22: Core accepts any well-formed one, and the server MUST verify its existence and ownership before it applies `beat_lane_add` or `beat_lane_set_media`. `ProjectContent.ReferencedMedia` counts the clips', the pads' and a sampler's media, which is what audit rows 4 and 11 want a media store to keep; the store must apply it to every document of the history too, as an undo step can play a file the project no longer does (row 11). Not here: send lanes and curve shapes (the model has volume and pan lanes of linear points), reordering lanes, rendering a pattern to a track (engine work) and generating a pad from a prompt. | Proposed | The old lane editor and drum machine edit by index and by slider, so a command per gesture shape serves the UI, an agent and an undo step alike. Absolute targets and set-not-toggle keep every frame of a drag safe to replay. |
| D26 | The lyrics, score and stem commands keep the old DAW's lyrics rules in `AudioLab.Core.Rules.LyricsRules` and edit a clip's `meta`, which only an audio clip has: a MIDI clip is `rejected`. Word times are in the seconds of the clip's source audio, so moving, trimming and splitting a clip never rewrite them. `lyrics_set_text` keeps the word times when the words are the same as far as spacing, case, punctuation and tags go, compared with the old text or with the timed words (so heard words that are not in the text do not lose their times when the text is saved again), and drops them otherwise; a text with no words removes the lyrics. Dropping the times also drops what described them: the model and the unknown fields go, and the source becomes manual (generated lyrics stay generated and keep their model); `lyrics_clear_timing` does the same, and on lyrics with no times changes nothing. A model's answer comes in as `lyrics_apply_alignment` (known text) or `lyrics_apply_transcript`; the server runs the engine and the command only records what was heard, with an `offset` for where the audio that was sent began in the source (the clip's own offset when absent). Words an aligner most likely invented are flagged unsure, which the strip leaves out, and a time set by hand clears the flag. `lyrics_set_word_time` takes an absolute start, end or both, kept between the end of the word before and the start of the word after (words do not overlap) and with the end no earlier than the start; with both, the start is placed first and the end after it, so the result does not depend on where the word was and a repeat changes nothing. A drag is a gesture, an index outside 0 to 999999 is `invalid` and one that names no word is `not_found`. `lyrics_estimate_timing`, `lyrics_clear_timing`, `lyrics_shift` (never before 0, and by 0 a no-op) and `lyrics_import_lrc` (timeline seconds to source seconds, by the clip's start and offset) follow the old Lyrics tab. A change to a clip's lyrics follows each of its vocal stems, the clips whose stem link names it and whose stem is called vocal or vocals in any case: the stem gets the lyrics rebased to start at 0 where it was cut, or loses them with the clip's. `stem_set` records that offset in the stem link (`stemOf.sourceOffset`, an optional member of the model: the source's current offset unless the command gives one, from 0 to the source's length), and later edits rebase by it, so trimming the source after the stem was cut does not move the stem's lyrics; a link that records none uses the source's current offset. The old DAW also found a stem by a track called "Vocals: name"; that guess is dropped, the link is the only record. A split and a duplicate keep the lyrics and nothing else of the metadata (D22). `score_set` replaces a clip's score record with the fields given (a field left out is removed, unknown fields are kept), `score_set_label` and `score_clear` edit it, and a score's parent must be another audio clip and may not make a loop, which is `rejected`. `stem_set` records the clip a stem came from and its name (not blank, up to 128 characters), and a vocal stem takes the source's lyrics at that moment, as the old Stems tab did; a stem that would make a loop of stems is `rejected`; `stem_clear` removes the link. Clips with nothing left in their metadata have none. Edits that keep the times (set text with the same words, shift, one word's time) keep the unknown fields of the record, while those that drop the times (a text with other words, clear timing) or replace them (a new answer of a model, an estimate, an LRC import) start fresh, because those fields described the old timing. A line of an LRC file with several time stamps counts once for each, and a file whose lines would hold more text than a clip may is `invalid`; the words of an answer are matched to the text with a bounded search (`LyricsRules.SearchBudget`, in character comparisons: each look is charged its word's length times the characters it may compare, and a word that was missed once is not looked for again), after which the rest keep the line they are on; a transcript whose words would make a text longer than a clip may hold is `invalid` before it is built. Text and labels are bounded by the validator's limits, and the schema states them. Not here: laying the lyrics out for the strip, LRC and SRT export, and generating or aligning (engine work); the score tab's own ABC editing. | Proposed | The Lyrics tab's edits are the commands; keeping the rules as pure code lets the engine, the strip and an agent share them. Recording what a model heard, not calling it, keeps Core free of I/O, and mirroring to the vocal stem in the command keeps the two documents of one voice from drifting apart. |
| D27 | The project settings, track settings and instrument commands keep the old DAW's rules in `AudioLab.Core.Rules` (`TrackRules`, `InstrumentRules`, `GridRules`). Project: `project_rename` (trimmed; a blank name is `rejected`), `project_set_metronome` and `project_set_view`, which sets the ruler, the edit mode and the grid, at least one of them; the grid is one of `GridRules.Values`, which includes `auto`, the grid that follows the zoom of a view and that a server reads as a sixteenth. Track: `track_set_armed` and `track_set_monitor`, where an aux bus has no input so turning either on is `rejected` and turning it off is fine, and which are live state: whoever is at the controls arms and monitors, so they are no undo step, undo and redo never take them back (a track that undo brings back is not armed), and a project opens with nothing armed or monitored (`LiveState.Reset`, which the server MUST apply when it opens a project, and which a project file need not respect). A command is marked live with `undoable: false` when it is registered, which the descriptor shows and `CommandRegistry.IsUndoable` answers, and history keeps the live fields of the present when it restores a step (`LiveState.Carry`), also when it decides that a gesture ended where it began; `track_set_input`, which is for audio tracks only (an instrument track plays MIDI and a bus has no input, so both are `rejected`) and takes the device and the channels as one absolute value: a device or channel outside the project's limits is `invalid` whatever the mode, a single channel needs its number, 1 to 256, which is `invalid` when missing, and the other modes must have none, which is `rejected`; `track_set_height`, 16 to 2000 as the validator has it, where the track menu offers 30, 44, 64, 100 and 160; and `track_set_icon`, which takes `TrackIcon`'s rule (short clean text or a small png, jpeg or webp image, not empty) and removes the icon when it is left out. What the transport does while it records (the old DAW refused to change arming or an armed track's input then), and the take count, belong to the engine and the recorder, not to the document. Instrument, on an instrument track only: `instrument_set_preset` puts a synth preset's whole sound back, tweaks included, or gives a synth track an empty sampler, while the Sampler preset on a sampler changes nothing, as the old menu item only opened the sampler's panel; `instrument_set_parameter` sets a numeric setting by name with the validator's range, which a test holds equal, and a setting the kind does not have is `rejected` (the second oscillator's ratio has no control in the old DAW and so no command); `instrument_set_wave`; and, on a sampler only, `instrument_set_root_note`, `instrument_set_one_shot` and `instrument_set_sample`, whose name needs a sample (a name with none is `rejected`) and is held to the validator's 200 characters, as the old DAW's cut to 40 was only how its panel showed a name. The old DAW turned a synth into a sampler when a sample was set; here that is explicit, the Sampler preset first, and a synth is `rejected`. A sample is a `MediaId` under D22's statement: Core accepts any well-formed one and the server MUST verify its existence and ownership before it applies `instrument_set_sample`. The other commands here are undo steps, as every command is, although the old DAW kept the input, the metronome and the view settings out of undo. Not here: recording, the take count, MIDI input and the computer keyboard, which are the engine's and the UI's, and the second oscillator's ratio and a sampler's name, which the old DAW never let a user change. | Proposed | These are the settings the track menu, the instrument card and the project bar change, one command each, with the limits the project already enforces, so the UI, an agent and a gesture share them. |
| D35 | The engine renders in two steps. `RenderPlan.Create` compiles a document, off the audio path, into arrays: mute, solo, group and send gains from `MixRules` (D21), the tempo map, the heard stretches of each audio clip from `LayerRules` in Core, and media supplied by `IMediaSource` (decoded float PCM by `MediaId`), cleaned (a sample that is not a number becomes silence), cut to two channels (a file's channels beyond the second are dropped) and brought to the project's rate by a windowed-sinc filter of 32 zero crossings a side with its cutoff at 0.97 of half the lower rate (measured on OFFICE-DESKTOP, a sine through 44.1 to 48 kHz and 96 to 48 kHz: -0.1 dB at 0.9 of that Nyquist frequency, -2.4 dB at 0.95, -6 dB at the cutoff, -30 dB at 1.02 and -68 dB at 1.05; a constant level exact at every ratio tried), kept in a `MediaCache` that a caller may share between plans, keyed by media id (an id always names the same audio, so a changed file takes a new id) and bounded at 512 MiB by default, least recently used out first and the newest always kept, so editing a project does not resample every file again; a clip whose media is missing, or a file a source asks the plan's media for and does not get, fails the plan with `MissingMediaException`, and media shorter than the clip claims (D22) is cut off hard and silent past its end, with no fade, since the claim is the store's to check. The plan requires a document that passed `ProjectValidator`; the one fault it catches itself, because a dictionary of clips would throw an unclear error, is two clips of a track with one id. `MixerGraph.Render(left, right)` then pulls stereo float blocks with no allocation, lock or exception, from a position held in whole frames, and every time-dependent value (fades, seams, automation, clip edges) is worked out from the absolute frame, so the output is the same for any block size; the offline renderer is a loop over it and live playback is the same call from a callback (D5). The signal path is clips and the instrument source, inserts, pre-fader sends, fader (track gain times its group's, times the volume lane), pan, post-fader sends, then the solo gate and the master sum; a track silent under solo still runs when a send of it is heard, so soloing a bus hears what feeds it. A bus has its own inserts, fader, pan and solo gate, and sends only reach buses. A clip layer is heard only where no later unmuted clip covers it, with a 4 ms ramp at each cut and both clips heard over a crossfade (the old rule, now `LayerRules.AudibleSpans` in Core, since the lyrics strip reads it too); clip fades are linear in gain. Automation lanes play in every mode but off: a volume lane multiplies the fader and a pan lane offsets the track pan, clamped to -1 to 1. Lanes are read on a grid of 32 frames fixed to the timeline: the volume runs straight between grid points and the pan holds one value per cell, which cut a render with lanes on every track of a 16-track project from 16 to 90 times real time (OFFICE-DESKTOP, JIT, one run each, the machine busy) and does not depend on the block. Pan is the law of the Web Audio stereo panner the old DAW used: a track whose sound is the same on both channels at the fader (all its clips mono and no reverb on it, or an instrument source that says it is mono) is shared out by a constant-power law, 3 dB down at the centre and unity at either side, and any other track keeps unity at the centre and has the side panned away folded across. The master volume then the limiter run last; the limiter is a peak limiter with no delay and no gain below its ceiling of -1 dBFS, instant attack and a 0.15 s release, so a mix under the ceiling equals the sum of its stems sample for sample (audit row 23: the old limiter delayed 6 ms and added 0.5 dB). A render's length is the end of the last audible clip (`ProjectContent.End` with `audibleOnly`: a muted clip, a muted track and a track silenced by solo that sends nothing heard do not count) plus a tail, the longest chain of insert tails of a track and the buses it feeds (audit rows 45, 58 and 71). Instrument tracks play through an `ITrackSourceFactory` that the synth and sampler work supplies, which is also given the plan's media so that a missing sample fails the plan, and are silent without one; the drum pattern is not rendered yet and will get a document-level source in its own change; the metronome is never rendered. The effects are D39. A render over a range starts every effect cold, as a jump of the playhead does, so it differs from the same stretch of a full render wherever a tail would have reached into it. Output is deterministic on one platform; the sine, cosine, exponential and power functions it uses may differ in the last bits between platforms, so D5's same output means equal within a small tolerance (the golden renders of D39 use 2e-4) across x64 and WebAssembly, not bit for bit. The offline renderer returns the whole render in memory, which is for tests and short renders; export must stream (D40). Known limits: an insert's state lives in the plan, so swapping a plan during live playback cuts the tails, and `LayerRules.AudibleSpans` compares each clip with the later ones of its track, which is quadratic in the clips of one track (the project allows 100,000). | Proposed | One graph for live and offline is D5, and the plan is where the allocating rules live, so the loop that must meet the real-time rules (section 6) has nothing left to allocate. Absolute-frame time is what makes a block of any size, a seek and an export agree, which the test of block sizes holds. The ceiling, release and pan law keep what the old DAW sounded like while removing the latency and the level change it had by accident. |
| D39 | The effect DSP in `AudioLab.Audio` follows the settings of D23 and the graph of the old DAW, with these differences. The equaliser is five biquad sections in series (high-pass, low shelf, peak, high shelf, low-pass) with the Web Audio coefficient formulas, so a pass filter's Q is in decibels as the old DAW set it (0.707 dB); a band at 0 dB is left out, and a frequency is held under 0.49 of the sample rate. The compressor is a feed-forward design with a soft knee, a detector on the louder channel and attack and release smoothing the gain reduction in decibels; it has no look-ahead and no automatic make-up gain, where the Web Audio compressor node the old DAW used looked ahead 6 ms and added one, so a compressor's level and delay differ from the old DAW's. The reverb is a convolution with the old generated impulse response: the same seed from the size, the decay and the channel, the same generator (mulberry32) and power-law decay, so a room is the same room, each channel with its own impulse, scaled as the Web Audio convolver scales with normalisation on, after a predelay, and crossfaded with the dry signal (dry 1 minus the mix); a reverb makes its track stereo for panning (D35). The convolution has no delay: a direct sum over the first 128 taps, then frequency-domain blocks of 128 taps up to tap 4096 and of 4096 taps beyond, on real FFTs, working from its own sample count so the block size does not matter. The delay feeds a low-pass tone filter whose output is both the wet signal and the feedback, as the old graph did, but the tone filter is a Butterworth low-pass (no peak) where the old graph set a resonance of 0.707 dB, which peaks 1.7 dB and let a feedback above about 0.82 grow without limit; a change of time glides over about 15 ms; a synced delay takes its time from the tempo step in force at each frame and so retunes at the frame of each tempo change; the longest delay is 4 s. Saturation is the tanh curve of the old waveshaper on an input held to -1 to 1, then the trim, crossfaded with the dry signal, and it is anti-aliased by the antiderivative of the curve where the old one oversampled four times, which costs no delay. An effect with `enabled` false is not built. A render's tail is the effects' own: a reverb its predelay plus its length, a delay its time until its echoes are 60 dB down (at most 30 s), and the old DAW's fixed 0.1 s and 9 s floors are gone. Unit tests check each effect on known signals, and golden renders of each effect and of a chain are stored in `tests/AudioLab.Audio.Tests/Golden` and compared within 2e-4; a change that moves the sound regenerates them on purpose with `AUDIOLAB_UPDATE_GOLDEN=1`. | Proposed | Keeping the old settings, impulse and filter formulas keeps projects sounding as they did where a rule can be kept; the compressor, the saturation anti-aliasing and the zero-delay convolution are the three places where the old DAW's own nodes had latency or a level change that would break D5's sample-for-sample agreement between a mix and its stems. The convolution is split in stages because one long stage costs several times more for the same sound. |

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

#### D24 evidence: the O1 spike

Code: [branch `spike/o1-engine-in-page`](https://github.com/HartsyAI/AudioLab/tree/spike/o1-engine-in-page) (the engine in `src/AudioLab.Audio`, the worker host in `src/AudioLab.Audio.Worker`, the page in `src/AudioLab.Web`, a test server in `src/AudioLab.SpikeServer`). Machine: OFFICE-DESKTOP, Intel Core i7-9700K (8 logical cores), 40 GB, NVIDIA GeForce RTX 3060, Windows 11 Pro 26200. Headed Chrome 157.0.8103.79 (the installed build, driven by Playwright 1.65 with a throwaway profile and three background-throttling flags removed) and Firefox 159.0 (Playwright's patched build, the only Firefox it drives; stock Firefox was not run). .NET 10.0.14; the cited runs use the ahead-of-time (AOT) build unless a row says interpreted. The output ran at 48 kHz stereo to the machine's default device at a low master gain; which device that was was not checked. Everything below was measured live on that machine; the compressed-audio figures are arithmetic and say so.

What ran. A session of 16 tracks: 8 sines with an overtone and 8 looping 30-second stereo 44.1 kHz clips resampled by linear interpolation; each track has a low-pass swept by a slow sine and a peaking filter on each channel, a pan and a reverb send; one Schroeder reverb and a soft clip on the master. The engine renders 512-frame chunks (10.7 ms). The worklet keeps a ring of those chunks, capped at the budget, and asks for one chunk for each it plays, so no more than the ring is ever queued. In the worker case the worker and the worklet share a transferred `MessagePort`, so chunks never pass through the page's main thread; there is no `SharedArrayBuffer`. The page ran inside a same-origin iframe served with no COOP or COEP header: `crossOriginIsolated` was false and `typeof SharedArrayBuffer` was `undefined` in every run, started by a real click inside the frame, the window visible and focused. The UI load is the D17 DOM timeline (32 tracks, 500 clips) scrolled across, down and diagonally, with a clip dragged and another picked up and dropped, in a loop of 6-second scenarios (100 in a 10-minute run), in the page that hosts the engine.

How dropouts were counted. The shim counts them in the page, on the audio thread: after the ring first filled, any 128-frame quantum that found less than a quantum of audio in the ring is a dropout (an event is a run of consecutive ones). A run is 10 minutes of frames the audio thread played (28.8 million at 48 kHz), not timer time; the audio clock and the wall clock agreed to within a second in every final run. The output buffering is the ring plus the context's base and output latency as the browser reports them (Chrome 10 + 40 ms, Firefox 0 + 40 ms), kept at or under 100 ms. Chrome's own `playbackStats` puts its output latency at 46.6 ms on average and 51.3 ms at most, so on that reading the 100 ms Chrome runs total 99 to 104 ms; the Chrome run at a 90 ms budget below totals 93 ms even then. A deliberate 300 ms stall of the producer registered as one event in the worker and in Chrome's main thread (10,368 to 11,136 frames of silence) and as five in Firefox's main thread, so the counter is live; those controls ran on the first build, with an 85 ms (Chrome) and 96 ms (Firefox) ring, before the budget was tightened. Chrome's `playbackStats` is a second, independent count: it showed 2 events (20 ms) by the first report at 7 s in each Chrome run, at start-up, and none afterwards; Firefox has no such count.

| Run (10 minutes unless noted) | Ring + browser latency | Machine CPU, mean (peak) | Dropout events | Ring's lowest point | The scripted UI load: worst second, longest frame |
|-------------------------------|------------------------|--------------------------|----------------|---------------------|---------------------------------------------------|
| Chrome, engine in a worker, AOT | 42.7 + 10 + 40 = 92.7 ms | 24% (30%) | 0 | 26.7 ms | 97 fps, 40 ms |
| Firefox, engine in a worker, AOT | 53.3 + 0 + 39.9 = 93.2 ms | 30% (44%) | 0 | 8.0 ms | 81 fps, 83 ms |
| Chrome, engine on the main thread, AOT | 92.7 ms | 27% (50%) | 0 | 5.3 ms | 97 fps, 30 ms |
| Chrome, main thread, AOT, 5 minutes | 92.7 ms | 25% (32%) | 0 | 18.7 ms | 97 fps, 40 ms |
| Firefox, engine on the main thread, AOT | 93.0 ms | 30% (36%) | 2 (32 ms), both in the first 7 s | 0 | 85 fps, 70 ms |
| Firefox, main thread, AOT, 5 minutes | 93.2 ms | 30% (36%) | 2 (133 ms), at 57 s and 157 s | 0 | 79 fps, 171 ms |
| Chrome, worker, AOT, 90 ms budget | 32.0 + 10 + 40 = 82.0 ms | 24% (32%) | 0 | 16.0 ms | 97 fps, 40 ms |
| Firefox, worker, AOT, 90 ms budget | 42.7 + 0 + 39.8 = 82.5 ms | 30% (41%) | 1 (2.7 ms), at 170 s | 0 | 89 fps, 40 ms |
| Chrome, worker, interpreted | 42.7 + 10 + 40 = 92.7 ms | 26% (35%) | 0 | 18.7 ms | 95 fps, 60 ms |
| Firefox, worker, interpreted | 53.3 + 0 + 37.9 = 91.2 ms | 34% (47%) | 0 | 10.7 ms | 86 fps, 70 ms |

Reading it. The pass line (zero dropouts for 10 minutes, at 100 ms or less) is met with the engine in a worker in both browsers, in both builds, and with it on the main thread in Chrome only: Firefox's main thread missed in both of its runs, and Chrome's main-thread ring came within 5 ms of empty, against 27 ms for the worker. Firefox's worker margin is thin: its ring bottomed at 8 ms, and with 11 ms less ring it dropped one quantum. Each worker configuration also ran clean in an earlier run at the same budget (see below). The engine itself is cheap: about 3% of one core for AOT (the median chunk takes 0.25 to 0.5 ms of its 10.7 ms) and 12% interpreted (1.25 ms), and the page held 95 to 97 fps in Chrome and 81 to 89 in Firefox.

The machine is shared, and contention matters. These runs were taken after waiting for the machine to settle; other work on it kept the CPU at 24 to 34% on average. Earlier runs on the first build (a ring of 85 ms in Chrome and 96 ms in Firefox, so 125 to 135 ms in all, over the line, and the UI load heavier than designed, see below) with the machine at 60 to 70% on average, peaking near 99%, dropped out in the worker: Chrome 6 events (144 ms), Firefox 24 (355 ms), and the longest single render call in those runs was 98 to 108 ms against a median of 0.25 to 0.5 ms, which is the worker thread being descheduled, not computing. Firefox's worker also dropped 39 events (1.07 s) at 47% mean CPU in that series, 31 of them in a burst at 46 to 57 s when the machine went to 94%. Those runs differ from the cited ones in more than the load on the machine, so they show that the margin is thin under contention, not by how much. A client whose CPU is busy would probably need more buffer than the pass line allows, or the fallback; the fallback was not built or measured.

What was corrected on the way, so the numbers above are not mixed with others. The first series used a ring sized before the output latency was known, which put total buffering at 125 to 135 ms; the ring is now sized after it. In the same series the scripted UI load was heavier than designed (a frame-recorder bug left one more animation-frame chain running after every scenario, so the scenario step ran hundreds of times a frame by the end); the main-thread runs of that series (Chrome 48 to 56 events, Firefox 147 to 258) were caused by it and are not evidence, and the worker runs that passed under it were a harder test than the corrected one. Only the table above is cited, apart from the earlier runs named in the text.

Engine cost and build. The interpreted build held in the worker on a quiet machine in both browsers (table), at about four times the AOT cost: 12% of a core against 3%. On a saturated machine (98% CPU) the first interpreted worker run used 61% of a core and dropped 151 events in 30 seconds, so AOT leaves more room under contention; that was not repeated on a quiet machine or under controlled load. AOT needs the `wasm-tools` workload, which CI does not have, and grows the page to 3.8 MB brotli (1.7 MB interpreted); the worker's own .NET runtime is a second download, 2.2 MB (2.0 MB interpreted), cacheable. Nothing was trimmed.

How clip audio reaches the page, and what a listener downloads. Clips are fetched with `HttpClient` from the server, parsed from 16-bit PCM WAV in C# and held as 16-bit samples in the runtime that hosts the engine. Eight clips of 30 s (42.3 MB, 5.29 MB each) loaded in 1.5 to 2.2 s in Chrome and 0.25 to 1.9 s in Firefox over loopback, so the figure is about the size, not the network. That is 176 KB/s of audio per track for uncompressed WAV, paid once and cacheable, and nothing while playing; a 10-minute stereo track is 106 MB, so 16 such tracks would be 1.7 GB. A streamed server mix costs the same for any number of tracks but is paid for every second of listening: 192 KB/s as 48 kHz 16-bit PCM (115 MB in 10 minutes), or about 16 KB/s as 128 kbit/s Opus (9.6 MB), and takes a mixer per listener on the server. Neither compressed figure was run. Decoding compressed clips in the page (a managed decoder, or `decodeAudioData` called through built-ins) was not tried; for large projects it decides which side is cheaper.

The script. The shim is one file, `src/AudioLab.Web/wwwroot/audio-shim.js`, 77 lines and 3.9 KB, and it does two jobs: it is the `AudioWorkletProcessor` (the ring, the credits and the dropout counter), which [section 8](#8-javascript-policy) names, and it is the bootstrap of the worker, which has to start the .NET runtime, call its exports and post each chunk to the worklet's port, which section 8 does not name. It holds no engine logic. The worker role needs the owner's approval as a second role of that line; the main-thread case would need only the first. Two things the spike found about it: the .NET runtime hangs in a worker if the worker has set `onmessage` before `dotnet.create()` finishes, so the bootstrap starts the runtime first and says "booted"; and the worker's `[JSExport]` file has to compile into the worker host project while living in `src/AudioLab.Web/Interop/`, where the convention test requires all interop. The page side (the context, the node, the worker, the channel) needs no script: it is 14 imports of browser built-ins, as in D17. Building this would add a worker-host project to the [section 4](#4-solution-layout) table. The test harness (a Node and Playwright driver) is not in the repository.

Not run: Safari and WebKit, stock Firefox, a slow or battery-powered CPU, an output device other than the default, more than 16 tracks or heavier effects, runs longer than 10 minutes, the interpreted build under controlled load, and a server-side mix as the fallback.

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
| `src/AudioLab.Core` | Project model, commands and their handlers, undo history, the pure rules (tempo map, sections, groups, clip split, lyrics). No ASP.NET, no UI, no audio I/O. | nothing in this solution | Exists (contract version, project model v4, id service, JSON contract, command registry and history; tempo, marker, section, group, track, mixer, clip, MIDI, effect, automation, drum pattern, lyrics, score, stem, project settings, track settings and instrument commands with the tempo map, meter, section, group, mix, clip, note, grid, effect, automation, beat, lyrics, track and instrument rules) |
| `src/AudioLab.Audio` | The engine: DSP, mixer graph, real-time and offline rendering, synth and sampler, MIDI codec. Pure C#, no native dependencies. | Core | Exists (render plan, mixer graph, offline renderer, limiter, resampler, effect DSP; export, synth, sampler and MIDI codec to come) |
| `src/AudioLab.Devices` | Native audio and MIDI device I/O behind `IAudioDevice` and `IMidiInput`. | Core, Audio | Planned |
| `src/AudioLab.Engine` | HartsyInference-backed services behind interfaces: providers, model install, TTS, STT, stems, alignment, music, voice, wake word. | Core | Planned |
| `src/AudioLab.Server` | ASP.NET Core host: thin routes, the event hub, open-project sessions, the store, MCP, and serving the UI. | all of the above | Exists (host, the three doors, store) |
| `src/AudioLab.Web` | The Blazor WebAssembly UI. | Core, Audio | Planned |
| `src/AudioLab.Desktop` | The desktop shell: starts the server in-process and opens the window. | Server | Planned |
| `tests/AudioLab.Conventions.Tests` | Fails the build on style and policy the compiler cannot check. | none | Exists |
| `tests/AudioLab.Core.Tests` | Model, ids, JSON round trips and rejection; command registry and history; the domain commands and rules. | Core | Exists |
| `tests/AudioLab.Server.Tests` | Doors, configuration, routes, store atomicity, ownership and recovery. | Server | Exists |
| `tests/AudioLab.Audio.Tests` | Mixing, rendering and effects against known signals, block-size and allocation checks, golden renders. | Audio | Exists |
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
  command, a handler, a refusal and the registry's listing is D18; the domain commands' names and refusal rules are D19.
  A client never sends an id for something the command creates; the result lists the ids the handler allocated.
- **History.** Undo and redo on the server, coalesced by gesture: a drag is one step. History is in memory and is
  lost on restart. A gesture belongs to the client that sent it. Its commands each apply and show at once and share
  one undo step, recorded when the gesture closes. The same client sending another gesture or a single edit closes it,
  and so does undo, redo or the release that names it. A gesture that ends where it began leaves undo and redo exactly
  as they were. A client that previews locally (below) sends a gesture's commands at its release, usually one. A client may
  stream them while it drags; an edit by another client in between is then its own step (D18).
- **Store.** Open documents, a lock per project, debounced autosave, atomic writes. Projects and media are scoped to
  the user who owns them (D12).
  - A file is `users/<user>/projects/<project>.json` (media records in `media/`) under one configurable root, where each
    name is a hash of the id, so no id reaches a path. A read for another user finds nothing: the answer is 404, never
    403. A file is refused, with its error list, if it fails `ProjectValidator`, is stored under another project's name,
    or names another owner than the user it is stored for. Opening a project validates the whole file. The list reads
    only the first 16 KiB of each file (id, owner, name) and its modified time, so it never parses a project body: a file
    whose start is intact is listed and refuses to open, and a file whose start is wrong, or that cannot be read, is
    left out and counted, so one bad file never fails the list.
  - A write goes to a temporary file, is flushed, and is renamed over the project, so a crash leaves the whole old or
    the whole new file; leftover temporary files are removed at start, and one process at a time holds the root.
  - An open project is one session with a revision. Save means "write the open document now": a project changes only
    through commands, never by uploading a document. Autosave waits 2 s after the last change and at most 15 s in all;
    a failed write stays unsaved and is retried after 15 s, doubling to 5 min, and stopping the server writes every unsaved project. A session
    stays open until it is closed, its project is deleted or the server stops; closing writes what is unsaved first.
    Idle eviction waits for the hub, which knows which clients hold a project. Each project has its own lock; a short
    lock guards only the table of locks, so one project's slow load or write never stalls another.
  - Limits: a project file is at most 32 MiB by default (about 400,000 notes at 80 bytes each; a document is held whole
    in memory at several times that) and never more than 512 MiB; a user has at most 1000 projects (creating or
    duplicating beyond it is a 409); a request body is at most 64 KiB until a route takes uploads. A project too large to
    write is not retried until it changes. A file error is a 503, a full disk a 507, neither with a path in the body.
    Not limited yet: the number of open sessions, and total storage per user (hosted door, phase 10).
- **Hub.** Every open client of a project receives every change, including changes made by an agent (D20). A command
  runs through the registry and the project's history under the project's lock, and the new revision is then sent to
  the project's group, in order. The hub keeps no state of its own but the projects a connection joined; a session
  stays open after its last client leaves, until idle eviction is built. A change made any other way than a command
  (a direct `Update`, used by tests) is not announced and drops the undo history. A reconnecting client is a new
  connection with a new client id and must join again; automatic reconnection is SignalR's and is not exercised by a
  test. The server's registry is `CoreCommands.CreateRegistry()`, so a real server accepts the domain commands that Core ships (tempo, markers and sections so far).
  A client is sent each project's newest change and may skip the ones between (the outbox of a connection keeps one
  change per project), so it applies a change whose revision is above its own and keeps one that arrives before its join
  reply returns. A client that takes nothing for `HubSendTimeout` (10 s) is disconnected. A command runs twice on a
  session, the second time with the ids of the first, so its size can be checked before it is applied; a handler must be
  pure (D18). Core's command limit (16 Mi characters) is above the hub's (2 Mi), which the owner may want to lower in Core.
  The publish order belongs to the project's key, not to its session: a change, a join and a deletion each take it, even
  for a project that is not open, so a join can never open a project beside its deletion; a session closed while a client
  joins (not deleted) sends that client back with "the project is closing". Still unbounded: the cost of serialising the
  whole document once per revision, which is done under the project's order and lock, and the number of connections.
  Not built: patches in place of whole documents, a limit on the rate of commands, sharing a project between users,
  and moving the hub's message types into `AudioLab.Core` (section 4 lets `AudioLab.Web` reference only Core and
  Audio, so the UI needs them there).
- **Gestures.** A continuous gesture (dragging a clip, drawing automation) is previewed locally with the shared rule
  code and committed as one command on release. The UI never waits on the network to move something under the pointer.

## 6. Audio

One engine (`AudioLab.Audio`), one processing graph, used for live playback and for export (D5). Where it runs
depends on where the listener is (D9):

| Listener | Engine runs | Output |
|----------|-------------|--------|
| Desktop app, or a browser on the machine that runs the server | In the server process | The machine's audio device, through `AudioLab.Devices`. Low-latency monitoring, recording and MIDI devices live here. |
| A browser on another machine (hosted site, remote SwarmUI) | In a web worker in the page, through a minimal `AudioWorklet` shim ([D24](#2-decisions)); a server-side mix streamed to the page is the fallback | The page |
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
  did not come from its own proxy. Only the health route is outside the door: it names no user and shows no project.
- The Swarm and hosted doors trust a proxy that sends the shared secret (`AudioLab:Door:ProxySecret`, 32 characters or
  more, required at start) in `X-AudioLab-Secret` and the signed-in user in `X-AudioLab-User`: one printable ASCII name of up to
  128 characters, hashed into the user id so that any name maps to a valid id and two names never share one. A request
  without the secret gets 403, a proxied request without a usable user 401. The desktop door answers loopback peers only,
  and, because a web page the user has open can send requests to the loopback address, also requires a loopback `Host` (against
  DNS rebinding), refuses an `Origin` that is not its own scheme, host and port, and refuses a `Sec-Fetch-Site` other than
  `same-origin` or `none`; each refusal is a 403 and is logged with its reason. A client that sends neither header is let through.
- The desktop and Swarm doors refuse to start on an address that is not loopback; the hosted door may listen anywhere.
- The hub is WebSockets only, so a proxy of the Swarm or hosted door must forward the WebSocket upgrade of `/api/hub` with the
  secret and user headers, as it does for a route; the door checks the upgrade once, when the connection is made.
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
| O3 | The native audio device and MIDI input library. | A spike that checks license and upkeep. **Pass:** round-trip latency of 12 ms or less on the owner's audio interface, and correct device enumeration. |
| O4 | The desktop window: an embedded webview, or the default browser at first. | Owner, after O2. |
| O5 | Score notation. The extension uses the abcjs library for rendering, playback and timing. Options: a C# renderer, or that library on the allowlist. | Owner. |
| O6 | Where a hosted instance runs: on the GPU machine with the engine in-process, or on a web host with a remote engine. | Owner. |
| O7 | How a child AudioLab process shares VRAM with SwarmUI's own HartsyInference backend. | Design work before the extension cutover. |
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
| 1 | Spikes O1, O2, O3 and O9 each closed in this file, and the O10 evaluation recorded as D14. D14, D17 (O2) and D24 (O1) stay Proposed until the owner decides; Proposed is not closed. D17 also waits on a Firefox drag rerun on an idle machine. |
| 2 | Core and server: model, commands, history, store, hub, routes, and a minimal UI that opens a project. |
| 3 | MCP on the command registry (D11). |
| 4 | The audio engine: offline render and export, then real-time playback. |
| 5 | The UI, one domain at a time: tempo, markers, sections and groups; tracks and mixer; clips and media; effects, automation, MIDI, piano roll and beats. |
| 6 | Engine services and the model manager, and the media store: it must verify the existence, ownership (D12) and duration of the media that `clip_add`, `beat_lane_add`, `beat_lane_set_media` and `instrument_set_sample` name, which Core cannot check (D22, D25, D27). |
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
| 2026-11-22 | `AudioLab.Server` and `AudioLab.Server.Tests` added (section 4): the host skeleton, the desktop, Swarm and hosted doors, and the proxy contract (section 9); the desktop door also refuses a foreign `Host`, `Origin` or `Sec-Fetch-Site`. |
| 2026-11-22 | Tempo, marker and section commands, with the tempo-map, meter and section rules they share, added to `AudioLab.Core` (section 5); D19 proposed. |
| 2026-11-22 | The store: per-user hashed file names, atomic writes, open-project sessions with debounced autosave, and the project routes; save means flush (section 5). Rests on D12, D15 and D16 (Proposed). |
| 2026-11-22 | Group, track and mixer commands added to `AudioLab.Core` with the group and mix rules (section 5); D21 proposed. A name longer than the schema allows is refused as sent, and a missing value is an out-of-range one (D19). |
| 2026-11-22 | The hub (section 5): SignalR over WebSockets, behind the door; O8 closed as D20 (Proposed). A failing autosave is retried with growing delays. |
| 2026-11-22 | Hub: each connection has an outbox that keeps the newest change per project, so a slow client delays nobody and a stalled one is disconnected; deletions are announced in order; the hub limits are stated (section 5, D20). |
| 2026-11-22 | Hub: the publish order belongs to the project key, so a join and a deletion cannot interleave; a send that fails for any reason aborts the connection; a project is reported as closing when its session closes during a join (section 5). |
| 2026-11-22 | Clip commands added to `AudioLab.Core` with the clip rules (section 5): audio and MIDI clips, trim, split, copy and delete; D22 proposed. The server checks a clip's media before applying it. |
| 2026-11-22 | MIDI note and controller commands and effect chain commands added to `AudioLab.Core` with the note, grid and effect rules (section 5); D23 proposed. |
| 2026-11-22 | Automation and drum pattern commands added to `AudioLab.Core` with the automation and beat rules (section 5); D25 proposed. |
| 2026-11-22 | Lyrics, score and stem commands added to `AudioLab.Core` with the lyrics rules (section 5); D26 proposed. |
| 2026-11-22 | Project settings, track settings and instrument commands added to `AudioLab.Core` with the track and instrument rules (section 5); D27 proposed. |
| 2026-11-22 | O1 recorded as D24 (Proposed): the engine runs in a web worker in the page behind a minimal `AudioWorklet` shim and held 10 minutes with zero dropouts at 93 ms of output buffering in Chrome and Firefox on a machine at 24 to 34% CPU; the main thread missed in Firefox; a server-side mix is named as the fallback, not built or measured; evidence under D24; section 6 names the worker. |
| 2026-11-22 | `AudioLab.Audio` added: render plan, mixer graph with a pull `Render` and an offline renderer, a no-latency master limiter, resampling, and `LayerRules` in Core; D35 proposed. |
| 2026-11-22 | Effect DSP added to `AudioLab.Audio`: equaliser, compressor, delay, saturation and a zero-delay convolution reverb with golden renders; D39 proposed. |
