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

`Aethra\bin\x64\Debug\net10.0-windows10.0.19041.0\`

## Architecture

- App UI and shell: `Aethra/Views`
- Preferences surfaces: `Aethra/Preferences`
- Native playback interop: `Aethra/Native`
- Commands: `Aethra/Commands`
- Input runtime and bindings: `Aethra/Input`
- Configuration and persistence: `Aethra/Configuration`
- Profiles and domain types: `Aethra/Profiles`, `Aethra/Models`
- Core app services: `Aethra/Services`
- Runtime native binaries: `Aethra/NativeRuntime/x64`

## License

This repository is licensed under the [MIT License](LICENSE).

## Planning and Direction

- Active roadmap: `Aethra/ROADMAP.md`
- Architectural guidance for agent-driven changes: `Aethra/COPILOT_INSTRUCTIONS.md`
- Historical implementation log: `Aethra/COPILOT_WORKLOG.md`

## Contributing

See `CONTRIBUTING.md` for workflow and contribution expectations.

## Security

See `SECURITY.md` for reporting vulnerabilities.
