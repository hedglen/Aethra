# Aethra AI Agent Repository Sitemap

Use this file to orient quickly in the Aethra repo before making decisions or edits. It is the navigation and ownership map, not the policy canon. Follow the linked source-of-truth documents for the actual rules.

## Start Here

Read in this order when you begin a new task:

1. `docs/architecture/agent-sitemap.md`
   - Use this file to find the real authoring targets, avoid redirect stubs, and route questions to the right guide.
2. `docs/project/DIRECTION.md`
   - This is the canonical policy, architecture, terminology, and licensing direction.
3. `docs/development/copilot-instructions.md`
   - This defines how an agent is expected to work in this repo, including build and worklog requirements.
4. `docs/project/roadmap.md`
   - This is the active execution order unless the user explicitly redirects priorities.
5. `docs/development/worklog.md`
   - This is the latest implementation history and the best snapshot of what just changed.

Conflict rule:

- `docs/project/DIRECTION.md` wins when policy, architecture, terminology, or licensing guidance conflicts with another doc.
- `docs/project/roadmap.md` owns sequencing and current priority order.
- `docs/development/copilot-instructions.md` owns agent workflow, verification expectations, and the requirement to update the worklog.
- `docs/development/worklog.md` is the recent execution record, not the policy canon.

## Decision Routing

Use these documents for the corresponding decisions:

- Product direction, architecture boundaries, terminology, and licensing posture: `docs/project/DIRECTION.md`
- Current priorities, next sequence, and phase scope: `docs/project/roadmap.md`
- How an agent should work, verify changes, stop, and update docs: `docs/development/copilot-instructions.md`
- Latest shipped, in-progress, or recently validated repo reality: `docs/development/worklog.md`
- Native runtime provenance, bundled DLL obligations, and distribution notices: `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md`
- Direct mpv.net reuse and intake provenance: `docs/project/MPVNET_REUSE_MAP.md`
- Attribution, downstream reuse, redistribution, and citation expectations: `docs/project/ATTRIBUTION.md`, `NOTICE`, `LICENSE`, `CITATION.cff`
- Contributor and PR workflow expectations: `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`

## Repository Map

### Repo Root

- `README.md`
  - Public project entry point and the fastest high-level project overview.
- `docs/`
  - Canonical documentation tree for policy, workflow, roadmap, and architecture notes.
- `src/`
  - Real implementation root. This is where app source changes belong.
- `tests/`
  - Automated test projects and test assets.
- `tools/`
  - Reserved area for internal utilities and support tooling.
- `.github/`
  - Repository automation and pull request workflow surfaces.
- `Aethra.slnx`
  - Solution entry point for builds.
- `Directory.Build.props`
  - Shared build configuration that applies across the repo.
- `Aethra/`
  - Redirect-only transition surface for older paths. Do not treat it as the live app tree.

### `docs/`

- `docs/project/`
  - Direction, roadmap, attribution, and reuse-policy docs. Read here when deciding what Aethra should be.
- `docs/development/`
  - Agent workflow and implementation history. Read here when deciding how to work and what happened recently.
- `docs/architecture/`
  - Repository navigation and deeper structure notes. This sitemap lives here.
- `docs/contributing/`
  - Reserved for expanded contributor process docs beyond the root `CONTRIBUTING.md`.
- `docs/packaging/`
  - Reserved for unpackaged-first and optional MSIX/release distribution notes.

### `src/Aethra/`

- `App.xaml` and `App.xaml.cs`
  - Application startup and top-level app initialization. This is where runtime bootstrap begins.
- `Views/`
  - WinUI shell and presentation surfaces. Contains `MainWindow` plus XAML for the major UI surfaces.
- `Preferences/`
  - Preferences orchestration and page logic. Persistent configuration UI behavior belongs here.
- `Commands/`
  - `aethra:*` command IDs and dispatch behavior for first-party app actions.
- `Input/`
  - Binding models, defaults, parsing, gesture capture, and the runtime binding service.
- `Configuration/`
  - Disk-backed stores, import/export, portable-config import, and persistence helpers.
