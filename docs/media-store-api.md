# Media store API (AudioLab.Server, Media/)

The routes and files of the media store, for the page, MCP and the audio engine. Decision: [D37](../DESIGN.md#2-decisions); the page's client of these routes is D55.

## Routes (all behind the door; owner is always the session's user; a missing file and another user's file are the same 404)

| Route | Answer |
|-------|--------|
| `POST /api/media?name=<label>` | Body is the raw WAV file, `Content-Type: audio/wav`. 201 + summary JSON, `Location`. 415 wrong type, 400 name missing or given twice, 422 not an accepted file (detail says why), 413 too large, 409 quota, 429 too many uploads at once (4 per user), 503/507 store trouble. |
| `GET /api/media` | `{ files: [summary], refused }`, newest first. |
| `GET /api/media/{id}` | Summary JSON. |
| `GET /api/media/{id}/content` (and HEAD) | The WAV bytes, `audio/wav`, range requests (206), `ETag` = `"<sha256>"`, `Cache-Control: private, max-age=31536000, immutable`. |
| `GET /api/media/{id}/peaks` (and HEAD) | The peak file, `application/vnd.audiolab.peaks`, ranges, `ETag` `"<sha256>-peaks1"`, immutable. Peaks are derived data: a missing or bad file is rebuilt from the WAV; while a rebuild runs longer than 3 s the answer is 503 with `Retry-After` (retry). |
| `DELETE /api/media/{id}` | 204; 409 while any project of the user plays it (present, undo/redo history of an open project, or a stored file) or while another delete of it is in progress; no force. |

Summary JSON (camelCase, no owner): `id, name, kind ("audio"), format ("wav"), encoding ("pcm16"|"pcm24"|"pcm32"|"float32"), duration (seconds, exact), sampleRate, channels, bytes, sha256, created (UTC)`.

Accepted: RIFF WAVE, 16/24/32-bit integer PCM or 32-bit float (plain or extensible), 1 to 8 channels, 8 to 384 kHz. Compressed formats arrive with the engine (phase 6). Limits: 512 MiB an upload, 5000 files and 20 GiB a user by default (`AudioLab:Media:*`).

## What a client must do

- **Send the summary's `duration` verbatim in `clip_add`.** The server refuses (`rejected`) a clip whose duration differs from the file's by more than one sample period at the file's own rate (1/sampleRate), and `not_found` for a file that is missing or another user's (one message for both). The clip keeps the value you send.
- **Check size and quota before a large upload.** A refusal sent before the body is read (a full quota, an announced length over the limit, a door refusal) can reach a client as a connection reset instead of a 4xx.
- A project whose clips name media the user does not have, or at another length, is refused when opened (422 with problems).
- Fetch audio with `HttpClient` from `/content` (D24); peaks from `/peaks`.

## Peak file (little endian; `PeakFile` in AudioLab.Core reads and writes it, use it in the page)

24-byte header: `ALPK`, version u16 = 1, channels u16, sample rate u32, frames u64, level count u16, reserved u16 = 0. Then per level: frames per peak u32, peak count u32. Then each level's values: for every peak, for every channel, min then max as int16 (sample x 32768, round to even, clamp; a 16-bit file's peaks are its exact samples). Levels: 64, 256, 1024, 4096, 16384 frames a peak; each coarser level is the min/max of four finer ones; the last peak covers what is left. `PeakFile.Read(bytes)` returns `PeakData` with `Levels[i].FramesPerPeak` and `Values`.

## Wiring in the Server

`ProjectSession` takes `IMediaCatalog`; `OpenProjects` takes `MediaStore` (after the registry); `ProjectEditService` takes `MediaStore` (last); `ProjectService.DuplicateAsync` calls `OpenProjects.RequireMediaAsync`. DI builds all of these; only tests that `new` them need an update (`TestStore` shows how). A command tool in MCP must go through `ProjectEditService.ApplyAsync` or `ProjectSession.Apply` to be checked.

## Core additions

`MediaRecord` (measured fields), `MediaSummary` / `MediaListing` / `MediaJsonContext` (the JSON the routes answer with, in Core so the page can read it), `IMediaCatalog.Find / FindIncludingPendingDelete`, `MediaRules.VerifyChange / DocumentProblems / DurationTolerance(record)`, `CommandHistory.HeldDocuments()`, `PeakFile / PeakData / PeakLevel / PeakHeader / PeakBuilder`. The WAV reader (`WavReader`) is in `AudioLab.Audio/Wav`, not Core.

## A new command that names a `MediaId`

`MediaCommandTests.CommandsThatNameMediaAreAllCoveredHere` lists every command that names media; add the new one to `_covered` with its own test or main goes red.

## Services

`MediaStore` takes `MediaOptions` and `TimeProvider`; `MediaService` takes a `PeakRebuilder`; both are built by DI (`AddMedia`).

## Opening and commands

- Opening or copying a project that names a file whose delete has begun answers 409 (retry); a media folder that cannot be read answers 503 on open and on command paths. The 422 for a project naming missing or wrong-length media lists `clip X (media id): ...`, and the hub shows it. `MediaStore.Find` never reads the disk: call `PrepareAsync(user, ct)` then `RequireReadable(user)` before asking (the command path and open path do). `IMediaCatalog` gained `IsDeleting`.
