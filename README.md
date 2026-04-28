# Aethra
Bright media. Pure playback.

Clarity in every frame.

A native Windows video player with a clean dark UI, responsive controls, and serious playback ambitions.
Right-click is the interface.

Built with WinUI 3 + C#.

## Status

Active Windows-first development. The app currently targets x64-first and uses a native libmpv backend with a WinUI shell. Packaging is unpackaged-first, with MSIX-capable tooling kept optional.

## Build

From the repository root:

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
```

## Run

Launch from Visual Studio/Visual Studio Code, or run the built executable from:

`src\Aethra\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\`

## Architecture

- App UI and shell: `src/Aethra/Views`
- Preferences surfaces: `src/Aethra/Preferences`
- Native playback interop: `src/Aethra/Native`
- Commands: `src/Aethra/Commands`
- Input runtime and bindings: `src/Aethra/Input`
- Configuration and persistence: `src/Aethra/Configuration`
- Profiles and domain types: `src/Aethra/Profiles`, `src/Aethra/Models`
- Core app services: `src/Aethra/Services`
- Runtime native binaries: `src/Aethra/NativeRuntime/x64`

## License

This repository is licensed under the [MIT License](LICENSE).

## Planning and Direction

- Documentation index: `docs/README.md`
- Active roadmap: `docs/project/roadmap.md`
- Architectural guidance for agent-driven changes: `docs/development/copilot-instructions.md`
- Historical implementation log: `docs/development/worklog.md`

## Contributing

See `CONTRIBUTING.md` for workflow and contribution expectations.

## Security

See `SECURITY.md` for reporting vulnerabilities.
