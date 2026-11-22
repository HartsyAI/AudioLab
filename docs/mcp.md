# Driving AudioLab from an agent (MCP)

The AudioLab server speaks the [Model Context Protocol](https://modelcontextprotocol.io) at `/api/mcp`, so an agent can
read your projects and edit them with the same commands the UI sends. Whatever the agent changes, every UI that has the
project open shows at once.

The design is [DESIGN.md D36](../DESIGN.md#2-decisions) (Proposed). This page says how to use it.

## Start the server

```bash
dotnet run --project src/AudioLab.Server
```

On the desktop door (the default) it listens on `http://127.0.0.1:5280`, needs no sign-in and answers loopback only, so
the endpoint is `http://127.0.0.1:5280/api/mcp`. Projects are stored under the configured `AudioLab:Storage:Root`.

## Connect a client

The transport is Streamable HTTP, stateless: there is no session to keep or resume, and each request is checked on its own.
A request that carries an `Mcp-Session-Id` is refused.

**Claude Code** speaks Streamable HTTP itself:

```bash
claude mcp add --transport http audiolab http://127.0.0.1:5280/api/mcp
claude mcp list
```

**A client that only speaks stdio** (Claude Desktop's local configuration is one) needs a bridge from stdio to HTTP.
AudioLab ships none. A third-party bridge such as [mcp-remote](https://github.com/geelen/mcp-remote) is the usual
answer; it has not been tried with AudioLab.

**Anything else** that speaks Streamable HTTP takes the URL above. The server has not been run against Claude Code or
the MCP inspector yet; it has been run against the C# SDK's client and against plain `curl`:

```bash
curl -s http://127.0.0.1:5280/api/mcp \
  -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

The client must be a program, not a web page: the endpoint refuses a request that a browser marks as coming from another
origin or site (`Origin`, `Sec-Fetch-Site`; 403), and a POST whose `Content-Type` is not `application/json` (415). A page
of a web-based inspector is another origin, so it is refused; use one that runs as a program.

## Who the agent is

The agent acts as the user the server's door resolved for the request, and it can touch only that user's projects. A
project of another user is reported as not found, exactly as a project that does not exist is.

| Door | How an agent connects |
|------|-----------------------|
| Desktop | Nothing to give: it needs no sign-in, and a request from a web page of another origin is refused. |
| SwarmUI tab | Through Swarm's own proxy URL, signed in as the Swarm user. Never hand an agent the proxy secret: it names any user. |
| Hosted | Not possible yet: there is no credential an agent can hold. |

## Tools

- **One tool per command**, named by the command's wire name (`tempo_set_bpm`, `marker_add`, `track_add`, ...). Its
  description and input schema are the command's, generated from the command record, so `tools/list` is always the
  complete, current list of what can be done to a project. A command added to AudioLab appears here with no other change.
  A command that is no undo step (arming a track, monitoring) says so in its description. A tool carries the usual hints:
  a command that removes, deletes, clears or ungroups is marked destructive, and one that sets a value, renames, removes,
  deletes or clears is marked idempotent. They come from the command's name and are advice to a client, not a guarantee.
- `list_projects`: your projects, newest first, with the id every other tool takes.
- `get_project`: a project, read in parts (below).
- `get_history`: the revision, how many undo and redo steps there are, and the command each would take back or put back.
- `undo` and `redo`: one step, for everyone who has the project open. They need the revision the agent last read (below).

A project's whole document is also a resource: `audiolab://projects/{id}`, up to the document limit (below).

There is no tool to upload media or to list it: an agent cannot add a sound, only edit what a project already holds.

### Reading a project

`get_project` with only the project returns a summary: counts, and each track's id, name, kind and number of clips. Read
more with `section`:

| `section` | Returns | Also needs |
|-----------|---------|------------|
| `summary` (default) | counts, and the tracks with ids, names, kinds and clip counts | |
| `settings` | everything but the lists below: tempo, time signature, master, metronome, view | |
| `markers`, `sections`, `groups` | that list | |
| `beat_pattern` | the drum machine | |
| `tracks` | the tracks, each without its clips | `track` narrows to one |
| `audio_clips` | a track's audio clips | `track` (and `clip` narrows to one) |
| `midi_clips` | a track's MIDI clips, each without its notes | `track` (and `clip`) |
| `notes`, `control_changes` | a MIDI clip's notes or control changes | `track` and `clip` |

A list section also takes `offset` and `count`, and says its `total`, so a long list is read in pages. Every id comes from
an earlier result; never invent one.

A part cannot be divided further than this table: one track's automation and effects (`tracks`), the tempo map (`settings`),
the drum machine and a single clip are each returned whole. If one is over the limit the read is refused and there is
nothing smaller to ask for; the resource gives the whole document, up to its own limit.

### Results are bounded

A result is at most 80,000 characters (`AudioLab:Mcp:MaxResultChars`). A client puts the whole result in its context:
Claude Code warns above 10,000 tokens of tool output and by default refuses above 25,000 (`MAX_MCP_OUTPUT_TOKENS` raises
it; see [its MCP docs](https://code.claude.com/docs/en/mcp)). Dense JSON can be 3 characters to a token, so 80,000
characters can be more than that default: lower the limit, or read less at a time. A longer read is refused with a
`limit` error that says how to read less (a smaller `section`, `count` and `offset`, or one `track` or `clip`). The JSON
is sent once, as text. The answer to a command, an undo or a redo is never replaced: the change has been made.

### A call

A command's arguments are the command itself, and always name the project:

```json
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"marker_add","arguments":{"project":"prj_01a2...","time":2.5,"name":"Verse"}}}
```

The result is JSON in the text:

```json
{"succeeded":true,"changed":true,"revision":2,"errors":[],"created":["mrk_..."]}
```

`revision` is the project's revision after the call. `created` lists the ids the command allocated, which is how an agent
learns the id of what it just added; never invent an id. A command that changes nothing succeeds with `changed: false`.
An argument named twice is read as its last value.

### Undo and redo are shared with the people using the project

The history belongs to the project, not to the agent: an undo takes back the latest step, whoever made it, and a person
may have edited a moment ago. So `undo` and `redo` require `expectedRevision`, the revision `get_history` reported, and
are refused as `rejected`, with the project's current revision, when the project is no longer at it; nothing changes.
Read the history again and decide. Every command an agent sends is one undo step and clears the redo steps, as a person's
does.

A person's drag in progress (a gesture that is not ended) is not in the history, and a drag that has paused leaves the
revision where `get_history` saw it. An `undo` then closes the gesture and takes it back, as the UI's own undo would.
When the history shows nothing to undo, an `undo` can still change the project if someone is mid-gesture.

### Refusals

A command that is refused is an ordinary result with `isError: true`, not a protocol error, and changes nothing:

```json
{"succeeded":false,"changed":false,"revision":1,"errors":[{"code":"rejected","message":"There is nothing to undo."}],"created":[]}
```

| `code` | Meaning | What to do |
|--------|---------|------------|
| `malformed` | The arguments are not valid for the tool: a member missing, unknown or of the wrong type. | Fix the call from the tool's schema. |
| `invalid` | A value is out of range, or the edit would leave a project that breaks the rules. | Use a value inside the range the schema states. |
| `rejected` | A rule forbids it: nothing to undo, the project moved on since `expectedRevision`, or it will not open. | Read again, then choose. |
| `not_found` | The project, track, clip or other target does not exist (or is not yours). | List or read again; ids come from results. |
| `limit` | A limit of the server was reached: a result or a project too large. | Do less in one call, or read the message. |

`unknown_command` and `wrong_project` are classes of the hub that never occur here: an unknown tool is a protocol error,
and the project is taken from the arguments, so a command always names the project it is applied to.

Only a tool or resource that does not exist is a protocol error (`InvalidParams`), and, because a resource has no error
result, a resource that cannot be returned: `InvalidParams` for a uri that is not a project's or a document over its limit,
`ResourceNotFound` for a project that is not yours or does not exist, `InternalError` when the store cannot be read.

## Limits

- A request is at most 1 MiB (`AudioLab:Mcp:MaxRequestBytes`); a larger one is answered 413.
- A user has at most 4 calls in flight (`AudioLab:Mcp:MaxConcurrentCallsPerUser`); a further one is answered 429 with
  `Retry-After: 1`. The hub likewise runs one call at a time per connection.
- A result is at most 80,000 characters (`AudioLab:Mcp:MaxResultChars`), as above.
- The project resource is a document of at most 1,048,576 characters (`AudioLab:Mcp:MaxDocumentChars`); a larger project is
  read with `get_project` in parts.
- There is no limit on the rate of calls, as there is none on the UI's.
- Reading a project opens it and it stays open until the server shuts down, as with the hub.
- Until the media store exists, a command that names a media file (`clip_add`, `beat_lane_add`, `instrument_set_sample`
  and the like) accepts any well-formed media id, as it does from the UI ([DESIGN.md D22](../DESIGN.md#2-decisions)).
