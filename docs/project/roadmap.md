# Aethra Roadmap (Full V1)

This roadmap is the execution map for building a fully customizable, native, modern Aethra. It aligns to `docs/project/DIRECTION.md`, `docs/development/copilot-instructions.md`, and current implementation history in `docs/development/worklog.md`.

## Locked Direction

- License baseline: MIT OR Apache-2.0.
- Default public-binary native runtime posture: LGPL-first unless an explicit owner-approved distribution-policy change is recorded.
- Platform baseline: Windows x64-first.
- Packaging baseline: unpackaged-first, with optional MSIX-capable packaging.
- Architecture baseline: one main WinUI window, mpv render API, app-owned command/input/config behavior, no telemetry.

## Current State Snapshot

- Done: GPU-first playback path is working via OpenGL through ANGLE with software fallback retained.
- Done: foundational `aethra:*` command dispatch exists with keyboard runtime binding support and A-B loop essentials.
- Done: initial Preferences expansion exists (video presets, shader/script bootstrap surfaces, portable-config import).
- Done: foundational persistence exists (last media, position, volume, window geometry).
- Done: Phase 2 command/input architecture is complete with native command routing, configurable keyboard/mouse Controls UX, and persisted bindings.
- In progress: reliability hardening is adding automated command/input regression coverage in CI and closing remaining manual shell smoke gaps.
- Next: Phase 3 typed profile parity and deeper Preferences consistency across major categories.

## Phase 1 - Shell correctness and playback reliability

Goal: make the current player shell consistently native-feeling and production-stable before expanding major surface area.

In scope:
- Harden fullscreen behavior (monitor coverage, restore logic, overlays, no renderer fallback regressions).
- Finalize transport interactions and visual coherence with modern Fluent expectations.
- Ensure cursor/controls reveal and hide behavior is predictable across playback states.
- Keep BOSS KEY and Preferences toggle paths instant and reliable.

Out of scope:
- Mini-player/PiP (deferred until post-Phase 1 shell stability work; prioritize in later UX/integration phases).
- New extension ecosystems.

Dependencies:
- Existing GPU path and command foundations.

Exit criteria:
- Manual smoke pass succeeds for open/play/pause/seek/volume/fullscreen/Preferences across repeated cycles.
- No known shell interaction regressions tracked in worklog.

## Phase 2 - Command catalog and unified input runtime

Goal: complete a single native command/input architecture that is fast, customizable, and resilient.

In scope:
- Expand `Commands/` coverage for first-party behaviors: Preferences, fullscreen, playback core, volume/mute, A-B loop, favorites, HUD, playlist/library entry points, window actions.
- Complete runtime input mapping for keyboard plus mouse buttons (including extended buttons).
- Build Controls workflows: capture-next-input, conflict detection, duplicate warning, clear/reset, section reset.
- Keep mpv `input.conf` import as migration support only.
- Introduce durable binding persistence in Aethra-native model/store and optional export path.

Out of scope:
- Gamepad/touch/pen as first-class defaults (can remain deferred).

Dependencies:
- Phase 1 stability.

Exit criteria:
- App-owned actions route through `aethra:*` command IDs by default.
- Input hot path remains in-memory and allocation-light with no disk/script dependency.
- Controls page supports real capture/edit/reset workflows for keyboard and mouse bindings.

## Phase 3 - Preferences depth and typed profiles parity

Goal: make Preferences the authoritative persistent configuration surface with typed, understandable models.

In scope:
- Complete page model: Playback, Video, Audio, Subtitles, Controls, Library, Shaders, Profiles, Network, Customization, Advanced.
- Expand typed profile coverage for playback/video/audio/subtitles/network/advanced options.
- Route preference application through backend profile APIs, not scattered UI `set` calls.
- Add per-page reset-to-defaults and clear applied/pending states.
- Preserve import/export and round-trip compatibility without making raw text primary UX.

Out of scope:
- New major media features not tied to profile/config parity.

Dependencies:
- Phase 2 command/input architecture.

Exit criteria:
- Major preference categories are backed by typed models and consistent apply/reset flows.
- Controls, Preferences, and Adjustments terminology is consistent in UI copy and docs.

## Phase 4 - Customization-first modern UX

Goal: deliver modern personalization depth while preserving native simplicity for default users.