- `Profiles/`
  - Typed preference and playback/profile models that back the Preferences UI.
- `Services/`
  - Cross-cutting orchestration helpers that should not live directly in views.
- `Native/`
  - libmpv interop, runtime loader, GPU/software playback backends, and rendering bridge code.
- `NativeRuntime/`
  - Side-by-side native runtime bundle used by the app at runtime.
- `Controls/`
  - Reusable UI controls and WinUI-specific interaction helpers.
- `Models/`
  - Simple shared data models that do not belong in profiles or services.
- `ThirdPartyNotices/`
  - Native dependency notices and provenance records that must stay aligned with bundled runtime changes.
- `Assets/` and `Properties/`
  - Application assets, manifests, and publish profile metadata.

## Important Entry Points

- Startup:
  - `src/Aethra/App.xaml.cs`
  - Installs `NativeRuntimeLoader` and creates the main window.
- Main shell and playback chrome:
  - `src/Aethra/Views/MainWindow.xaml`
  - `src/Aethra/Views/MainWindow.xaml.cs`
  - This is the central UI composition and playback-surface orchestration layer.
- Preferences UI surface:
  - `src/Aethra/Views/FullSettingsPanel.xaml`
  - `src/Aethra/Preferences/FullSettingsPanel.xaml.cs`
  - Note the split: the XAML surface lives under `Views/`, while the orchestration code lives under `Preferences/`.
- Input defaults and runtime:
  - `src/Aethra/Input/InputBindingCatalog.cs`
  - `src/Aethra/Input/InputRuntimeService.cs`
  - `src/Aethra/Input/InputBindingSetting.cs`
- Persistence and import/export:
  - `src/Aethra/Configuration/AtomicFile.cs`
  - `src/Aethra/Configuration/PreferencesProfilesStore.cs`
  - `src/Aethra/Configuration/InputBindingSettingsStore.cs`
  - `src/Aethra/Configuration/PlaybackPersistenceStore.cs`
  - `src/Aethra/Configuration/MpvPortableConfigImporter.cs`
- GPU/native playback path:
  - `src/Aethra/Native/NativeRuntimeLoader.cs`
  - `src/Aethra/Native/NativeMpvContext.cs`
  - `src/Aethra/Native/NativeMpvOpenGlPlayer.cs`
  - `src/Aethra/Native/NativeMpvSoftwarePlayer.cs`
  - `src/Aethra/Native/AngleEglContext.cs`
  - `src/Aethra/Native/AngleD3D11SwapChainContext.cs`
- Automated tests:
  - `tests/Aethra.Tests/`
  - Use the folder names there to find coverage by domain: `Commands`, `Configuration`, `Input`, `Profiles`, `Services`, `Views`.

## Avoid Wrong Targets

- Treat `src/Aethra/` as the real app tree. If you are changing product behavior, UI, configuration, input, or native playback code, start there.
- Treat the top-level `Aethra/` folder as redirect-only compatibility surface. Its `README.md`, `COPILOT_INSTRUCTIONS.md`, `COPILOT_WORKLOG.md`, and `ROADMAP.md` point to canonical locations and should not become the primary source of new guidance.
- Do not author inside `bin/` or `obj/` directories. Those are build outputs, not source.
- When touching `src/Aethra/NativeRuntime/x64`, also review `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md`. Runtime bundle changes require provenance and notice updates in the same step.
- When a path looks duplicated between `Views/`, `Preferences/`, or old redirect docs, prefer the canonical guide docs and the live `src/Aethra/` source tree before assuming both are authoring targets.

## Agent Requirements

- Consult the guiding markdown set before making decisions:
  - `docs/project/DIRECTION.md`
  - `docs/development/copilot-instructions.md`
  - `docs/project/roadmap.md`
  - `docs/development/worklog.md`
- Update `docs/development/worklog.md` after completed work. Record what changed, what was verified, and what still needs review or follow-up.
- If you change policy or guidance ownership, update `docs/project/DIRECTION.md` first, then the dependent docs, then add a worklog entry.
- If you change behavior, architecture, or workflow docs, keep links aligned across the entrypoint docs so future agents can still navigate from the top.
