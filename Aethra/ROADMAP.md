# Aethra Roadmap

The GPU path is proven under core use, BOSS KEY feels instant, and the next phase is making the app feel intentional instead of prototype-shaped. Preserve the hot input path: live keyboard/mouse handling must stay native, synchronous, and already-loaded in memory.

## 1. Player UI polish and shell correctness

- Fix the central play/pause button visual so it reads as a polished native media control.
- Hide the mouse pointer after a short idle delay over the video surface, and show it immediately on movement.
- Continue hardening true fullscreen behavior: validate monitor coverage, restore behavior, control reveal, and no renderer fallback.
- Revisit the transport shelf spacing, icons, disabled states, hover states, and bottom action cluster so the player feels close to modern Windows Media Player but better suited to Aethra.
- Keep BOSS KEY and `S` preferences toggling as snappy reference interactions.

## 2. Command and input architecture

- Expand `Commands/` beyond BOSS KEY with native app command IDs and dispatcher cases for preferences, fullscreen, play/pause, seek, volume, mute, A-B loop, favorites, HUD, and future clip/library workflows.
- Add an `Input/` runtime service that maps WinUI keyboard/mouse gestures to already-loaded command IDs without disk IO, script calls, or preference parsing.
- Move default binding models/catalog into the `Input/` area.
- Add keyboard capture in the Controls page of Preferences first.
- Add mouse-button capture after keyboard capture works, including Scimitar-class extended buttons.
- Add conflict detection, duplicate gesture warnings, clear/reset per binding, and per-section reset.
- Add import from existing mpv `input.conf` as a migration helper, not as the runtime architecture.
- Add export/save to Aethra's future `%APPDATA%\Aethra\input.conf` or native binding store only after the model and UI flow are approved.

## 3. Preferences UI rebuild

- Redesign Preferences as a serious native configuration surface, not the current scaffold.
- Fix the Controls page so it is easy to search, sort, capture, edit, reset, and understand what is native Aethra versus mpv passthrough.
- Split Preferences into clear pages: Playback, Video, Audio, Subtitles, Controls, Library, Shaders, Profiles, Network, Customization, Advanced.
- Add first-class quality presets in Video preferences (for example `Reference`, `Cinema`, `Anime`) backed by typed profile values, not ad hoc UI-side property writes.
- Add per-page reset-to-defaults, import/export profiles, and clear applied/pending state.
- Keep advanced controls available without making the default Preferences page feel like a spreadsheet.
- Move configuration UI code toward `Preferences/` plus view models when the UI becomes more than a simple shell.

## 4. Native replacement plan for `input.conf`

- Treat every old `input.conf` row as intent to translate into a native Aethra command or native mpv passthrough.
- Native Aethra commands should cover app behavior: BOSS KEY, preferences, fullscreen, HUD, favorites, A-B loop, playlist UI, chapter/clip workflows, screenshots UI, and window actions.
- mpv passthrough remains appropriate for engine-owned playback operations: seek, frame step, volume, track switching, subtitle delay, audio delay, speed, video filters, and raw properties.
- Store bindings in a native model first; only export/import text files for compatibility and power users.

## 5. Native replacement plan for `mpv.conf`

- Build typed Aethra profile models before exposing raw option text:
  - Playback profile: resume, loop, speed defaults, playlist behavior.
  - Video profile: hardware decode, scaling, debanding, interpolation, aspect, rotation, HDR/tone mapping.
  - Audio profile: WASAPI mode, exclusive/shared, passthrough, channel layout, audio delay.
  - Subtitle profile: default language, scale, delay, visibility, style overrides.
  - Network profile: cache, streaming options, yt-dlp behavior.
  - Advanced profile: raw mpv options for users who want exact control.
- Apply those profiles to libmpv through backend profile APIs, not scattered `set` calls across UI code.
- Add an explicit backend options/profile service boundary so new preference surfaces and overlays read/write one typed API instead of issuing direct mpv commands from multiple UI call sites.
- Keep hand-edit/import/export paths, but make the first-class UI native and understandable.

## 6. Shaders, scripts, and extension surface

- Shaders are first-class: expose shader folders, shader chains, enable/disable ordering, presets, and per-profile application in Preferences.
- Built-in shader workflows should be native Aethra preferences/profile choices.
- Lua/mpv scripts are optional user/community extensions only. Surface a scripts folder and script enable/disable management later, but do not depend on scripts for first-party Aethra features.
- For features that used to be scripts, first ask for the clean native Aethra command/service design.
- Sequence rule: complete command/input architecture and typed profile plumbing first, then add script/shader management UI on top of that foundation.

## 7. Backend and runtime cleanup

- Keep the GPU renderer as the primary path.
- Complete the visible `SwapChainPanel` GPU presentation path as the default renderer path, with software kept as a fallback only.
- Keep software fallback for now, but stop broadening it unless needed for diagnostics or recovery.
- Keep native runtime resolution fully rooted in `NativeRuntime\x64`; do not reintroduce root-level prototype native binaries.
- Rebuild and document the native media bundle for the MIT-licensed GitHub direction: prioritize reproducible, high-quality playback builds with clear third-party notices and release obligations.
- Validate true fullscreen, resize, snap, drag/drop reload, seek, long playback, and no `GPU renderer task failed` lines.

## 8. Windows integration and persistence

- Add SMTC for media keys and lock-screen controls.
- Add window/state persistence: position, size, monitor, volume, last folder, last file, watch progress.
- Add prevent-sleep during playback.
- Add file associations and "Open with" support.
- Add mini-player / picture-in-picture after the main player shell is stable.

## 9. Repo readiness

- Keep README/CONTRIBUTING/SECURITY/CODE_OF_CONDUCT/CHANGELOG current as architecture and process evolve.
- Add issue templates when triage volume warrants them; keep PR templates and CI in sync with current workflow.
- Expand CI from build-only toward smoke/release automation in reviewed increments.
- Keep `ThirdPartyNotices/THIRD_PARTY_NOTICES.md` current whenever native binaries change.
