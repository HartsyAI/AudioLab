# What a user can do with a project

Every route is under `/api` (and under the path base the server is configured with), behind the door: the user is the one
the door resolved, and a project, track, clip or file that is another user's is a `404`, the same answer as one that does not
exist. A change always goes through a command ([DESIGN.md section 5](../DESIGN.md#5-the-server-owned-project)); the routes
here read a project, take a file in or send a file out.

| To do | How | Details |
|-------|-----|---------|
| Edit the project | A command, over the hub or as an MCP tool | [mcp.md](mcp.md) |
| Upload, download or delete a sound | `/api/media` | D37 in DESIGN.md |
| **Export the mixdown as a WAV file** | `POST /api/projects/{id}/export/mix` | [export.md](export.md) |
| **Export the stems as a ZIP of WAV files** | `POST /api/projects/{id}/export/stems` | [export.md](export.md) |
| **Import a .mid file as a MIDI clip** | `POST /api/projects/{id}/tracks/{track}/midi` | [midi.md](midi.md) |
| **Export a MIDI clip or a track as a .mid file** | `GET /api/projects/{id}/clips/{clip}/midi`, `GET .../tracks/{track}/midi` | [midi.md](midi.md) |

An export is a project-level action and not a command: it changes nothing in the project, so it has no undo step and is not
announced; what it sends is the project's current state, including changes that are not saved yet. A MIDI import is a
command (`midi_clip_import`), so it is one undo step and every client of the project sees it.

Export and MIDI answers that refuse carry a `reason` (`bad_options`, `busy`, `missing_media`, `too_large`, `timed_out`,
`empty`, or a command's `rejected`, `invalid`, `limit`) in a problem document; the tables in those pages list them.