In scope:
- Build first-class Customization page: theme, accent, transport layout, OSD behavior, density.
- Ensure progressive disclosure (simple defaults first, expert controls discoverable).
- Add Preferences search/filter with stable results across pages.
- Align panel layouts, spacing, and interaction affordances with Fluent modern baseline.

Out of scope:
- Replacing core playback architecture.

Dependencies:
- Phase 3 Preferences/page framework.

Exit criteria:
- Customization settings are persistent, discoverable, and coherent across sessions.
- Preferences remains approachable for defaults and powerful for advanced users.

## Phase 5 - Shaders and scripts extension layer (optional-first)

Goal: support advanced extensibility without making first-party behavior depend on scripts.

In scope:
- First-class shader management: folders, chains, ordering, presets, per-profile application.
- Script extension management: enable/disable, script folder configuration, visibility of active extensions.
- Keep first-party features implemented in native commands/services.

Out of scope:
- Shipping mandatory first-party Lua script dependencies.

Dependencies:
- Phase 3 typed profile plumbing and Phase 4 Preferences UX.

Exit criteria:
- Shader workflows are complete in Preferences.
- Script management is optional and does not gate core app behavior.

## Phase 6 - Windows-native integrations

Goal: make Aethra feel like a first-class Windows media app in system-level interactions.

In scope:
- SMTC media key and lock-screen control support.
- Prevent-sleep during active playback.
- File associations and "Open with" support.
- Taskbar/jump-list/media-session integration polish.

Out of scope:
- Broad installer/distribution automation details beyond reviewed scope.

Dependencies:
- Phases 1-3 for stable playback/state/commands.

Exit criteria:
- Core Windows integration features work consistently and are documented.

## Phase 7 - Persistence and state parity completion

Goal: complete session continuity and watch-later parity expected from a serious player.

In scope:
- Expand persistence for monitor-aware window state and playback session details.
- Add durable state for last folder, recent media context, and per-file progress refinements.
- Ensure persistence interactions remain fast and do not pollute hot input paths.

Out of scope:
- Telemetry-based behavior tracking.

Dependencies:
- Phase 3 settings model and Phase 6 integration hooks.

Exit criteria:
- Restart/restore flows feel reliable for common daily usage.
- Persistence behavior is predictable and testable across reopen scenarios.

## Phase 8 - Native runtime and rendering hardening

Goal: harden native media/runtime layer for sustained reliability and quality targets.

In scope:
- Keep GPU path primary with software fallback limited to diagnostics/recovery.
- Validate long-playback stability, resize/fullscreen transitions, and multi-monitor transitions.
- Keep runtime rooted in `NativeRuntime\\x64` and avoid prototype binary drift.
- Maintain reproducible native build provenance and clear third-party notices per runtime updates.
- Continue quality-bar improvements (HDR/tone mapping/scalers/sync/audio passthrough) as native capabilities mature.

Out of scope:
- Non-x64 runtime shipping commitments before explicit decision.

Dependencies:
- Phases 1-3 functional stability.

Exit criteria:
- No known blocking runtime instability in long-form manual playback tests.
- Third-party notices and native build provenance remain current for shipped runtime bundle.

## Phase 9 - Release and repo hardening

Goal: make execution and release flow dependable for contributors and public users.

In scope:
- Keep README/CONTRIBUTING/SECURITY/CODE_OF_CONDUCT/CHANGELOG aligned with implementation.
- Expand CI from build-only toward smoke and release checks in reviewed increments.
- Add issue templates when triage/review volume warrants them.
- Keep roadmap and worklog roles clear: roadmap for execution order, worklog for historical progress.

Out of scope:
- Force-fitting process overhead before the team needs it.

Dependencies:
- Ongoing across all phases.

Exit criteria:
- Contributor docs match real workflow and architecture.
- CI meaningfully protects against regressions beyond compile-only checks.

## Explicit Deferrals (Non-V1 Unless Re-prioritized)

- DRM streaming service support (Netflix/Disney+/etc.).
- Screen recording/capture and editing/transcoding pipelines.
- Mandatory script-based first-party features.
- Non-x64 production runtime support before dedicated native/runtime work.

## Operating Rules During Execution

- Keep one reviewed step at a time with `dotnet build .\\Aethra.slnx -p:Platform=x64`.
- Update `docs/development/worklog.md` after each completed step.
- Keep command/input hot path in-memory, native, and non-blocking.
