# Aethra Copilot Instructions

Start here before making changes.

## First Steps

1. Read `COPILOT_WORKLOG.md`.
2. Confirm the current focus, completed work, and next items from the worklog.
3. Use the `Roadmap` section in `COPILOT_WORKLOG.md` as the current execution order unless the user explicitly redirects.
4. Plan the requested change as a numbered list before editing files.
5. Make one small step only.
6. Run `dotnet build .\Aethra.slnx -p:Platform=x64`.
7. Fix warnings and errors before stopping.
8. Update `COPILOT_WORKLOG.md` with what changed, what was verified, and what remains next.
9. Stop for review.

## Product Direction

Aethra is a ground-up native Windows 11 media player: bright media, pure playback, and clarity in every frame. It should feel clean, modern, comprehensive, and intuitive without carrying over architecture from existing players. It is published as free, open-source software on GitHub under GPL-3.0-or-later, and aims for the highest practical quality on local files, network streams, and online video — comparable to or better than other mpv-based players on Windows, with the polish of a first-party Windows app.

Core principles: free, easy, and the best of everything. C# runs only the UI and orchestration; all heavy media work happens in native libraries, so feature breadth does not trade off against UI snappiness.

## Project Rules

- Language: C#.
- Target framework: .NET 10 (latest stable). The csproj should target `net10.0-windows10.0.19041.0`; the existing `net8.0-...` target is a leftover and should be migrated in a reviewed step.
- UI: WinUI 3 with the latest stable Windows App SDK.
- App model: unpackaged.
- Target: Windows-only, x64 first.
- Repo license: GPL-3.0-or-later.
- Do not use WPF, WinForms, UWP, MAUI, Electron, or web views for the player UI.
- Do not add NuGet packages or native dependencies without explicit approval; the open-source distribution rules below describe what's allowed.
- Do not refactor unrelated code in the same step.
- Build from the ground up, replacing temporary prototype code with clean native pieces one reviewed step at a time.
- Native by default. For every feature request, first ask: "What is the cleanest native Aethra way to do this?" Even when the user gives an example from mpv, an mpv config, an mpv script, or another player, treat it as product intent to translate into native C#/WinUI/app architecture unless the user explicitly asks for mpv-compatible behavior.

## Architecture Rules

- One main WinUI window only.
- All player UI lives in the main WinUI visual tree: settings, HUD, progress, context menus, overlays, and command surfaces.
- No separate overlay windows for controls.
- No `wid`/child-HWND mpv embedding.
- No player UI in web views.
- WinUI handles input first; UI commands dispatch to the media backend.
- libmpv is the media engine, not the UI host.
- Use the mpv render API path for video rendering.
- Prefer app-owned PInvoke/native interop for libmpv integration.
- Do not build around mpv-dotnet or another player wrapper. Existing wrapper usage is temporary and should be removed in small reviewed steps.
- Keep mpv options, shaders, tone mapping, and playback profiles behind backend profile APIs.
- Core Aethra behavior must be native app code. App-owned features such as Boss Key, A-B loop workflow, favorites, HUD, settings, window control, and future library/clip workflows should be implemented as C#/WinUI/app-service commands, not as required mpv/Lua scripts or copied mpv config patterns.
- Use `aethra:*` command names for app-owned actions. Raw mpv commands remain valid for playback operations that mpv natively owns, such as `seek 5`, `cycle pause`, or `add volume 2`.
- mpv/Lua scripts are optional user/community extensions only. Do not make first-party Aethra functionality depend on scripts.
- Win32 interop is allowed for HWND subclassing of the main window when WinUI input gaps require it. Route interop through named helpers, not ad hoc code paths.

## Organization Architecture

This organization is the current target shape, not a rigid forever-plan. Keep it clean and native, but stay willing to adjust the folder/module boundaries when new product needs reveal a better structure. The goal is always the absolute best organization for Aethra, not blind adherence to an early layout.

