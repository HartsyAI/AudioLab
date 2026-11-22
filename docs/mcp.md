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

## Who the agent is

The agent acts as the user the server's door resolved for the request, and it can touch only that user's projects. A
project of another user is reported as not found, exactly as a project that does not exist is.

| Door | What an agent needs |
|------|---------------------|
| Desktop | Nothing. A request from a web page of another origin is refused (403). |
| SwarmUI tab | The proxy's secret and user headers, as for any route. |
| Hosted | Not possible yet: there is no credential an agent can hold. |

## Tools

- **One tool per command**, named by the command's wire name (`tempo_set_bpm`, `marker_add`, `track_add`, ...). Its
  description and input schema are the command's, generated from the command record, so `tools/list` is always the
  complete, current list of what can be done to a project. A command added to AudioLab appears here with no other change.
- `list_projects`: your projects, newest first, with the id every other tool takes.
- `get_project`: a project's document and its revision.
- `get_history`: the revision, how many undo and redo steps there are, and the command each would take back or put back.
- `undo` and `redo`: one step, for everyone who has the project open.

A project is also a resource: `audiolab://projects/{id}` is its document.

### A call

A command's arguments are the command itself, and always name the project:

```json
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"marker_add","arguments":{"project":"prj_01a2...","time":2.5,"name":"Verse"}}}
```

The result is JSON, in the text and in `structuredContent`:

```json
{"succeeded":true,"changed":true,"revision":2,"errors":[],"created":["mrk_..."]}
```

`revision` is the project's revision after the call. `created` lists the ids the command allocated, which is how an agent
learns the id of what it just added; never invent an id. A command that changes nothing succeeds with `changed: false`.

### Refusals

A command that is refused is an ordinary result with `isError: true`, not a protocol error, and changes nothing:

```json
{"succeeded":false,"changed":false,"revision":1,"errors":[{"code":"rejected","message":"There is nothing to undo."}],"created":[]}
```

| `code` | Meaning | What to do |
|--------|---------|------------|
| `malformed` | The arguments are not valid for the tool: a member missing, unknown or of the wrong type. | Fix the call from the tool's schema. |
| `invalid` | A value is out of range, or the edit would leave a project that breaks the rules. | Use a value inside the range the schema states. |
| `rejected` | The values are fine but a rule forbids the edit on this project. | Read the project and choose another edit. |
| `not_found` | The project, track, clip or other target does not exist (or is not yours). | List or read again; ids come from results. |
| `limit` | A limit of the server was reached. | Do less in one call, or read the message. |

Only a tool or resource that does not exist is a protocol error (`InvalidParams`).

## Limits

- A request is at most 1 MiB (`AudioLab:Mcp:MaxRequestBytes`); a larger one is answered 413.
- `get_project` and the project resource return a document of at most 1,048,576 characters
  (`AudioLab:Mcp:MaxDocumentChars`); a larger project is refused with `limit`. There is no partial read yet.
- There is no limit on the rate of calls, as there is none on the UI's.
- Until the media store exists, a command that names a media file (`clip_add`, `beat_lane_add`, `instrument_set_sample`
  and the like) accepts any well-formed media id, as it does from the UI ([DESIGN.md D22](../DESIGN.md#2-decisions)).
