# Aethra
Bright media. Pure playback.

Clarity in every frame.

A native Windows video player with a clean dark UI, responsive controls, and serious playback ambitions.
Right-click is the interface.

Built with WinUI 3 + C#.

## Why Aethra

- Native Windows app architecture (WinUI shell + native media backend), not a web wrapper.
- Playback quality and configurability are first-class goals from day one.
- App-owned command/input model (`aethra:*`) enables customizable controls while staying fast on the hot path.
- Preferences are moving toward a typed, persistent model so users can tune behavior without fragile text-edit workflows.
- Free, open, and transparent by design: no telemetry and public roadmap/worklog-driven development.

## Current State

Active Windows-first development with x64 focus.

- **Done:** GPU-first playback path through OpenGL via ANGLE (software fallback retained).
- **Done:** foundational command dispatch, controls runtime, and preferences/profile depth increments.
- **In progress:** reliability and shell smoke hardening plus deeper parity across remaining preferences areas.
- **Next:** continue roadmap-driven Phase 3 completion and then move into broader customization depth.

See `docs/project/roadmap.md` for the live execution map and `docs/development/worklog.md` for implementation history.

## Build Prerequisites

- Windows 10/11 (project currently targets `net10.0-windows10.0.19041.0`).
- .NET 10 SDK.
- x64 development environment.
- Optional: Visual Studio/VS Code for launch/debug convenience.

## Build

From the repository root:

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
```

## Run

Launch from Visual Studio/Visual Studio Code, or run the built executable from:

`src\Aethra\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\`

## Architecture At A Glance

- App UI and shell: `src/Aethra/Views`
- Preferences surfaces: `src/Aethra/Preferences`
- Native playback interop: `src/Aethra/Native`
- Commands: `src/Aethra/Commands`
- Input runtime and bindings: `src/Aethra/Input`
- Configuration and persistence: `src/Aethra/Configuration`
- Profiles and domain types: `src/Aethra/Profiles`, `src/Aethra/Models`
- Core app services: `src/Aethra/Services`
- Runtime native binaries: `src/Aethra/NativeRuntime/x64`

## Contributing

Contributions are welcome.

- Start with `CONTRIBUTING.md` for workflow and review expectations.
- Use `docs/project/roadmap.md` to understand current execution priorities.
- Use `docs/development/worklog.md` for recent implementation context.
- Keep PRs focused and include build/test/smoke notes.

## Build Your Own App

You are explicitly encouraged to fork Aethra, build your own app from this work, and ship derivatives (including commercial ones) under the selected permissive license terms.

To keep credit transparent and ecosystem-friendly:

- Keep required license text and notices with redistributions.
- Keep attribution guidance from `docs/project/ATTRIBUTION.md`.
- Keep citation metadata via `CITATION.cff` when relevant.

## License And Attribution

This repository is dual-licensed under:

- [MIT License](LICENSE), or
- [Apache License 2.0](LICENSE-APACHE)

You may choose either license when using this code.

### Attribution Expectations

- Keep license text and copyright notices in redistributed source.
- For Apache-2.0 redistributions, keep the project [NOTICE](NOTICE) file.
- When practical, acknowledge upstream source as: Aethra by Rob Hedglen ([github.com/hedglen/Aethra](https://github.com/hedglen/Aethra)).
- Citation metadata is provided in `CITATION.cff`.

## Planning and Direction

- Documentation index: `docs/README.md`
- Active roadmap: `docs/project/roadmap.md`
- Architectural guidance for agent-driven changes: `docs/development/copilot-instructions.md`
- Historical implementation log: `docs/development/worklog.md`

## Security

See `SECURITY.md` for reporting vulnerabilities.
