# AudioLab — agent entry point

<role>
You are working on AudioLab, a standalone C# digital audio workstation with AI audio built in, targeting net10.0. The
server owns the project and every edit; the Blazor WebAssembly UI, MCP clients and the SwarmUI extension are consumers
that send typed commands. New behaviour goes into `AudioLab.Core` commands and server services, not into a consumer.

Code lives in `src/` (one folder per project) with `tests/` beside it.
</role>

<source_of_truth>
Two files decide everything, and they are kept short so you can read them whole:

- [DESIGN.md](DESIGN.md): what AudioLab is, every architecture decision, the project layout, the open questions and
  the roadmap.
- This file: how work is done here, and the code style.

Read both at the start of every task. Then:

- Build what DESIGN.md says. If the task, a plan you were handed, or the code disagrees with DESIGN.md, stop and ask
  the owner. Do not pick a side quietly and do not work around it.
- A change of design is a change to DESIGN.md, made in the same pull request as the code that depends on it, with a
  line in its change log. A decision that exists only in a chat or a pull request description does not exist.
- Do not create a second planning, architecture or style document. Each fact has one home; link to it.
- DESIGN.md marks each decision Decided, Proposed or Open. Say so in the pull request when your work rests on a
  Proposed one, and do not settle an Open one yourself: run the spike it names, or ask.
</source_of_truth>

<rules>
These apply to every task.

- **kalebbroo is the sole author.** Do not add a co-author, session or tool-attribution trailer to commits, pull
  request bodies or release notes, whatever a tool's default is.
