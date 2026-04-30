# Aethra Direction Canon

This document is the single source of truth for Aethra's direction, architecture/framework boundaries, licensing posture, and guidance ownership.

When another document appears to conflict with this file, treat this file as canonical and align the other document.

## Product Mission

Aethra is a native Windows media player focused on playback quality, configurability, and transparent open-source distribution.

Core product goals:

- Native-first WinUI experience with no web-hosted player UI.
- Best practical playback quality on Windows using a native media stack.
- Fast, configurable controls and preferences with clear user-facing terminology.
- Public and auditable project operations (roadmap, worklog, third-party provenance).
- No telemetry, analytics, or remote logging.

## Non-Goals

- DRM-protected streaming service support unless explicitly re-prioritized.
- Turning first-party app behavior into required script dependencies.
- Shipping non-x64 runtime targets before explicit owner prioritization.

## Platform And Framework Baseline

- OS target posture: Windows-first, x64-first.
- UI stack: WinUI 3 on Windows App SDK.
- Language/runtime: C# on `net10.0-windows10.0.19041.0`.
- App model baseline: unpackaged-first, with optional MSIX-capable packaging when reviewed.

## Architecture Boundaries

- One main WinUI window; no separate overlay windows for core controls.
- libmpv is the media engine; WinUI is the UI host.
- Use mpv render API for video path.
- Use app-owned native interop/PInvoke for media integration.
- App-owned commands use `aethra:*`; script/config compatibility remains optional.
- Input hot path remains in-memory and non-blocking.

Folder responsibility baseline:

- `src/Aethra/Views`: shell and presentation surfaces.
- `src/Aethra/Commands`: app command IDs and dispatch behavior.
- `src/Aethra/Input`: input binding/runtime capture and conflict domain.
- `src/Aethra/Configuration`: persistence and disk-backed settings/state.
- `src/Aethra/Profiles`: typed preference/profile models.
- `src/Aethra/Preferences`: user-facing persistent preferences surfaces.
- `src/Aethra/Services`: orchestration and cross-cutting app logic.
- `src/Aethra/Native` and `src/Aethra/NativeRuntime`: interop layer and native runtime bundle.

## Terminology Canon

- `Preferences`: persistent app behavior.
- `Adjustments`: immediate playback/session tweaks.
- `Controls`: bindings and gesture mapping.
- `Customization`: appearance/chrome within Preferences.
- `Advanced`: expert/raw engine options.
- Avoid `Control Panel` and avoid `Settings` as top-level UX naming.

## Licensing And Distribution Posture

Two-layer licensing model:

1. Aethra-owned repository source is licensed as `GPL-2.0-or-later`.
2. Redistributed native runtime binaries may impose additional obligations based on actual build choices.

Default public-binary posture:

- LGPL-first native runtime distribution posture unless the owner explicitly approves a different policy.
- Keep FFmpeg nonfree-disabled for default public binaries.
- Preserve license/notice/source provenance for shipped runtime artifacts.

## Canonical Guidance Ownership

- Direction/framework/policy canon: `docs/project/DIRECTION.md` (this file).
- Repository navigation and path ownership map: `docs/architecture/agent-sitemap.md`.
- Execution sequencing and phase scope: `docs/project/roadmap.md`.
- Historical implementation record: `docs/development/worklog.md`.
- Agent implementer rules: `docs/development/copilot-instructions.md`.
- Native runtime licensing and provenance details: `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md`.
- Public project entry point: `README.md`.
- Contributor workflow and PR expectations: `CONTRIBUTING.md` and `.github/PULL_REQUEST_TEMPLATE.md`.

## Alignment Rule

When updating policy:

1. Update this canonical file first.
2. Update owning documents listed above.
3. Add a worklog entry summarizing what changed and why.
