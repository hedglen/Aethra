# Contributing to Aethra

## Development flow

1. Create a small, focused change.
2. Keep architecture boundaries clear (UI, commands/input, configuration, native backend).
3. Build locally before opening a PR:
   - `dotnet build .\Aethra.slnx -p:Platform=x64`
4. Update relevant docs/worklog when behavior or architecture changes.

## Pull requests

- Keep PRs narrow and reviewable.
- Prefer one concern per PR (structure move, behavior change, or cleanup).
- Include a short test plan in the PR description.

## Coding conventions

- C#, nullable enabled.
- File-scoped namespaces preferred.
- Avoid broad unrelated refactors in the same change.
- Keep hot input/playback paths allocation-light.

## Architecture expectations

- `Views`: UI composition and event wiring.
- `Commands`: app command IDs and dispatch.
- `Input`: runtime gesture/binding domain (in progress).
- `Configuration`: persistence and disk store behavior.
- `Native`: interop and playback backend details.

## Questions

Open a draft PR early if you want architecture feedback before completing implementation.
