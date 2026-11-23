# Exporting a project (WAV and stems)

The server renders a project offline, with the same engine that plays it, and streams the result to the response. The
design is [DESIGN.md D45 and D40](../DESIGN.md#2-decisions) (Proposed). Both routes are behind the door, like every
project route: the project is the signed-in user's, or it is a 404.

| Route | Answer |
|-------|--------|
| `POST /api/projects/{id}/export/mix` | The mixdown, one `audio/wav` file. |
| `POST /api/projects/{id}/export/stems` | A ZIP (`application/zip`) with a WAV file for each track with sound and each fed bus. |

The body is optional JSON (`Content-Type: application/json`); an empty body means every default. A member that is not
listed, or has the wrong type, is a 400.

| Member | Meaning | Default |
|--------|---------|---------|
| `format` | `pcm16`, `pcm24` or `float32` (a float file keeps every sample as rendered) | `pcm24` |
| `dither` | `none`, `rectangular` or `triangular`; a float file ignores it | `triangular` |
| `seed` | Seeds the dither; the same project and options give the same bytes | `1` |
| `sampleRate` | `44100`, `48000`, `88200` or `96000` | `48000` |
| `start`, `end` | The range in seconds; `end` is after `start` | from 0 to the end of the last audible clip |
| `tail` | Run on past the end until the effects have rung out | `true` |
| `tailSeconds` | How long that run-on is, up to 60, instead of as long as the effects need; needs `tail` | by the effects |
| `tracks` | Stems only: the track ids to make stems of, each once | every track with sound and every fed bus |

The answer is the file itself, with `Content-Length` (the exact size, known before the first byte), `Content-Disposition:
attachment` (a name made from the project's name, with nothing that can be a path) and `Cache-Control: no-store`.
Nothing is written to disk on the server.

## Refusals

A refusal is sent before the first byte, as `application/problem+json` with a `reason` (except where the table has none).
`details`, when there is something to list, is always an array of sentences, each naming what it is about (a clip and the
media id it plays, a track id); it is absent otherwise.

| Status | `reason` | When |
|--------|----------|------|
| 400 | `bad_options` | The body is not a valid export request (an unlisted member, a wrong type), a member is out of range, a track is unknown or repeated. |
| 404 | | The project is not the user's or does not exist (the same answer). |
| 409 | | The project was closed as the export began, or a media file it plays is being deleted (retry). |
| 415 | | The body is not `application/json`. |
| 422 | `missing_media` | A clip, drum lane or sampler names a file that is missing, of another length or not the user's; `details` says which. |
| 422 | `too_large` | Over `MaxSeconds` of audio in a file (judged first, from the project, before any file is read), over `MaxBytes` or the 4 GiB a file holds, or media too large to decode. |
| 422 | `empty` | Nothing sounds in the range, or there is no track to make a stem of. |
| 422 | `timed_out` | The export took longer than `MaxTime` before its first byte. |
| 429 | `busy` | As many exports are running as the user or the server allows; `Retry-After` says when to retry. Nothing is queued. |
| 503, 507 | | The project store or the user's media cannot be read, or the disk is full (no path is in the body). |

## When an export stops after its first byte

The status has gone out by then, so a failure, a client that leaves, or the time limit ends the connection instead of
ending the body: the client sees a transfer that stops short of its `Content-Length` and must discard it. A file whose
length matches what was promised is complete. (HTTP clients that check the length, such as `curl` and .NET's `HttpClient`,
report the short transfer as an error.)

## Limits

All under `AudioLab:Render`: `MaxSeconds` (3600), `MaxBytes` (2 GiB), `MaxTime` (10 minutes, request to last byte),
`MaxConcurrentExportsPerUser` (2), `MaxConcurrentExports` (half the processor cores, at least 2), `MaxTailSeconds` (60),
`MaxDecodedBytesPerFile` (1 GiB) and `MaxDecodedBytesPerRender` (2 GiB). The server refuses to start on a value that cannot work.

## Examples

```bash
# 24-bit mixdown of the first ten seconds, stored as mix.wav
curl -sS -X POST http://127.0.0.1:5280/api/projects/$PROJECT/export/mix \
  -H "Content-Type: application/json" -d '{"end":10}' -o mix.wav

# stems of two tracks as 16-bit files with no dither
curl -sS -X POST http://127.0.0.1:5280/api/projects/$PROJECT/export/stems \
  -H "Content-Type: application/json" \
  -d '{"format":"pcm16","dither":"none","tracks":["t1","t2"]}' -o stems.zip
```

Not built: MP3, FLAC and other compressed formats, files over 4 GiB (RF64, ZIP64), a progress report, and a job queue
(phase 10).