- **C# first.** JavaScript and TypeScript are allowed only under the
  [JavaScript policy](DESIGN.md#8-javascript-policy), by an approved line in the allowlist. Second-guess every script:
  the default answer is to write it in C#.
- **Reuse before writing.** Search for an existing rule, service, component or helper before adding one. Keep new code
  modular, generic and parameterized, so callers share one implementation instead of each keeping a copy.
- **Ship work as a draft pull request.** Open it early, and merge it only after the checks below pass, CI is green and
  every review comment is resolved. One branch and one pull request per item.
- **Work in your own worktree and clean it up.** Create it from a fresh `origin/main` under the gitignored
  `.claude/worktrees/` folder, named after the branch. After the pull request merges, delete that worktree and its
  local and remote branch. Never edit in the main checkout; other agents share it.
- **Pin every package** in `Directory.Packages.props`. No floating versions, and no new NuGet dependency without the
  owner's agreement.
- **Never weaken a gate to get a change through.** Do not lower a severity in `.editorconfig`, add a `NoWarn`, a
  `#pragma warning disable`, a suppression attribute or a skipped test without the owner's agreement in that pull
  request.
</rules>

<code_style>
Mandatory. The tag after a rule says what enforces it: `[build]` fails `dotnet build`, `[format]` fails
`dotnet format --verify-no-changes`, `[test]` fails `AudioLab.Conventions.Tests`. Untagged rules are enforced in review.

Declarations and layout

- Explicit types; never `var` `[build]` or `dynamic` `[test]`.
- File-scoped namespaces `[build]`; one public type per file `[test]`; usings outside the namespace, System first, no
  unused usings `[build]`.
- Explicit access modifiers `[build]`. Prefer public: public for anything another type could reasonably use, internal
  for implementation details of a project, private for state and helpers.
- Classes are sealed unless designed for inheritance (`[build]` for non-public types). Fields that are never
  reassigned are readonly `[build]`.
- Primary constructors for assignment-only initialization, without a private field copy of the parameter. Ordinary
  constructors for validation, overloads or nontrivial setup.
- Value records are `readonly record struct`; options and configuration are record classes with `init` properties and
  `required` on mandatory ones. Never a mutable record struct.
- Naming `[build]`: PascalCase types, members and constants; `I`-prefixed interfaces; `T`-prefixed type parameters;
  camelCase locals and parameters; `_camelCase` private fields; `Async` suffix on async methods (tests excepted).
- Braces on every block `[build]`, Allman style `[format]`. Expression bodies for simple members.
- No `this.` qualification and language keywords over BCL names (`int`, not `Int32`) `[format]`.
- Pattern matching over cast-and-check `[build]`.
- Keep simple assignments and calls on one line. Soft line limit 120, hard limit 150. Wrap long signatures at
  parameters. One blank line between members; no padding inside braces.
- Member order: constants and statics, fields, constructors, properties, public methods, helpers, `IDisposable`,
  nested types.

Comments and errors

- XML documentation on every public member `[build]` and on internal APIs. One concise line, with `<summary>` and
  `</summary>` on the same line as the text `[test]`. `param` and `returns` tags only for a contract that is not obvious.
- Comments explain why, not what: one short line, and only for a constraint the code cannot state. No narrative, no
  history, no measured numbers.
- Validate arguments at boundaries and fail fast with a meaningful exception. Never continue with corrupted state.
- Catch ordinary failures at route and service boundaries, log them, and return a meaningful error. No empty catch,
  no blanket `catch (Exception)` in low-level code.

Async and contracts

- Return `Task` or `ValueTask`; `IAsyncEnumerable<T>` for streams. `CancellationToken` is the last parameter and is
  passed through. No `async void` except event handlers. No `Thread.Sleep`, no `GC.Collect`.
- JSON uses source-generated contexts over the typed records in `AudioLab.Core`. No hand-built JSON strings.

Audio

- Code reachable from the audio callback follows the [real-time rules](DESIGN.md#6-audio): no allocation, locks,
  logging, LINQ, I/O or exceptions for control flow.

Blazor

- A `.razor` file holds markup only; logic goes in its `.razor.cs` partial class `[test]`.
- No inline `<script>` `[test]`; no script file off the allowlist `[test]`; JavaScript interop only in
  `src/AudioLab.Web/Interop/` `[test]`.
- Shared state and behaviour live in a C# service or in `AudioLab.Core`, not copied between components.

Interface design

- Read [DESIGN.md section 7](DESIGN.md#7-user-interface) before any UI work, and open
  [the session mock](docs/design/session-mock.html) in a browser. The section is the whole visual vocabulary; the
  mock is what it looks like. What you build should be indistinguishable from the mock at the same size.
- The mock and DESIGN.md declare the same tokens `[test]`, and the mock's screen uses tokens only `[test]`. Change
  a token in both, in one pull request, and look at the mock afterwards.
- Styles use tokens only: no colour literal, and no literal font size, font family, radius, shadow or stacking
  value `[test]`. No viewport units `[test]`.
- Build screens from the component catalogue. Do not restyle a catalogue component from a screen, and do not
  hand-build a control the catalogue has. A new token, component or variant is a change to DESIGN.md in the same
  pull request.

Tests

- A test earns its place when its failure would otherwise be silent: command results and undo, model round trips
  and rejection of invalid documents, DSP against golden renders, store atomicity, concurrency, hub fan-out. Do not
  add a test that only shows the code runs.
- Unit tests are fast and deterministic and need no GPU, model weights, audio device or network. A test that needs
  one of those carries a trait and skips cleanly where the resource is missing.
</code_style>

<checks>
Run what `.github/workflows/ci.yml` runs before every push, from the repository root:

```bash
dotnet restore
dotnet format AudioLab.slnx --no-restore --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

`dotnet format AudioLab.slnx` without `--verify-no-changes` applies the fixes it can. Two rules (`this.` and BCL type
names) are reported only by `dotnet format`, not by the build, so a clean build is not a clean check.

State in the pull request what you ran live and what you only checked statically. Recording from a real microphone or
instrument and model-backed generation depend on the owner's hardware and models; say when they were not exercised.
</checks>

<working_style>
- Open a file before you make a claim about it. Answer questions about the code from what you have read, not from memory.
- Make the change and run the checks yourself when the task asks for a change. Propose a diff only when the task asks
  for a recommendation.
- Confirm with the owner before any action that is hard to reverse or visible to other people: force-pushes, deleting
  branches or worktrees you did not create for the pull request you just merged, discarding files you did not create,
  posting to external services, downloading model weights, or restarting shared GPU or SwarmUI state. Local,
  reversible actions such as editing files and running tests need no confirmation.
- Keep answers short. Lead with the result, and do not restate what the diff already shows.
- Delete any scratch scripts or helper files you created during the task before you finish it.
</working_style>

<maintaining_agent_files>
Agent instructions in this repository are written for current Claude models. Keep this file and DESIGN.md short enough
to read whole, keep a single source for each fact, and when a rule gains or loses a mechanical check, update its tag
in the code style above in the same pull request.
</maintaining_agent_files>
