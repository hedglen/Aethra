# Contributing to Aethra

## Development flow

1. Create a small, focused change.
2. Keep architecture boundaries clear (UI, commands/input, configuration, native backend).
3. Follow canonical direction/policy in `docs/project/DIRECTION.md` and execution order in `docs/project/roadmap.md`.
4. Build locally before opening a PR:
   - `dotnet build .\Aethra.slnx -p:Platform=x64`
5. Run a manual playback smoke when touching playback/input/persistence (open file, play/pause, seek, volume, fullscreen, Preferences open/close).
6. Update relevant docs/worklog when behavior or architecture changes (use `docs/development/worklog.md` for implementation history and `docs/project/roadmap.md` for active sequencing).

## Pull requests

- Keep PRs narrow and reviewable.
- Prefer one concern per PR (structure move, behavior change, or cleanup).
- Include a short test plan in the PR description.
- If native runtime binaries or their sourcing change, update `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md` in the same PR.

## Coding conventions

- C#, nullable enabled.
- File-scoped namespaces preferred.
- Avoid broad unrelated refactors in the same change.
- Keep hot input/playback paths allocation-light.

## Architecture expectations

- `Views`: UI composition and event wiring.
- `Commands`: app command IDs and dispatch.
- `Input`: runtime gesture/binding domain.
- `Configuration`: persistence and disk store behavior.
- `Profiles`: typed playback/rendering preference models.
- `Preferences`: user-facing persistent configuration surfaces.
- `Services`: cross-cutting app behaviors and orchestration helpers.
- `Native`: interop and playback backend details.

## Platform and packaging baseline

- Current support target is Windows x64-first.
- Unpackaged runs are the primary local-dev path.
- MSIX-capable tooling exists and can be used when a reviewed release workflow requires it.

## Questions

Open a draft PR early if you want architecture feedback before completing implementation.

## License and attribution for contributions

- By submitting a contribution, you agree your changes are licensed under the repository's **GPL-2.0-or-later** terms.
- This contribution license covers Aethra-owned source changes; it does not remove or replace obligations from bundled third-party native binaries.
- Public-binary default posture is LGPL-first for runtime dependencies unless owner-approved policy explicitly changes it.
- Keep attribution-related files current when touched by your change:
  - `LICENSE`
  - `NOTICE`
  - `CITATION.cff`
- If you import or adapt third-party source code, include provenance comments in the source file and update the intake map/provenance docs in the same PR.
- If your change adds, replaces, or reconfigures native runtime artifacts/dependency flags, update `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md` in the same PR and summarize compliance impact in the PR notes.
