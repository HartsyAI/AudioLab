# AudioLab

A digital audio workstation with AI audio built in, written in C#. It runs as a desktop app, inside a SwarmUI tab,
and as a hosted site, from one codebase, on the [HartsyInference](https://github.com/HartsyAI/HartsyInference) engine.

**Status: early.** The repository holds the design, the contributor rules and the build gates. The application is not
usable yet. The working DAW today is the [SwarmUI-AudioLab](https://github.com/HartsyAI/SwarmUI-AudioLab) extension,
which this project will replace.

## Read first

- [DESIGN.md](DESIGN.md): what AudioLab is, the architecture decisions, the open questions and the roadmap.
- [AGENTS.md](AGENTS.md): how work is done here and the code style, for people and for coding agents.

## Build

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet format AudioLab.slnx --no-restore --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

The build treats warnings and code-style violations as errors, and CI runs the same four commands.

## License

[MIT](LICENSE)