- `Commands/`: native `aethra:*` app commands and dispatch. This is where first-party behavior goes when it would have been an mpv script, such as BOSS KEY, A-B loop workflow, favorites, HUD actions, settings commands, window commands, and future library/clip workflows. Keep command execution synchronous and allocation-light on the hot input path.
- `Input/`: binding models, default binding catalog, input gesture parsing/capture, conflict detection, and the runtime input-binding service. This is where behavior that would have lived in `input.conf` should be represented natively first.
- `Configuration/`: load/save for user settings, portable mode, round-trip import/export, and future `%APPDATA%\Aethra\` state. Disk IO belongs here, not in the live input path.
- `Profiles/`: user-facing playback, video, audio, subtitle, shader, and advanced profile models. This is where behavior that would have lived in `mpv.conf` should become approachable Aethra settings first.
- `Player/` or `Native/`: media backend abstractions and native interop. libmpv remains the engine; Aethra owns the app behavior and UI.
- `Settings/`: WinUI settings surfaces and view models as they are introduced. Settings edits should update models/stores; runtime input should use already-loaded in-memory state.
- `MainWindow`: shell only. It may host the video surface, title bar, settings host, and window-specific operations such as minimize/maximize, but it should not become the long-term home for command lists, script-like behavior, binding persistence, or profile logic.

Hot-path rule: input must stay snappy. Native input should flow through in-memory binding lookup and command dispatch without disk reads, script calls, blocking waits, or settings parsing.

## Quality Bar

Aethra aims for "best of everything" on Windows. Concrete targets the build and UI must support:

- HDR10 and HDR10+ passthrough; HDR-to-SDR tone mapping via libplacebo when the display is SDR.
- Dolby Vision passthrough where the display chain supports it (profile 5/8). Treat libdovi as deferred until licensing/build implications are resolved.
- 10-bit and 12-bit video pipelines end-to-end where the display permits.
- High-quality scalers (ewa_lanczossharp, FSRCNNX, anime4k via user shaders) and motion interpolation through libplacebo and the mpv render path.
- Frame-perfect display sync: match display refresh, support display-resampling, and minimize judder on 60/120/144/240 Hz panels.
- WASAPI shared and exclusive output; bit-perfect path with format negotiation.
- Surround passthrough (DTS, DTS-HD, Dolby Digital, TrueHD, E-AC-3, Atmos, DTS:X) on capable HDMI/receiver chains.
- libass-quality subtitle rendering with SSA/ASS, SRT, embedded, external, and online subtitle sources.

C# overhead on these features is effectively zero. The managed thread orchestrates; native code does the work.

## Playback Scope

Aethra explicitly supports:

- Local files in any container/codec FFmpeg handles (MKV, MP4, WebM, AVI, MOV, TS, M2TS, FLAC, ALAC, WAV, DSD, etc.).
- Network streams: HTTP(S), HLS, DASH, RTSP, RTMP, SMB.
- yt-dlp integration for online video sites. Bundle or auto-fetch `yt-dlp.exe` and pipe URLs through it. Keep the integration toggleable in settings.
- Lua scripts and user shaders. Build mpv with `-Dlua=enabled`, expose `~~/scripts/` and `~~/shaders/` folders under the app's user data directory, and surface those paths in the settings UI. Ship no scripts by default; users opt in.
- DVD and Blu-ray playback through libdvdnav and libbluray. AACS/BD+ keys are not shipped; users supply their own per local law.

Out of scope unless explicitly requested: DRM-protected streaming services (Netflix, Disney+, etc.), screen capture/recording, video editing, transcoding pipelines.

## Open Source Distribution

- Aethra is free software, distributed on GitHub under GPL-3.0-or-later.
- GPL-licensed native dependencies are allowed and encouraged when they materially improve playback, rendering, codec support, or shader support. Build mpv with `-Dgpl=true` and FFmpeg with `--enable-gpl` to maximize codec/filter coverage.
- Do not use FFmpeg `--enable-nonfree`, opaque third-party redistributables, or native binaries with unclear provenance without explicit owner approval.
- Build or source libmpv/mpv, FFmpeg, libplacebo, libass, libdvdnav, libbluray, ANGLE, and related native binaries from official or well-known reproducible sources.
- Prefer the highest-quality mpv render API path available for WinUI: mpv + libplacebo GPU rendering, currently proven through OpenGL via ANGLE on this machine.
- Keep native media/rendering libraries as separate DLLs and preserve their original names.
- Preserve license text, source links, exact versions/commits, configure/build commands, and notices for all redistributed native binaries.
- For ANGLE, include the matching license/notice text with the binaries.
- Update `ThirdPartyNotices\THIRD_PARTY_NOTICES.md` whenever a native dependency binary is added, replaced, or removed.
- No telemetry, analytics, or remote logging of any kind. The app must be auditable and run fully offline.

## UI Direction

- Dark mode first; light mode follows the system theme.
- Fluent design, Mica, rounded corners, system accent color.
- Use the basic feel of the modern native Windows Media Player as a visual reference: quiet, native, clean, media-first.
- Aethra must go much deeper on configurability than Windows Media Player.
- Minimal chrome with custom title bar.
- Keep UI responsive and never block the dispatcher thread.
- Native Windows integrations are first-class: System Media Transport Controls (SMTC) for media keys and lock-screen controls, jump list entries, taskbar thumbnail buttons, file association registration, "Open with" support, prevent-sleep during playback, and per-monitor remembered window position/size/state.
- Mini-player / picture-in-picture mode for keeping playback visible while working.
- Multi-monitor aware: fullscreen on the active monitor, persist per-monitor preferences when reasonable.
- Settings should be comprehensive but approachable, grouped into clear areas: Playback, Video, Audio, Subtitles, Input, Library, Shaders, Profiles, Network, Advanced (raw mpv).
- Progressive disclosure: simple defaults up front, expert controls available without making the app feel like a spreadsheet.
- Settings has search/filter, per-pane reset-to-defaults, exportable/importable profiles, and an Advanced pane that accepts raw mpv property strings for power users.
- Window/state persistence: last position, last volume, last folder, last open file, watch-progress per file.

## Input

- Keyboard and mouse are co-equal first-class input surfaces. Many users (including the project owner, who plays on a 12-button Corsair Scimitar) drive primarily by mouse with custom button bindings, so mouse-button-as-binding-target is a core feature, not an afterthought.
- Bindings are user-configurable for keyboard keys, mouse buttons (including extended buttons up to MOUSE_BTN20+ as mpv defines them), scroll axes, and modifier combinations.
- Provide an in-app binding editor that captures the next input chord and maps it to a player command. No need to hand-edit text files for common cases, though raw `input.conf` editing remains available for power users.
- Sensible defaults out of the box (space = BOSS KEY, arrows = seek, F = fullscreen, M = mute, scroll = volume, double-click = fullscreen, middle-click = play/pause). BOSS KEY is written in all caps and means pause playback and minimize the app.
- Touch, pen, and gamepad/HID remote support are deferred until the GPU path is proven, then added in small reviewed steps. Do not block the architecture from accepting them.
- Right-click, hotkeys, focus, and settings behavior must work through normal WinUI input routing.

## Configuration

- Single source of truth on disk: a user-data directory containing `mpv.conf`-equivalent settings (which mpv natively understands), `input.conf` for bindings, `scripts/`, `shaders/`, and Aethra-specific UI/profile state.
- The settings UI reads and writes the same store. Hand-edits and GUI edits round-trip without loss.
- Default location follows Windows conventions (`%APPDATA%\Aethra\`), with an opt-in portable mode that puts config beside the .exe.

## Code Style

- File-scoped namespaces.
- Nullable enabled.
- One class per file, with filename matching type.
- Use `async` and `await`; do not use `.Result` or `.Wait()`.
- Use MVVM with CommunityToolkit.Mvvm when view models are introduced.
- No regions.
- No XML documentation unless it is public API.
- Do not write comments that simply restate the code.

## Testing

- The `TempMpv` console project is the smoke harness for the native backend. Add a smoke case there when adding any new native interop or render path before wiring it into the UI.
- Smoke run: `dotnet run --project .\TempMpv\TempMpv.csproj -p:Platform=x64`.
- Unit tests (xUnit) are welcome when a piece of logic is non-trivial and managed-only. Don't add a test project just to have one.

## Repository Layout (GitHub-ready)

The "no broad scaffolding" moratorium is lifted now that Aethra targets a public GitHub release. The following may be added in small reviewed steps:

- `README.md` (project overview, screenshots, install, build).
- `LICENSE` (GPL-3.0-or-later text).
- `CONTRIBUTING.md`.
- `CODE_OF_CONDUCT.md`.
- `SECURITY.md`.
- `.github/ISSUE_TEMPLATE/` and `.github/PULL_REQUEST_TEMPLATE.md`.
- `.github/workflows/` for CI: build, smoke, and release artifact packaging.
- `CHANGELOG.md` (Keep a Changelog format; releases follow SemVer).
- `ThirdPartyNotices/THIRD_PARTY_NOTICES.md` (already present).

Update mechanism, code-signing, and release pipeline are open questions to revisit when approaching the first public release.

## Build Commands

Run from the solution folder:

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
```

Native smoke harness:

```powershell
dotnet run --project .\TempMpv\TempMpv.csproj -p:Platform=x64
```

## Worklog Requirement

Always keep `COPILOT_WORKLOG.md` current. Add a concise summary after each completed step:

- What changed.
- Which files were touched.
- Build result.
- What is done.
- What still needs review or follow-up.
