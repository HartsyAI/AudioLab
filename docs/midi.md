# MIDI files in and out

The server turns a Standard MIDI File into a MIDI clip on an instrument track, and a MIDI clip back into a file. The design
is [DESIGN.md D46 and D42](../DESIGN.md#2-decisions) (Proposed). All routes are behind the door: the project is the
signed-in user's, or it is a 404.

| Route | Answer |
|-------|--------|
| `POST /api/projects/{id}/tracks/{track}/midi?start=&name=` | Imports the file in the body as a new clip. `201` with a summary. |
| `GET /api/projects/{id}/clips/{clip}/midi` | A MIDI clip as `audio/midi`, a format 0 file at 480 ticks a quarter note. |
| `GET /api/projects/{id}/tracks/{track}/midi` | The same for a track that has exactly one MIDI clip. |

## Import

The body is the file itself, with `Content-Type: audio/midi` (`audio/x-midi` and `audio/mid` are taken too), up to
`AudioLab:Midi:MaxFileBytes` (16 MiB by default). `start` is where the clip goes on the timeline in seconds (0 when not
given); `name` is the clip's name ("MIDI 1", "MIDI 2", ... when not given). Each may be sent once.

```bash
curl -sS -X POST "http://127.0.0.1:5280/api/projects/$PROJECT/tracks/$TRACK/midi?start=8&name=Verse%20keys" \
  -H "Content-Type: audio/midi" --data-binary @verse.mid
```

The whole import is one undo step and every client of the project sees the clip appear. The answer:

| Member | Meaning |
|--------|---------|
| `clip`, `revision` | The new clip's id and the project's revision once it is in. |
| `notes`, `controlChanges` | How many the clip holds. |
| `length` | The clip's length in seconds: to the file's last End of Track or the end of its last note. |
| `bpm`, `tempoChanges` | The file's starting tempo and its later changes. **The project's tempo is not changed**: notes keep the file's seconds. |
| `ignoredNoteOffs`, `unclosedNotes` | Note-offs that ended nothing (left out); notes never ended (they last to the end of their track). |

The notes of every track and channel of the file go into one clip, ordered by start and pitch. Format 0 and 1 files are read;
format 2, SMPTE timing, and anything the reader judges damaged are refused (below).

| Status | When |
|--------|------|
| 400 | `start` is not a number, or `start` or `name` is sent twice. |
| 404 | The project or the track is not the user's or does not exist (the same answer). |
| 413 | The file is over the limit, or the clip would make the project larger than the server stores (`reason` is `limit`). |
| 415 | The content type is not a MIDI one. |
| 422 | Not an accepted MIDI file (the title says why); not an instrument track (`rejected`); a name or value breaks the project's rules. |
| 429 | The user, or the server, is running as many MIDI jobs as it allows. `Retry-After` says when to retry. |

The reader is strict on purpose: a file with bytes after its last chunk, a track that declares more than the file holds, a
running status that crosses a meta event and the like are refused rather than guessed at. A refused file changes nothing.

## Export

A clip is written as a format 0 file that starts where the clip does, at the project's tempo there, with the project's later
tempo changes and its time signature. Notes are cut at the clip's end, controller events after it are left out, and a sustain
pedal still down at the end is let up. A track with no MIDI clip, or with several, is a `422` (`rejected`); the answer for
several names the clip ids to export one by one. The file name comes from the clip's (or track's) name with anything a file
name cannot hold made an underscore.

## From an agent

The command behind the import, `midi_clip_import`, is also an MCP tool (see [mcp.md](mcp.md)): it takes the notes and
controller events as JSON, which suits a small clip. A file is imported through the route above.

Not built: taking the tempo, time signature or key of a file into the project, merging the clips of a track into one file,
files over 16 MiB, and a lenient reader.
