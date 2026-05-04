# Aethra Architectural Review — 2026-05

Author: senior-engineer onboarding pass
Scope: `src/Aethra` (~12.9k LOC of C#) + `tests/Aethra.Tests` (~1.7k LOC), as of `main` at `9741e76`.
Mandate: map the system, identify structural problems, duplicated code, performance bottlenecks, maintainability risks; propose a refactor plan that preserves functionality.

This document is a planning artifact, not a directive. Where it conflicts with `docs/project/DIRECTION.md`, treat DIRECTION as canon and adjust this plan.

---

## 1. What Aethra is, in one paragraph

Aethra is a native Windows 10/11 video player. The shell is WinUI 3 on Windows App SDK (`net10.0-windows10.0.19041.0`, x64-only); the media engine is libmpv (`Endpne.LibMPV.Windows` 0.41.0) plus a hand-rolled native interop layer. The visible video surface is a `SwapChainPanel`; the production GPU path runs mpv's render API into an app-owned D3D11 swap chain bridged to OpenGL ES via ANGLE, with a software (`WriteableBitmap`) fallback. There is no backend service, no database, no public HTTP surface, no IPC — Aethra is a single-process desktop app whose "public surface" is (a) the `aethra:*` command IDs consumed by input bindings, (b) imported `mpv.conf` / `input.conf` files, and (c) JSON files persisted under `LocalAppData\<package>\LocalState\`.

The repo has 30+ bundled native runtime DLLs in `src/Aethra/NativeRuntime/x64/` (mpv + ffmpeg + libass + freetype + ANGLE + libplacebo) — the LGPL-first "public binary" obligations live in `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md`.

---

## 2. System architecture (current, as built)

### 2.1 Layer view

```
                    ┌──────────────────────────────────────┐
  XAML files        │  Views/MainWindow.xaml  (982 lines)  │
  in Views/         │  Views/FullSettingsPanel.xaml (1342) │
                    │  Views/VideoAdjustmentsPanel.xaml    │
                    └──────────────────────────────────────┘
                                     │ (partial-class glue)
                                     ▼
   ┌──────────────────────────────────────────────────────────────┐
   │  CODE-BEHIND (acts as the entire app: VM + controller +      │
   │  service composition root + native lifetime owner)           │
   │   • Views/MainWindow.xaml.cs            3,153 LOC, ~244 mthds│
   │   • Preferences/FullSettingsPanel.xaml.cs  1,957 LOC, ~125 m │
   │   • Views/VideoAdjustmentsPanel.xaml.cs    210 LOC           │
   └──────────────────────────────────────────────────────────────┘
        │            │             │              │             │
        ▼            ▼             ▼              ▼             ▼
   ┌────────┐ ┌─────────────┐ ┌─────────┐ ┌─────────────┐ ┌────────┐
   │Commands│ │   Input     │ │Services │ │Configuration│ │ Native │
   │  3 cs  │ │   6 cs      │ │  6 cs   │ │   10 cs     │ │ 22 cs  │
   └────────┘ └─────────────┘ └─────────┘ └─────────────┘ └────────┘
        │            │             │              │             │
        └────────────┴─────────────┴──────────────┘             ▼
                            │                           ┌──────────────┐
                            ▼                           │ libmpv-2.dll │
                    ┌───────────────┐                   │ + ffmpeg/ass │
                    │ApplicationData│                   │ + libplacebo │
                    │ LocalSettings │                   │ + ANGLE      │
                    │  + JSON files │                   └──────────────┘
                    └───────────────┘
```

There is no `ViewModels/` folder, no `INotifyPropertyChanged` anywhere in the codebase (verified by grep). State lives in private fields on the page; UI elements are mutated by direct method calls. This is not a stylistic complaint — it is the structural reason `MainWindow.xaml.cs` is 3,153 lines.

### 2.2 Process / threading model

Two threads matter:

1. **Dispatcher (UI) thread** — owns all XAML, the `SwapChainPanel`, settings file I/O (today), and event subscriptions.
2. **Player loop thread** — long-running `Task` per backend (`NativeMpvOpenGlPlayer.Run`, `NativeMpvSoftwarePlayer.RunAsync`). Owns the `mpv_handle`, the ANGLE EGL context, the D3D11 swap chain back buffer, and the mpv render API. It dequeues commands enqueued by the UI thread (`ConcurrentQueue<string[]>`), pumps mpv events, and posts state changes back to the UI thread via `DispatcherQueue.TryEnqueue`.

Both backends spin their own polling loop (`WaitHandle.WaitOne(8ms)` for GPU, `await Task.Delay(15ms)` for software) and ignore the wakeup callback that mpv already provides via `SetWakeupCallback` — the callback is wired but it only sets a flag; the loop wakes on a fixed timer regardless. That is, mpv tells us "I have work for you" but we time-slice and check every 8/15 ms anyway.

### 2.3 Persistence (the analogue of DB schema)

There is no database. State is split across **two backends with no shared abstraction**, all accessed via `static` classes:

| Store                              | Backend                    | File / key root                             | Owner             |
| ---------------------------------- | -------------------------- | ------------------------------------------- | ----------------- |
| `PreferencesProfilesStore`         | JSON file                  | `LocalFolder\preferences-profiles.json`     | Configuration/    |
| `InputBindingSettingsStore`        | JSON file                  | `LocalFolder\input-bindings.json`           | Configuration/    |
| `PlaybackPersistenceStore`         | `LocalSettings.Values` KV  | `Playback.*`, `Window.*`                    | Configuration/    |
| `ScriptExtensionSettingsStore`     | `LocalSettings.Values` KV  | `Extensions.ScriptsEnabled` etc.            | Configuration/    |
| `AccentColorService`               | `LocalSettings.Values` KV  | `AccentColorHex`, `FavoriteAccentColors`    | Services/         |
| `MpvRuntimeBootstrapSettings`      | in-memory singleton, fed by importer | n/a                               | Configuration/    |
| `MpvPortableConfigImporter`        | text parser (mpv.conf, input.conf) | reads user-chosen folder            | Configuration/    |

Every store is `static` (or singleton) and reaches `ApplicationData.Current.LocalSettings.Values` / `ApplicationData.Current.LocalFolder` directly. None of them go through an `ISettingsStore`, none take a path/clock/serializer dependency, none lock around writes.

### 2.4 "API surface" for this app

The closest analogues to API endpoints are:

* **`aethra:*` command IDs** in `Commands/AethraCommandIds.cs` — 39 string constants forming a stable contract with the Input layer. These are the actions external surfaces (input bindings, future scripts, mpv config import) can reference.
* **mpv command passthrough** classified by `Input/InputCommandSupport.cs` into `NativeAlias`, `PassthroughSafe`, `Blocked`, `Invalid` — the only deny-listed verbs are `run`, `subprocess`, `script-message-to` (with a narrow allowlist of script-message-to args via aliasing).
* **Preferences/profile JSON** as schema — `PreferencesPageProfiles` and `InputBindingSetting` are the wire format users edit on disk and that import/export rely on.

### 2.5 UI architecture

The UI is composed of **three XAML surfaces** orchestrated by code-behind, with no MVVM, no DI, and no navigation framework:

* `MainWindow.xaml` — shell, transport bar, command rail, video surface, OSD, chapter slider, video context flyout. Code-behind owns ~80 private fields and ~244 methods.
* `FullSettingsPanel.xaml` — single XAML file housing **11 panels** (Playback, Video, Audio, Subtitles, Input, Library, Network, Shaders, Profiles, Customization, Advanced) toggled by a manual visibility switch in `SetActiveSection(string tag)`. Code-behind in `Preferences/` (not `Views/`).
* `VideoAdjustmentsPanel.xaml` — small, focused.

`Controls/CursorAwareGrid.cs` is the only reusable XAML control; it's 16 lines.

---

## 3. File structure overview

```
src/Aethra/                       (~12.9k LOC C#, ~2.5k LOC XAML)
├── App.xaml(.cs)                 startup
├── Aethra.csproj                 net10.0-windows10.0.19041.0, x64
├── Assets/                       MSIX scale assets
├── Commands/                     command IDs + dispatcher (3 files, 353 LOC)
├── Configuration/                disk-backed stores + import (10 files, 1285 LOC)
├── Controls/                     1 cursor helper (16 LOC)
├── Input/                        binding catalog, runtime, parser (6 files, 1059 LOC)
├── Models/                       1 record (MpvChapter)
├── Native/                       libmpv interop, ANGLE, D3D11 (23 files, 3706 LOC)
├── NativeRuntime/x64/            ~30 bundled native DLLs
├── Preferences/                  FullSettingsPanel.xaml.cs (1957 LOC) — only file
├── Profiles/                     typed preference models (6 files, 219 LOC)
├── Properties/                   launchSettings, publish profile
├── Services/                     accent, playback options, navigators (6 files, 914 LOC)
├── ThirdPartyNotices/            license/provenance for native bundle
└── Views/                        XAML + MainWindow.xaml.cs (3153 LOC), VideoAdjustmentsPanel.xaml.cs (210)
```

Two folders structurally surprise:

* `Preferences/` contains exactly one file — the code-behind for a XAML that lives in `Views/`. Same partial class, two folders.
* `Controls/` contains one 16-line type. The README treats it as a real layer; it isn't yet.

---

## 4. Findings

Findings are tagged **(P1)** must-fix-soon, **(P2)** plan-and-schedule, **(P3)** opportunistic / cleanup. Everything is described with the file path so it can be verified in seconds.

### F1. Two god-classes hold ~40% of the source `(P1)`

| File                                                | LOC   | ~methods | ~fields |
| --------------------------------------------------- | ----- | -------- | ------- |
| `src/Aethra/Views/MainWindow.xaml.cs`               | 3,153 | ~244     | ~80     |
| `src/Aethra/Preferences/FullSettingsPanel.xaml.cs`  | 1,957 | ~125     | ~14     |
| **Combined**                                        | 5,110 |          |         |

Together: **~40% of all C# in the project sits in two files**. `MainWindow` is simultaneously: native-player factory, GPU/software fallback chooser, transport bar binder, OSD timer host, chapter renderer, A/B-loop controller, repeat-mode controller, fullscreen state machine, cursor-hider, window subclass proc owner, command-context wirer, persistence reader/writer, accent-service consumer, and startup-media resolver.

This is the root cause of most of the other findings. Anything that touches "the player" today touches MainWindow.

### F2. No view-model layer; everything is private fields on the page `(P1)`

Verified: no `INotifyPropertyChanged`, no `ObservableObject`, no `ObservableProperty`, no `ICommand` use anywhere in the codebase. The README and sitemap mention `ViewModels` conceptually but no folder exists.

This is what forces F1: every state bit (paused, muted, current volume, current loop point, current chapter index, fullscreen, dragging, …) is a private field, every UI update is an imperative method call, and every test that wants to exercise that state must instantiate a `Window` (which requires the Windows App SDK runtime, which the test project mostly avoids — see F11).

### F3. Command system is a 5-place fanout with no compiler-enforced binding `(P1)`

Adding a new command today requires editing five places **in sync**:

1. `Commands/AethraCommandIds.cs` — add a `const string`.
2. `Commands/AethraCommandContext.cs` — add a constructor parameter **and** a property (currently 39 `Action` parameters, by position).
3. `Commands/AethraCommandDispatcher.cs` — add a switch case.
4. `Views/MainWindow.xaml.cs` line 180 — add an `Action` to the `new AethraCommandContext(...)` call (positional only).
5. `Views/MainWindow.xaml.cs` body — add the private method that implements the action.

The 39-positional-parameter call site at `MainWindow.xaml.cs:180-219` means swapping any two adjacent `Action`s silently misroutes commands at runtime — the compiler can't help because every parameter has the same `Action` type. The dispatcher will still report "handled = true" while invoking the wrong action.

Two existing artifacts of this fragility:

* `MainWindow.xaml.cs:186-187` passes `CloseWindowFromCommand` for **both** the `Quit` and `QuitWatchLater` slots. Both commands close via the same path, and `MainWindow_Closed` (line 229) unconditionally calls `PlaybackPersistenceStore.SaveLastMedia(...)` (gated only by the user's `RememberRecentFiles` preference at line 239). The result is that **both** commands persist resume position — the inverse of the mpv convention where `quit` discards the resume marker and only `quit-watch-later` keeps it. The test (`AethraCommandDispatcherTests.cs:50`) verifies that the dispatcher reaches the named slot, not behavior, so this divergence went unnoticed.
* `Commands/AethraCommandDispatcher.cs:95-97` aliases `AethraCommandIds.ToggleLoopFile` to `_context.CycleRepeat()` — same as the `CycleRepeat` case. The test (`AethraCommandDispatcherTests.cs:71`) locks this aliasing in. So the surface advertises two distinct commands that are operationally identical; either one of them should be retired or `ToggleLoopFile` should call a distinct loop-file-only path.

### F4. Two MPV backends duplicate ~250–300 LOC `(P2)`

`Native/NativeMpvOpenGlPlayer.cs` (431) and `Native/NativeMpvSoftwarePlayer.cs` (402) share, line-for-line or near-identically:

* The full `INativeMpvPlayerBackend` API — `LoadFile`, `TogglePause`, `Pause`, `SetProperty`(×2), `ExecuteCommand`, `Seek`, `SeekToTime`, `SeekToPercent`, `SetVolume` (~70 LOC each).
* The mpv option bootstrap (`config=no`, `idle=yes`, `vo=libmpv`, `osc=no`, observe properties 1–6).
* `HandleMpvEvent` event-routing logic for chapter, playlist count, eof, pause, time-pos, duration (~100 LOC each).
* `RefreshChapters`, `RefreshPlaylistCount`, `EnqueueCommand`, `DrainCommands`, `Queue*Changed` helpers, `ApplyRuntimeBootstrapOptions` and `TryApplyImportedOption` (~50 LOC each).

Realistic deduplication target via an abstract `MpvPlayerCore` base owning the event loop, the command queue, the property observers, and the dispatch helpers — leaving only `Run`'s render-specific body (ANGLE + present vs. WriteableBitmap blit) per backend. Estimated savings: 250–300 LOC, plus a single place to fix subtle divergences (e.g. the bootstrap options block in the GPU backend applies `gpu-api`/`gpu-context`; the software backend doesn't, intentionally — that asymmetry should remain explicit, not implicit-by-omission).

### F5. Dead native code: 580+ LOC unreferenced from production or tests `(P2)`

Verified by `grep` across `src/` and `tests/`:

| File                                              | LOC | References outside its own file | Status                  |
| ------------------------------------------------- | --- | ------------------------------- | ----------------------- |
| `Native/AngleSwapChainPanelContext.cs`            | 244 | none                            | dead — never instantiated |
| `Native/NativeMpvOpenGlSmokeRunner.cs`            | 95  | none                            | dead                    |
| `Native/NativeMpvOpenGlSmokeResult.cs`            | 12  | only by the dead runner above   | dead                    |
| `Native/NativeMpvSoftwareSmokeRunner.cs`          | 88  | none                            | dead                    |
| `Native/NativeMpvSoftwareSmokeResult.cs`          | 13  | only by the dead runner above   | dead                    |
| `Native/NativeMpvRenderApiProbe.cs`               | 155 | none in production source       | dead in production      |

The smoke runners and the API probe are referenced from `docs/development/worklog.md` (history) but not wired into either app startup or the test project. The `AngleSwapChainPanelContext` duplicates the production `AngleD3D11SwapChainContext`'s `ChooseConfig`, `CreateContext`, `TryCreateContext`, and `ThrowEglError`. Either gate the smoke/probe code behind a documented diagnostic entry point in `App.xaml.cs` (and keep it tested) or delete it. Right now it rots silently.

### F6. Persistence is fragmented across two backends with no abstraction `(P2)`

See §2.3. The risks are concrete:

* **No atomicity.** `InputBindingSettingsStore.Save` (`Configuration/InputBindingSettingsStore.cs:99-107`) and `PreferencesProfilesStore.SaveToPath` (`PreferencesProfilesStore.cs:53-58`) both call `File.WriteAllText` directly. A crash mid-write produces a truncated or zero-byte file; the `try/catch` in `Load` then swallows it and silently restores defaults — the user loses their bindings.
* **No locking.** Two near-simultaneous saves from any source race on the file handle.
* **Inconsistent fallback semantics.** `PreferencesProfilesStore.Load` catches all exceptions and returns defaults silently. `InputBindingSettingsStore.LoadWithMigration` returns a structured warning. The user's experience differs based on which file was hit by the issue.
* **Untestable in isolation.** Every store reads `ApplicationData.Current.LocalSettings.Values`. There is no `ISettingsStore` to fake. The existing tests for `PreferencesProfilesStore` and `InputBindingSettingsStore` only exercise the `internal` *path-taking* overloads (`LoadFromPath`, `SaveToPath`, `ApplyMigrationForRows`) — the public surface that production uses is uncovered.
* **`ApplicationData.Current` requires the WindowsAppRuntime to be initialized** — meaning code that touches accent or playback persistence cannot be invoked from a plain unit-test project. This is the real reason `AccentColorService` has no tests.

### F7. Singletons-as-services hide all wiring `(P2)`

`AccentColorService` (static), `PlaybackOptionsService.Instance`, `MpvRuntimeBootstrapSettings.Instance`, every `*Store`, every `*Importer` — all global. `MainWindow.xaml.cs:140` and `FullSettingsPanel.xaml.cs:40` both reach into `PlaybackOptionsService.Instance` directly. There is no composition root other than the `MainWindow` constructor.

Practical consequences:

* Any new feature that needs "the playback options" reaches into a global. Over time every class becomes coupled to every singleton.
* Threading semantics are undocumented per service. `AccentColorService.AccentColorChanged` fires from whichever thread called `TryApplyHex` — usually the UI thread, but the contract isn't explicit.
* Tests that need to substitute behavior (e.g. "a fake playback options that records `Apply*` calls") have no seam.

### F8. Polling loops and per-frame allocations `(P2)`

* **GPU backend** — `NativeMpvOpenGlPlayer.Run` (`Native/NativeMpvOpenGlPlayer.cs:188-200`) wakes every 8 ms via `WaitHandle.WaitOne` regardless of whether mpv has work. The wakeup callback (`context.SetWakeupCallback`) is registered but only flips a flag — there is no `ManualResetEventSlim` the loop blocks on. Net: 125 wakeups/sec at idle.
* **Software backend** — `NativeMpvSoftwarePlayer.RunAsync` (`Native/NativeMpvSoftwarePlayer.cs:162-174`) sleeps 15 ms in a similar loop, **and** `CopyFrame` (line 364) allocates a `byte[640*360*4]` ≈ **922 KB** on **every frame**. At 30 fps that's ~27 MB/s of GC traffic on the player thread, every byte of which is throwaway after the marshal copy. Buffer pooling (or a `Span<byte>` against a single reusable array) is straightforward.
* **Settings writes on UI thread** — `PlaybackPersistenceStore.SaveWindow` writes 4 keys to `LocalSettings.Values` synchronously. If wired to `SizeChanged`/`PositionChanged` it churns on every drag pixel; debouncing or write-on-close is the standard fix.

### F9. Developer-machine path baked into shipping code `(P1)`

`src/Aethra/Views/MainWindow.xaml.cs:121`:

```csharp
private const string PreferredStartupMediaPath = @"C:\Users\rjh\Videos\test.mp4";
```

Used at line 2977 as the **preferred** startup target — it's checked before the user's persisted "last media" path. On the developer's box this overrides legitimate persistence behavior; on every other box it silently falls through. It also embeds the developer's username in the binary, which is a privacy / professionalism issue regardless of behavior.

Replace with: an opt-in dev override fed by env var (the file already uses the same idiom for `AETHRA_GPU_SURFACE_SMOKE` at line 58), or — better — by a debug-only `launchSettings.json` argument. Production code should not contain user-specific paths.

### F10. View/code-behind split for FullSettingsPanel is non-standard `(P3)`

`Views/FullSettingsPanel.xaml` and `Preferences/FullSettingsPanel.xaml.cs` are partial classes for the same type but live in different folders. The csproj has explicit `<Page Remove>` / `<Page Include>` for the XAML and `<None Update>` for the code-behind to keep this glued together. WinUI / Visual Studio expect both halves to live next to each other; tooling like "Go to definition on a XAML element" still works but the split confuses navigation, makes "find all references on the XAML name" awkward, and is the kind of thing that breaks an upgrade to a future SDK.

The `SetActiveSection` body (`Preferences/FullSettingsPanel.xaml.cs:97-110`) is a hand-rolled visibility-toggle for 11 sections. WinUI's `Frame` + 11 separate `Page` types would replace this with first-class navigation, allow each section to have its own (small) view-model and its own (small) tests, and let the panels be `Lazy<T>`-instantiated so the cost of opening Preferences is paid once per section, not every time.

### F11. Test coverage shape blocks safe refactor `(P1, prerequisite for everything else)`

Test breakdown (15 files, 1,707 LOC):

| Surface       | Tests | Notes                                                                      |
| ------------- | ----- | -------------------------------------------------------------------------- |
| Commands      | 1     | Verifies dispatcher routes to the *named* context Action — not the *behavior* of the action. |
| Configuration | 4     | All exercise `internal` path/row overloads; production `Load`/`Save` paths uncovered. |
| Input         | 5     | Best-tested area — catalog, runtime, parser, mpv command-line parser, click tracker. |
| Profiles      | 1     |                                                                            |
| Services      | 3     | `PlaybackOptionsService` (368 LOC of tests), `FolderMediaNavigator`, `PlaybackMetadataFormatter`. |
| Views         | 1     | 105 LOC. Covers 3 `internal static` helpers extracted from a 3,153-line file. The remaining 99% of `MainWindow` is unverified. |

There are no tests for `Native/*` (interop), `AccentColorService`, `PlaybackPersistenceStore`, `ScriptExtensionSettingsStore`, or `App.xaml.cs`. The Native gap is partly justified — those types require a real `mpv_handle` and an EGL display — but the gap means any refactor of `MpvPlayerCore` (F4) lands without a safety net.

### F12. Documentation tree has two parallel roots `(P3)`

Top-level `Aethra/` folder still ships `README.md`, `COPILOT_INSTRUCTIONS.md`, `COPILOT_WORKLOG.md`, `ROADMAP.md`. The new `docs/architecture/agent-sitemap.md:143` explicitly disclaims them as "redirect-only compatibility surface" but doesn't prune them. Two roots → drift risk every time someone updates "the docs". Either delete the four redirect-only files (after confirming no external link relies on them) or replace each with a one-line stub that points at the canonical doc and nothing else.

### F13. Smaller smells worth noting `(P3)`

* `InputCommandSupport.cs` — `script-message-to` is in `DeniedCommandVerbs` (line 36) and simultaneously matched as a `NativeAlias` for two specific argument tuples (lines 203-218). The deny-list is only consulted *after* the alias check (line 100-110), so the two-step "deny, allowlist via alias" works — but it isn't documented anywhere. A code comment near the deny set saying "aliases override; see TryGetNativeAlias" would save the next reader a trip through the dispatcher.
* `AethraCommandContext` ends each property with two blank lines (`Commands/AethraCommandContext.cs:89-165`) — 39 properties × 3 lines each ≈ a 78-line block of effectively no information. Switching to `record` or to expression-bodied get-only auto-properties shrinks the file dramatically and makes adding a parameter a one-line change instead of a four-line change.
* `MainWindow.xaml.cs:21-23` carries a non-trivial comment about a `using` alias for `Rectangle` due to a `Path` collision with `System.IO.Path`. That kind of shadowing is fine, but it's a tell that this file is doing too many *kinds* of things at once (drawing shapes, walking the filesystem, parsing chapter timestamps). Split apart, the alias goes away.

---

## 5. Refactor plan (sequenced, functionality-preserving)

The guiding principle: **invest in test seams first**, then collapse duplication, then break up the god-classes. Every step ships green and shippable.

### Phase A — Safety net & quick wins (1–2 weeks)

A1. **Delete or gate the dead native code.** `AngleSwapChainPanelContext`, `NativeMpvOpenGlSmokeRunner`(+result), `NativeMpvSoftwareSmokeRunner`(+result), `NativeMpvRenderApiProbe`. If they're kept, wire them behind an `AETHRA_NATIVE_DIAGNOSTICS=1` env-var entry point in `App.xaml.cs` and add at least one `Aethra.Tests` invocation per runner. (~580 LOC removed or actually used.)

A2. **Remove the personal-path constant** (F9). Replace `PreferredStartupMediaPath` with reads of `AETHRA_STARTUP_MEDIA` env var (debug-only), keep `ResolveStartupMediaCandidate`'s existing fallback to persisted path.

A3. **Make file persistence atomic.** Helper: `WriteAllTextAtomic(path, json)` → write to `path + ".tmp"`, fsync, `File.Move(tmp, path, overwrite: true)`. Apply to `PreferencesProfilesStore.SaveToPath` and `InputBindingSettingsStore.Save`. No public API change. Add a unit test that interrupts mid-write (kill the file handle) and verifies the destination is either the old contents or the new — never partial.

A4. **Fix the `Quit` / `QuitWatchLater` aliasing** (F3). `MainWindow_Closed` already saves position unconditionally; the actual divergence from mpv convention is that `aethra:quit` *also* saves when it should discard. Add a `_suppressResumePersistenceOnClose` flag set by a new `QuitDiscardingResumeFromCommand` (the new `quit` action), and have `MainWindow_Closed` honor the flag by calling `PlaybackPersistenceStore.ClearLastMedia()` instead of `SaveLastMedia(...)`. `quitWatchLater` keeps the existing `CloseWindowFromCommand` path. Document the behavior change explicitly in the worklog.

A5. **Decide `ToggleLoopFile` vs `CycleRepeat`** (F3). Either retire the const and update tests/bindings, or implement a distinct loop-file-only handler. Today the second const is API noise.

A6. **Pool the software-backend frame buffer** (F8). Add `private byte[]? _frameScratch;` sized once to `Width*Height*4` and reuse it in `CopyFrame` instead of `new byte[…]` per frame.

### Phase B — Test seams (1–2 weeks)

B1. **Introduce `ISettingsStore` and `IFileStore` interfaces** (F6/F7). Implementations: `LocalSettingsStore : ISettingsStore` (delegates to `ApplicationData.Current.LocalSettings.Values`) and `JsonFileStore<T> : IFileStore<T>` (atomic writes, structured warnings on read failure). Existing static stores keep their public API but delegate to a `static readonly` instance; tests can substitute via `internal static SetForTesting(...)`. No production behavior change.

B2. **Introduce `IPlaybackOptions` and `IAccentColors` interfaces** for the singletons that view code consumes. The singletons remain (they're referenced from XAML-bound code paths today), but the consumers (`MainWindow`, `FullSettingsPanel`) take constructor parameters. Today both XAML pages have parameterless ctors; the workaround is to introduce an explicit `Initialize(...)` method on the page invoked by the host before `Activate()`, and route everything else through fields set in `Initialize`. This pattern is XAML-friendly and unblocks B3.

B3. **Replace the `AethraCommandContext` 38-positional ctor with a registry** (F3). New shape:

```csharp
internal sealed class AethraCommandRegistry
{
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.Ordinal);
    public void Register(string id, Action handler) => _handlers[id] = handler;
    public bool TryExecute(string id) { … }
}
```

`MainWindow` now wires by name: `_commands.Register(AethraCommandIds.SeekBack5, () => SeekRelative(-5));` — adding a command is two places (the const + the registration), the compiler still type-checks the action, swapping registration order is harmless because the binding is by string. The dispatcher class collapses to ~20 lines. Update `AethraCommandDispatcherTests` to register fakes by ID and assert behavior.

### Phase C — Native consolidation (1 week)

C1. **Extract `MpvPlayerCore`** (F4). Abstract base owning the command queue, the option/observe-property bootstrap (with virtual hook for backend-specific extras like `gpu-api`), the event-handling switch, and the `Queue*Changed` helpers. `NativeMpvOpenGlPlayer` and `NativeMpvSoftwarePlayer` shrink to render-specific code only (~80–100 LOC each). Estimated savings: 250–300 LOC, single place for property-observe-id changes.

C2. **Switch the player loops to wakeup-driven** (F8). Replace `WaitHandle.WaitOne(8)` / `await Task.Delay(15)` with an `AsyncManualResetEvent` (or `ManualResetEventSlim`) signaled by the wakeup callback and by `EnqueueCommand`. Cap with a max idle interval (e.g. 250 ms) for safety. Significantly reduces idle wakeups.

C3. **Define a documented disposal order.** Add an XML doc on `INativeMpvPlayerBackend.Dispose`: "Cancels and waits for the player loop, then releases native resources." Today `NativeMpvOpenGlPlayer.Dispose` returns immediately and the dispose chain runs as a fire-and-forget continuation — race window where `_dispatcherQueue.TryEnqueue(..._failed(ex))` can fire after the window is closed.

### Phase D — UI / view-model migration (3–6 weeks, incremental)

D1. **Lift transport state into `PlaybackViewModel`.** Owns `IsPlaybackPaused`, `CurrentVolume`, `IsMuted`, `RepeatMode`, `LoopPointA/B`, `Position`, `Duration`, `Chapters`. `INotifyPropertyChanged`. `MainWindow` keeps the XAML-bound surface (event handlers stay) but reads/writes the VM; the VM in turn talks to `IPlaybackOptions` and `INativeMpvPlayerBackend`. Estimated `MainWindow` LOC removal: 600–800 once XAML bindings replace direct `_*` mutations.

D2. **Lift cursor/window-chrome state into `WindowChromeViewModel`.** Owns `IsFullscreen`, `IsCommandRailExpanded`, `IsCursorVisible`, `IsTransientOsdVisible`, the timers, and the subclass-proc bookkeeping. Most of MainWindow's "what's on screen?" logic moves here.

D3. **Lift startup/persistence orchestration into `AppBootstrapper`.** `MainWindow` becomes the *view*; `App.xaml.cs` constructs the bootstrapper, which constructs the VMs, which the window receives via `Initialize(...)`. After D1-D3, `MainWindow.xaml.cs` should be in the 600–900 LOC range — primarily XAML event glue and platform-specific window code.

D4. **Convert `FullSettingsPanel` to `Frame` + 11 `Page`s** (F10). Each section gets its own XAML, code-behind (small), and view-model. The 11-way visibility switch in `SetActiveSection` is replaced by `NavView.SelectionChanged → Frame.Navigate(...)`. `FullSettingsPanel.xaml.cs` shrinks from 1,957 LOC to ~150 LOC of routing. Move both halves of each Page into `Views/Preferences/<Section>/` so the partial-class halves live together (resolves F10).

### Phase E — Polish (opportunistic)

E1. Convert `AethraCommandContext` to a `record` or to `init`-only auto-properties (F13). Trivial; do alongside B3.

E2. Add a comment block in `InputCommandSupport.cs` explaining the deny-list / alias precedence (F13).

E3. Prune `Aethra/` redirect docs (F12) — replace with one-line pointers or delete after confirming no external dependency.

E4. Move `Controls/CursorAwareGrid.cs` into `Views/Controls/` (or absorb back into MainWindow as a private nested type). One-file folders are noise.

---

## 6. What this plan deliberately preserves

* **The `aethra:*` command surface** — every const stays, every input binding keeps working. F3's refactor changes the *internal* wiring shape, not the public IDs.
* **The persistence file formats** — `preferences-profiles.json` and `input-bindings.json` keep the same schema. F6's atomic-write change is invisible to existing user files.
* **The two-backend GPU/software fallback** — F4 deduplicates the *implementation* but the backend selection logic in `MainWindow` is unchanged. Users with broken GPU paths still fall back to software.
* **The mpv.conf / input.conf import flow** — `MpvPortableConfigImporter` is untouched. The only files reused from `mpv.net` (per `MPVNET_REUSE_MAP.md`) stay structurally where reviewers expect them.
* **The "no telemetry" posture** — none of the proposed changes add network, analytics, or external services.
* **Build/run profile** — `net10.0-windows10.0.19041.0`, x64, unpackaged-first stays. No SDK upgrade, no NuGet additions implied by Phases A–C; Phase D may want a small `CommunityToolkit.Mvvm` reference for `ObservableProperty` source generation, which should be reviewed against `DIRECTION.md`'s framework policy before adopting.

---

## 7. What I'd do first, if I had one week

1. **A2** — pull the personal path. Five minutes; ships dignity.
2. **A1** — delete the dead native code (or gate it). One PR; ~580 LOC drop.
3. **A3** — atomic writes to the two JSON stores. Half a day; eliminates a class of "lost user settings" bugs.
4. **A4 + A5** — fix or document the `Quit`/`QuitWatchLater` and `ToggleLoopFile`/`CycleRepeat` aliasing. Half a day; restores the integrity of the `aethra:*` surface.
5. **B1** — `ISettingsStore` / `IFileStore` seams. Two days; unblocks every later test.

That sequence touches no XAML, no native interop, no public API, and lands the foundation for everything in §5.
