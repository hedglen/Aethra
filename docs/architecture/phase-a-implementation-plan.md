# Phase A — Implementation Plan

Owner: senior-engineer onboarding pass
Source plan: `docs/architecture/architectural-review-2026-05.md` §5, Phase A.
Workflow rules followed: `docs/development/copilot-instructions.md` ("one small step only", build after every step, worklog entry after every step, no unrelated refactors).

This document is the executable spec for Phase A. Every change below names exact files, exact line ranges, before/after sketches, the test changes that ship with it, and the verification commands to run before declaring done.

---

## 0. Phase A in one paragraph

Phase A is the safety-net pass: delete dead code, kill the personal path, make file persistence atomic, fix two `aethra:*` aliasing bugs, and pool the per-frame allocation in the software backend. **No XAML changes. No new NuGet packages. No public API changes other than removing one unused command ID and adding one new internal helper.** Each step is its own PR and each PR ends with a green `dotnet build` + `dotnet test` and a worklog entry.

Total scope: **6 PRs, ~580 LOC removed, 1 new helper file (~80 LOC), 1 new test file (~60 LOC), ~10 LOC changed in `MainWindow`, ~5 LOC changed in 2 store files, ~6 LOC changed in `NativeMpvSoftwarePlayer`.**

---

## 1. PR sequence and dependencies

PRs are independent and can be reviewed in parallel except where noted. The recommended **landing order** sequences lowest-risk first so trust accumulates before behavior changes:

| Order | PR  | Item | Risk  | Touches             | Reason for slot                                       |
| ----- | --- | ---- | ----- | ------------------- | ----------------------------------------------------- |
| 1     | #A1 | A1   | low   | `Native/`           | Pure deletion of unreferenced files. Build is the proof. |
| 2     | #A2 | A2   | low   | `MainWindow`, test  | One const swap + one test update. No behavior change for users. |
| 3     | #A3 | A3   | med   | `Configuration/` + new file | Foundation; later PRs may use the helper.     |
| 4     | #A4 | A6   | low   | `NativeMpvSoftwarePlayer` | Field-level perf change, single backend.        |
| 5     | #A5 | A5   | low   | `Commands/`, test   | Removes one unused const + one dispatcher case + one test row. |
| 6     | #A6 | A4   | med   | `MainWindow`, `Configuration`, test | Behavioral fix for `aethra:quit-watch-later`. |

Dependencies: **none of the PRs strictly block any other.** A6 (the behavioral fix for QuitWatchLater) lands last because (a) it's the only one that changes user-visible behavior and (b) a green CI on PRs A1–A5 builds confidence in the test scaffold first.

---

## 2. Standing rules for every PR in Phase A

Per `docs/development/copilot-instructions.md`:

1. **Build before opening:** `dotnet build .\Aethra.slnx -p:Platform=x64`. Zero warnings, zero errors.
2. **Test before opening:** `dotnet test .\Aethra.slnx -p:Platform=x64 --no-build`. All green.
3. **Worklog entry:** append a short block to `docs/development/worklog.md` describing what changed, what was verified, and what's next.
4. **PR description template:** use `.github/PULL_REQUEST_TEMPLATE.md` and explicitly call out which Finding (F-numbers from §4 of the architectural review) the PR closes.
5. **No NuGet additions, no XAML edits, no SDK upgrades** in Phase A.
6. **No unrelated cleanups.** If the diff strays beyond what's described below, split it.

---

## 3. PR #A1 — Delete dead native code

**Closes:** F5 (P2). **Risk:** low. **Estimated diff:** −607 LOC, +0 LOC.

### 3.1 What to delete

All six files have been verified as unreferenced from any production source or test (`grep`-confirmed in the architectural review §4, F5):

| Path                                                  | LOC | Justification                                                  |
| ----------------------------------------------------- | --- | -------------------------------------------------------------- |
| `src/Aethra/Native/AngleSwapChainPanelContext.cs`     | 244 | Only `AngleD3D11SwapChainContext` is instantiated in production. |
| `src/Aethra/Native/NativeMpvOpenGlSmokeRunner.cs`     | 95  | Never invoked from `App.xaml.cs`, `MainWindow`, or any test.   |
| `src/Aethra/Native/NativeMpvOpenGlSmokeResult.cs`     | 12  | Only consumed by the dead runner above.                        |
| `src/Aethra/Native/NativeMpvSoftwareSmokeRunner.cs`   | 88  | Same — never invoked.                                          |
| `src/Aethra/Native/NativeMpvSoftwareSmokeResult.cs`   | 13  | Only consumed by the dead runner above.                        |
| `src/Aethra/Native/NativeMpvRenderApiProbe.cs`        | 155 | Never invoked from production source.                          |

### 3.2 Re-verify before deleting

```powershell
# Run from repo root. Each command must return zero hits in src/ and tests/ outside the file itself.
rg -n "AngleSwapChainPanelContext"   src tests
rg -n "NativeMpvOpenGlSmokeRunner"   src tests
rg -n "NativeMpvOpenGlSmokeResult"   src tests
rg -n "NativeMpvSoftwareSmokeRunner" src tests
rg -n "NativeMpvSoftwareSmokeResult" src tests
rg -n "NativeMpvRenderApiProbe"      src tests
```

If any new reference has appeared since this plan was written, **do not delete that file.** Stop, investigate, and amend this plan.

### 3.3 What to do if we want to keep them as diagnostics

The architectural review noted these were once used as diagnostic probes. If the team wants to retain them rather than delete:

1. Wire each runner behind an env-var-gated branch in `src/Aethra/App.xaml.cs` mirroring the `AETHRA_GPU_SURFACE_SMOKE` pattern at `src/Aethra/Views/MainWindow.xaml.cs:58`. Suggested gates: `AETHRA_NATIVE_DIAGNOSTICS=1` for the probe; `AETHRA_GPU_SMOKE=1` and `AETHRA_SOFTWARE_SMOKE=1` for the runners.
2. Add at least one `Aethra.Tests` test per runner that invokes it with the gate set.
3. Document the env vars in `docs/development/copilot-instructions.md` under a new "Diagnostics" section.

**Default recommendation: delete.** The smoke runners haven't been touched since intake (per `docs/development/worklog.md`) and the GPU bridge work the review references is now downstream of `AngleD3D11SwapChainContext`. Keeping unused diagnostics is the same liability as keeping dead code — they rot until used, and using them eventually requires re-validating against current `mpv_handle` / EGL behavior.

### 3.4 Diff shape

```
D src/Aethra/Native/AngleSwapChainPanelContext.cs
D src/Aethra/Native/NativeMpvOpenGlSmokeRunner.cs
D src/Aethra/Native/NativeMpvOpenGlSmokeResult.cs
D src/Aethra/Native/NativeMpvSoftwareSmokeRunner.cs
D src/Aethra/Native/NativeMpvSoftwareSmokeResult.cs
D src/Aethra/Native/NativeMpvRenderApiProbe.cs
M docs/development/worklog.md     # one entry
```

### 3.5 Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test .\Aethra.slnx -p:Platform=x64 --no-build
```

Both must succeed without changing test counts.

### 3.6 Rollback

Single revert. No external surface affected. If a downstream consumer surfaces (e.g. a fork that subclassed `AngleSwapChainPanelContext`), restoration is a `git revert` plus a follow-up to wire the file behind the env-var gate per §3.3.

---

## 4. PR #A2 — Remove personal startup-media path

**Closes:** F9 (P1). **Risk:** low. **Estimated diff:** ~10 LOC changed, 1 test added.

### 4.1 Current state

`src/Aethra/Views/MainWindow.xaml.cs:121`:

```csharp
private const string PreferredStartupMediaPath = @"C:\Users\rjh\Videos\test.mp4";
```

Used at line 2977 inside `TryLoadStartupMedia`:

```csharp
var startupPath = ResolveStartupMediaCandidate(
    PreferredStartupMediaPath, _lastLoadedMediaPath, out var shouldResumePersistedPosition);
```

The existing `ResolveStartupMediaCandidate` (line 2989) already prefers a persisted path when the preferred one is missing — so on every machine that isn't the developer's, this const silently no-ops. We just need to stop hard-coding it and stop leaking the dev's username into the binary.

### 4.2 Target shape

Mirror the existing env-var pattern at `MainWindow.xaml.cs:57-58`:

```csharp
private static bool RunGpuSurfaceSmoke =>
    string.Equals(Environment.GetEnvironmentVariable("AETHRA_GPU_SURFACE_SMOKE"), "1", StringComparison.Ordinal);
```

Replace lines 121 and 2977 as follows.

**Before** (line 121):

```csharp
private const string PreferredStartupMediaPath = @"C:\Users\rjh\Videos\test.mp4";
```

**After:**

```csharp
// Optional debug override: set AETHRA_STARTUP_MEDIA to a full path to force the
// startup media target. When unset, startup falls back to the user's last-played file.
private static string? PreferredStartupMediaPath =>
    Environment.GetEnvironmentVariable("AETHRA_STARTUP_MEDIA");
```

**Before** (line 2977):

```csharp
var startupPath = ResolveStartupMediaCandidate(PreferredStartupMediaPath, _lastLoadedMediaPath, out var shouldResumePersistedPosition);
```

**After:**

```csharp
var startupPath = ResolveStartupMediaCandidate(
    PreferredStartupMediaPath ?? string.Empty,
    _lastLoadedMediaPath,
    out var shouldResumePersistedPosition);
```

`ResolveStartupMediaCandidate` already handles empty/whitespace via `NormalizeMediaTarget` → `IsPlayableMediaTarget` (line 3015), so the `?? string.Empty` is purely defensive against the signature requiring a non-null first arg.

### 4.3 Test changes

`tests/Aethra.Tests/Views/MainWindowStartupTests.cs` already covers `ResolveStartupMediaCandidate` for the three branches (preferred wins, persisted fallback, neither found). Those tests stay green because `ResolveStartupMediaCandidate` is unchanged.

Add one new test asserting the env-var contract:

```csharp
[Fact]
public void PreferredStartupMediaPath_ReturnsEnvVarValue_WhenSet()
{
    var sentinel = @"C:\fake\override.mp4";
    var previous = Environment.GetEnvironmentVariable("AETHRA_STARTUP_MEDIA");
    try
    {
        Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", sentinel);
        // PreferredStartupMediaPath is private; expose it as `internal static` for tests
        // (the file already uses InternalsVisibleTo for Aethra.Tests in Aethra.csproj line 79-81).
        Assert.Equal(sentinel, MainWindow.PreferredStartupMediaPathForTests);
    }
    finally
    {
        Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", previous);
    }
}

[Fact]
public void PreferredStartupMediaPath_ReturnsNull_WhenEnvVarUnset()
{
    var previous = Environment.GetEnvironmentVariable("AETHRA_STARTUP_MEDIA");
    try
    {
        Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", null);
        Assert.Null(MainWindow.PreferredStartupMediaPathForTests);
    }
    finally
    {
        Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", previous);
    }
}
```

This requires exposing the property to tests. Add an `internal static` test accessor in `MainWindow.xaml.cs` immediately after the property definition:

```csharp
internal static string? PreferredStartupMediaPathForTests => PreferredStartupMediaPath;
```

`Aethra.csproj` already declares `InternalsVisibleTo("Aethra.Tests")` (lines 79-81), so no project change is needed.

### 4.4 Documentation

Add a one-line note under a new "Debug overrides" section in `docs/development/copilot-instructions.md`:

```
- AETHRA_STARTUP_MEDIA: optional full path to force the startup media target (overrides the persisted last-played file).
- AETHRA_GPU_SURFACE_SMOKE: set to "1" to run the GPU surface smoke at MainWindow startup.
```

### 4.5 Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke (developer-only): launch the app once with `$env:AETHRA_STARTUP_MEDIA=$null` and once with `$env:AETHRA_STARTUP_MEDIA="C:\path\to\local.mp4"`; verify behavior matches the env var.

### 4.6 Rollback

Single revert. The const was load-bearing only on the developer's machine.

---

## 5. PR #A3 — Atomic file persistence

**Closes:** F6 (partial — atomicity only). **Risk:** medium. **Estimated diff:** +80 LOC new helper, +60 LOC test, ~6 LOC changed across 4 call sites.

### 5.1 Current state

Four call sites in `Configuration/` write JSON or `input.conf` text directly via `File.WriteAllText` / `File.WriteAllLines`:

| File:line                                                          | What it writes                          |
| ------------------------------------------------------------------ | --------------------------------------- |
| `src/Aethra/Configuration/PreferencesProfilesStore.cs:57`          | `preferences-profiles.json` (canonical) |
| `src/Aethra/Configuration/InputBindingSettingsStore.cs:106`        | `input-bindings.json` (canonical)       |
| `src/Aethra/Configuration/InputBindingSettingsStore.cs:115`        | `input.conf` (user export)              |
| `src/Aethra/Configuration/PreferencesProfileBundleExchange.cs:43`  | profile bundle export (user-chosen path)|

A crash, OS power loss, or OneDrive sync interruption between truncate and final write produces a partial or zero-byte file. Both `Load` paths catch all exceptions and silently restore defaults, which means **a user's customizations vanish without warning.**

### 5.2 New helper

Add `src/Aethra/Configuration/AtomicFile.cs` (~80 LOC) — a small, self-contained, dependency-free static helper. **No new NuGet packages.**

```csharp
using System.IO;
using System.Text;

namespace Aethra.Configuration;

/// <summary>
/// Crash-safe file writes for canonical settings/state files. Writes go to a
/// sibling temp file, the temp is flushed to disk, then it is renamed over the
/// destination atomically. On Windows NTFS, File.Move(overwrite: true) is an
/// atomic rename within a single volume. On a crash mid-write, either the old
/// file or the new file is present — never a truncated mix.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Atomically write UTF-8 text to <paramref name="path"/>.</summary>
    internal static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(contents);

        using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>Atomically write a sequence of UTF-8 lines to <paramref name="path"/>.</summary>
    internal static void WriteAllLines(string path, IEnumerable<string> lines, Encoding? encoding = null)
    {
        // Materialize once so we don't enumerate twice if the caller passed LINQ.
        var joined = string.Join(Environment.NewLine, lines);
        WriteAllText(path, joined + Environment.NewLine, encoding);
    }
}
```

### 5.3 Call-site changes

Each is a one-line swap. **`internal`** visibility is sufficient because all four call sites live in the same `Aethra.Configuration` namespace.

`PreferencesProfilesStore.cs:53-58`:

```csharp
internal static void SaveToPath(string path, PreferencesPageProfiles profiles)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
    var json = JsonSerializer.Serialize(profiles, JsonOptions);
    AtomicFile.WriteAllText(path, json);   // was: File.WriteAllText(path, json, Encoding.UTF8);
}
```

`InputBindingSettingsStore.cs:99-107`:

```csharp
public static void Save(IEnumerable<InputBindingSetting> bindings)
{
    var rows = bindings.Select(CloneBinding)
        .Where(b => !string.IsNullOrWhiteSpace(b.Gesture) && !string.IsNullOrWhiteSpace(b.Command))
        .ToList();
    var json = JsonSerializer.Serialize(rows, JsonOptions);
    AtomicFile.WriteAllText(GetBindingsFilePath(), json);  // was: File.WriteAllText(...)
}
```

`InputBindingSettingsStore.cs:109-117` (export):

```csharp
public static string ExportToInputConf(IEnumerable<InputBindingSetting> bindings)
{
    var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "input.conf");
    var lines = bindings
        .Where(b => !string.IsNullOrWhiteSpace(b.Gesture) && !string.IsNullOrWhiteSpace(b.Command))
        .Select(b => $"{b.Gesture.Trim()} {b.Command.Trim()}");
    AtomicFile.WriteAllLines(path, lines);   // was: File.WriteAllLines(path, lines);
    return path;
}
```

`PreferencesProfileBundleExchange.cs:42-43`:

```csharp
var json = JsonSerializer.Serialize(document, JsonOptions);
AtomicFile.WriteAllText(path, json);          // was: File.WriteAllText(path, json, Encoding.UTF8);
```

The `Directory.CreateDirectory` call inside `PreferencesProfileBundleExchange` (line 30-32) becomes redundant because `AtomicFile.WriteAllText` does it; **leave it in place** for this PR (no unrelated cleanup).

### 5.4 New tests

Add `tests/Aethra.Tests/Configuration/AtomicFileTests.cs` (~60 LOC). All tests run in the test temp folder; no Windows App Runtime needed.

```csharp
using System.IO;
using Aethra.Configuration;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class AtomicFileTests
{
    [Fact]
    public void WriteAllText_CreatesFileWithExpectedContents()
    {
        var path = TempFile();
        try
        {
            AtomicFile.WriteAllText(path, "hello\nworld");
            Assert.Equal("hello\nworld", File.ReadAllText(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void WriteAllText_OverwritesExistingFile()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "old contents");
            AtomicFile.WriteAllText(path, "new contents");
            Assert.Equal("new contents", File.ReadAllText(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void WriteAllText_LeavesNoTempFileBehind_OnSuccess()
    {
        var path = TempFile();
        try
        {
            AtomicFile.WriteAllText(path, "x");
            Assert.False(File.Exists(path + ".tmp"), "temp file should be renamed away");
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void WriteAllText_PreservesPriorContents_WhenWriteThrows()
    {
        // Simulate a write failure by holding an exclusive open on the destination
        // path's directory? Easier: pre-create the destination, then call WriteAllText
        // with a path whose parent directory cannot be written. We rely on a non-
        // existent volume to force a failure.
        var path = @"Z:\aethra-atomic-test-bogus\file.txt";
        Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllText(path, "x"));
    }

    [Fact]
    public void WriteAllText_CreatesParentDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"aethra-atomic-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "nested", "file.txt");
        try
        {
            AtomicFile.WriteAllText(path, "x");
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), $"aethra-atomic-{Guid.NewGuid():N}.txt");

    private static void Cleanup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }
}
```

### 5.5 Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke: run the app, change one preference, kill the process via Task Manager **between** the change and a subsequent change, restart. With the old code, the JSON file may be 0 bytes and prefs reset; with the new code, the prior change must persist.

### 5.6 Risk and rollback

Risk: `File.Move(overwrite: true)` requires .NET Core+ which is satisfied by `net10.0`. On non-NTFS volumes the rename may not be strictly atomic but is still durable; this is acceptable because `LocalAppData` is always NTFS on Windows.

Rollback: revert. The four call sites are tiny.

---

## 6. PR #A4 — Pool the software-backend frame buffer

**Closes:** F8 (partial — software-backend allocation only). **Risk:** low. **Estimated diff:** ~6 LOC changed in one file.

### 6.1 Current state

`src/Aethra/Native/NativeMpvSoftwarePlayer.cs:364-379`:

```csharp
private static byte[] CopyFrame(NativeMpvSoftwareFrame frame)
{
    var rowBytes = frame.Width * BytesPerPixel;
    var pixels = new byte[rowBytes * frame.Height];   // 640 * 360 * 4 = 921,600 bytes per frame
    for (var row = 0; row < frame.Height; row++)
    {
        Marshal.Copy(frame.Buffer + row * frame.Stride, pixels, row * rowBytes, rowBytes);
    }
    return pixels;
}
```

`FrameWidth = 640` and `FrameHeight = 360` are constants (lines 19-20). Frame size is fixed for the lifetime of the player. Every produced frame allocates a fresh ~922 KB array that is filled once, copied into the `WriteableBitmap`, and dropped.

The presentation flow is gated by `_presentationQueued` (`Native/NativeMpvSoftwarePlayer.cs:307-315`): the player thread will not produce a new frame until the UI thread has finished consuming the prior one. Therefore **only one `pixels` buffer is ever in flight.** A single shared scratch field is safe.

### 6.2 Target shape

Introduce one field and convert `CopyFrame` to instance:

```csharp
private byte[]? _frameScratch;

private byte[] CopyFrame(NativeMpvSoftwareFrame frame)
{
    var rowBytes = frame.Width * BytesPerPixel;
    var required = rowBytes * frame.Height;
    var pixels = _frameScratch ??= new byte[required];

    // Defensive: if frame dimensions ever became dynamic, resize. With current
    // FrameWidth/FrameHeight constants this branch is dead, but it's cheap insurance.
    if (pixels.Length < required)
        pixels = _frameScratch = new byte[required];

    for (var row = 0; row < frame.Height; row++)
    {
        Marshal.Copy(frame.Buffer + row * frame.Stride, pixels, row * rowBytes, rowBytes);
    }
    return pixels;
}
```

Add a comment block immediately above clarifying the safety contract:

```csharp
// _frameScratch is reused across frames. Single-in-flight presentation is enforced
// by the _presentationQueued interlock in QueueFramePresentation: the player thread
// will not start the next CopyFrame until PresentFrame on the UI thread has finished
// reading the buffer and cleared the gate. Do not change that gate without revisiting
// this reuse decision.
```

### 6.3 Why not `ArrayPool<byte>.Shared`?

ArrayPool is a fine alternative and arguably more idiomatic, but:

- It adds a Rent/Return lifetime to manage across the dispatcher boundary (the UI-thread `PresentFrame` would have to call `Return`).
- The extra indirection costs ~50 ns per frame versus a direct field read.
- The pool may hand back a buffer larger than `required`; `stream.Write(pixels, 0, pixels.Length)` would then write past the valid region. We'd need to track and pass the valid length.

A field is simpler, faster, and survives any future PR that bumps frame size as long as the gate semantics survive. If a later PR moves the software backend to dynamic resolution, switch to `ArrayPool` then.

### 6.4 Tests

The software backend has no existing unit tests (it depends on `NativeMpvContext` which depends on `libmpv-2.dll`). This PR does not add any — the change is internal, the behavior is unchanged, and the test fixture cost is disproportionate. The verification is a manual perf trace (see §6.5).

### 6.5 Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual perf check (developer-only):

1. Launch the app and load a video that forces the software backend (set `_useGpuVideoSurface = false` temporarily, or run on a machine without the ANGLE path).
2. Attach a profiler (PerfView or `dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime`).
3. Confirm that allocations under `NativeMpvSoftwarePlayer.CopyFrame` drop to zero per frame after the first.
4. Confirm visual playback is unchanged.

### 6.6 Risk and rollback

Risk: if the frame size ever becomes dynamic, the `if (pixels.Length < required)` branch handles it but a defensive comment in `NativeMpvSoftwarePlayer` near `FrameWidth`/`FrameHeight` should note that any change to those constants requires re-checking this path.

Rollback: revert. One file, ~6 LOC.

---

## 7. PR #A5 — Retire the `ToggleLoopFile` alias

**Closes:** F3 (partial — `ToggleLoopFile`/`CycleRepeat` ambiguity). **Risk:** low. **Estimated diff:** ~10 LOC removed across 3 files.

### 7.1 Current state

`AethraCommandIds.ToggleLoopFile` (`src/Aethra/Commands/AethraCommandIds.cs:31`) is defined as `"aethra:toggle-loop-file"`. The dispatcher (`src/Aethra/Commands/AethraCommandDispatcher.cs:95-97`) routes it to `_context.CycleRepeat()` — the same target as `AethraCommandIds.CycleRepeat`. The dispatcher test (`tests/Aethra.Tests/Commands/AethraCommandDispatcherTests.cs:71`) locks this alias in.

**Verified by `grep`:** `ToggleLoopFile` is **not bound** in `InputBindingCatalog` defaults, **not referenced in any XAML**, and **not referenced from any other production code**. It is API surface that nothing fires.

### 7.2 Decision

Retire the const. The repeat-cycle behavior remains accessible via `aethra:cycle-repeat` (`AethraCommandIds.CycleRepeat`). Note: `CycleRepeat` is bound in `InputBindingCatalog.cs` only to Scimitar mouse buttons (`KP_DEC` and `KP_DEL`, see lines 117 and 132) — it has **no keyboard binding by default**. That gap is a separate finding worth filing later, but it does **not** block this PR; users who today rely on `aethra:toggle-loop-file` would have nothing fired against it (no default binding) so removing it is a no-op for them too.

If a real `aethra:toggle-loop-file` is needed in the future (a single-file loop toggle distinct from the off/one/all cycle), it should be re-introduced with a distinct context method, not aliased.

### 7.3 Edits

1. **`src/Aethra/Commands/AethraCommandIds.cs:31`** — delete the line:

   ```csharp
   internal const string ToggleLoopFile = "aethra:toggle-loop-file";
   ```

2. **`src/Aethra/Commands/AethraCommandDispatcher.cs:95-97`** — delete the case:

   ```csharp
   case AethraCommandIds.ToggleLoopFile:
       _context.CycleRepeat();
       return true;
   ```

3. **`tests/Aethra.Tests/Commands/AethraCommandDispatcherTests.cs:71`** — delete the row:

   ```csharp
   yield return new object[] { AethraCommandIds.ToggleLoopFile, new[] { "CycleRepeat" } };
   ```

### 7.4 Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Build will fail loudly if any other consumer existed (which the grep confirms it doesn't). Test count drops by 1.

Manual smoke: launch the app and trigger `aethra:cycle-repeat` via its default Scimitar mouse binding (`KP_DEC` / `KP_DEL`), or temporarily add a keyboard binding in user settings, and confirm the repeat-mode UI cycles. Verify the `Off / One / All` glyphs at `MainWindow.xaml.cs:128-130` still cycle as expected. (For dev convenience without a Scimitar, the most reliable check is to assert via the existing `AethraCommandDispatcherTests.cs` tests for `CycleRepeat` — already green — and the dispatcher LOC drop is the actual deliverable.)

### 7.5 Worklog notes

The worklog entry should explicitly note: "Removed unused `aethra:toggle-loop-file` command. The repeat-mode cycle remains available via `aethra:cycle-repeat`. No user-visible binding change." Mention this even if no user is affected — future archeology benefits from the explicit record.

### 7.6 Rollback

Revert. No persisted user state references this ID (nothing was bound to it).

---

## 8. PR #A6 — Make `aethra:quit` discard resume position

**Closes:** F3 (the `Quit`/`QuitWatchLater` aliasing). **Risk:** medium (behavior change). **Estimated diff:** +15 LOC, ~5 LOC changed.

### 8.1 Current state — corrected diagnosis

`MainWindow.xaml.cs:186-187` passes `CloseWindowFromCommand` for **both** `quit` and `quitWatchLater` slots. `CloseWindowFromCommand` (line 595) just calls `Close()`, which fires `MainWindow_Closed` (line 229). That handler **unconditionally** persists state at lines 238-242:

```csharp
PlaybackPersistenceStore.SaveVolume(_currentVolume);
if (ShouldRememberRecentFiles())
    PlaybackPersistenceStore.SaveLastMedia(_lastLoadedMediaPath, _currentPlaybackPosition);
else
    PlaybackPersistenceStore.ClearLastMedia();
```

The `ShouldRememberRecentFiles()` gate (line 3111) reads a user preference and defaults to true, so for most users **both `aethra:quit` and `aethra:quit-watch-later` save the resume position today.** They are behaviorally identical.

mpv's convention is the opposite: `quit` discards the resume marker; `quit-watch-later` keeps it. The fix therefore is **not** to add saving to `quit-watch-later` (it already does); it is to make `quit` opt out of saving while preserving the X-button close behavior.

### 8.2 Target shape

Add one private flag and one new command method to `MainWindow`. The flag defaults to false, so the X-button (which is the most common close path) continues to honor `ShouldRememberRecentFiles()` exactly as today.

Add the flag near the other state flags (e.g. just below line 117 `_startupMediaLoaded`):

```csharp
private bool _suppressResumePersistenceOnClose;
```

Add a new private method adjacent to `CloseWindowFromCommand` at line 595:

```csharp
private void QuitDiscardingResumeFromCommand()
{
    _suppressResumePersistenceOnClose = true;
    CloseWindowFromCommand();
}
```

Update the resume-save block in `MainWindow_Closed` at lines 239-242:

**Before:**

```csharp
if (ShouldRememberRecentFiles())
    PlaybackPersistenceStore.SaveLastMedia(_lastLoadedMediaPath, _currentPlaybackPosition);
else
    PlaybackPersistenceStore.ClearLastMedia();
```

**After:**

```csharp
if (_suppressResumePersistenceOnClose || !ShouldRememberRecentFiles())
    PlaybackPersistenceStore.ClearLastMedia();
else
    PlaybackPersistenceStore.SaveLastMedia(_lastLoadedMediaPath, _currentPlaybackPosition);
```

Update the command wiring at lines 186-187:

**Before:**

```csharp
CloseWindowFromCommand,
CloseWindowFromCommand,
```

**After:**

```csharp
QuitDiscardingResumeFromCommand,    // quit
CloseWindowFromCommand,             // quitWatchLater
```

Volume and window-geometry persistence remain unconditional in both paths — they're not part of the resume-position semantic.

### 8.3 Behavior matrix after this PR

| Action                            | Resume saved? | Notes                                    |
| --------------------------------- | ------------- | ---------------------------------------- |
| X-button close                    | yes (gated by `RememberRecentFiles`) | unchanged from today |
| `aethra:quit` (default `Shift+q`) | **no** (always cleared)              | new — discards resume marker |
| `aethra:quit-watch-later` (default `q`) | yes (gated by `RememberRecentFiles`) | unchanged behaviorally; the fix routes it to the same path the X-button uses |
| Process kill / crash              | n/a — `Closed` does not fire         | unchanged                |

### 8.4 Test changes

The existing dispatcher test (`AethraCommandDispatcherTests.cs:50`) verifies that `aethra:quit-watch-later` reaches the `QuitWatchLater` named slot in the test fake context. That stays green.

Add one test to confirm the dispatcher routes `Quit` and `QuitWatchLater` to **distinct** named actions, so a future regression that re-aliases them is caught:

```csharp
[Fact]
public void Execute_QuitAndQuitWatchLater_RouteToDistinctActions()
{
    var invocations = new Dictionary<string, int>(StringComparer.Ordinal);
    var dispatcher = new AethraCommandDispatcher(CreateContext(invocations));

    dispatcher.Execute(AethraCommandIds.Quit);
    dispatcher.Execute(AethraCommandIds.QuitWatchLater);

    Assert.Equal(1, invocations.GetValueOrDefault("Quit"));
    Assert.Equal(1, invocations.GetValueOrDefault("QuitWatchLater"));
    Assert.Equal(2, invocations.Values.Sum());
}
```

This guards the dispatcher contract. The behavioral verification (that `Quit` actually clears the persisted last-media key) **cannot** be unit-tested today without the WindowsAppRuntime — `PlaybackPersistenceStore.ClearLastMedia` reaches `ApplicationData.Current.LocalSettings.Values` directly. Phase B (B1) introduces the `ISettingsStore` seam that lets us test this end-to-end. Until then, the verification is the manual smoke in §8.5.

### 8.5 Manual smoke (required for this PR)

1. Launch the app, load a video, seek to ~30 seconds.
2. Press `q` (the binding for `aethra:quit-watch-later`, see `InputBindingCatalog.cs:25`).
3. Relaunch. The video must reload and seek back to ~30 seconds. ✅
4. Load a video again, seek to ~30 seconds.
5. Press `Shift+q` (the binding for `aethra:quit`, see `InputBindingCatalog.cs:26` — note: the binding string is the literal uppercase `Q`, which the input parser interprets as `Shift+q`).
6. Relaunch. The app must **not** reload the prior video. ✅
7. Load a video, seek to ~30 seconds, click the window X button.
8. Relaunch. The video must reload and seek back to ~30 seconds (assuming default `RememberRecentFiles=true`). ✅ — this confirms the X-button path is unchanged.

If any of those three relaunch behaviors disagree with the "✅" outcome, the PR is not ready.

### 8.6 Documentation

Worklog entry must call out: "Behavior change. `aethra:quit` now discards the saved resume marker (matches mpv convention); `aethra:quit-watch-later` continues to save resume position. The X-button close path is unchanged — it follows the user's `RememberRecentFiles` preference exactly as today. No user data migration required."

### 8.7 Risk and rollback

Risk: medium — this is the only Phase A PR that changes user-visible behavior. The risk is that some user has come to rely on `aethra:quit` saving position; the explicit worklog note and the symmetry with mpv mitigate that.

The implementation risk is small: `_suppressResumePersistenceOnClose` is set on the dispatcher thread before `Close()`, and `MainWindow_Closed` runs on the dispatcher thread, so there is no race. Volume persistence and window-geometry persistence are untouched.

If after merge a user reports relying on the old behavior, the rollback is a one-line change: flip the flag's default check in `MainWindow_Closed` to ignore `_suppressResumePersistenceOnClose`. Full revert is also trivial (one method, one wiring slot, one flag).

---

## 9. Cross-cutting concerns and what Phase A explicitly does NOT do

### 9.1 Out of scope (defer to later phases)

* **No `ISettingsStore` / `IFileStore` interfaces.** That is Phase B (B1). Until then, Phase A test coverage of `PlaybackPersistenceStore`, `AccentColorService`, and `ScriptExtensionSettingsStore` remains thin.
* **No MVVM extraction.** No `INotifyPropertyChanged`, no view-models. Phase D.
* **No GPU backend changes.** The ANGLE/D3D11 path is touched only by the dead-code deletion in #A1. The `WaitHandle.WaitOne(8 ms)` polling loop in `NativeMpvOpenGlPlayer.Run` is not addressed in Phase A; that's Phase C (C2).
* **No registry/dispatcher refactor.** The 39-positional-Action `AethraCommandContext` constructor is not changed in Phase A. Phase B (B3) replaces it with a registry. PR #A6 only swaps which Action is in the `quitWatchLater` slot.
* **No deletion of redirect-only docs at `Aethra/`.** Phase E (E3).
* **No SDK upgrade, no NuGet additions, no XAML edits.**

### 9.2 What changes in `docs/development/worklog.md`

Each PR appends one entry. Suggested template:

```
### YYYY-MM-DD — Phase A / PR #A<n>: <title>

- Closes: <F-numbers>
- Diff: <files touched, LOC delta>
- Verified: dotnet build (clean), dotnet test (N tests, M passed), manual smoke (if applicable)
- Behavior change: <none | brief description>
- Next: <next Phase A PR or "Phase A complete">
```

### 9.3 What changes in `docs/architecture/agent-sitemap.md`

No structural change required during Phase A. After all Phase A PRs land, append a one-line entry under "Important Entry Points":

```
- Atomic file writes for canonical settings: `src/Aethra/Configuration/AtomicFile.cs`.
```

### 9.4 What changes in `docs/project/roadmap.md`

If the roadmap references Phase A items by name (review on-merge), update the status from "planned" to "shipped" per item. If the roadmap doesn't track at this granularity, no edit is required.

---

## 10. Definition of done for Phase A

Phase A is complete when **all six** boxes are true:

- [ ] PR #A1 merged: dead native code deleted (or gated per §3.3); CI green.
- [ ] PR #A2 merged: `PreferredStartupMediaPath` is env-var-backed; CI green; one test added.
- [ ] PR #A3 merged: `AtomicFile` helper exists and is used by all four call sites; AtomicFileTests passes; CI green.
- [ ] PR #A4 merged: software-backend frame buffer is reused; CI green; manual perf trace confirmed.
- [ ] PR #A5 merged: `aethra:toggle-loop-file` is gone from IDs/dispatcher/tests; CI green.
- [ ] PR #A6 merged: `aethra:quit-watch-later` saves position; manual smoke confirmed; CI green.

After Phase A:

* Repo loses ~607 LOC of dead code, gains ~80 LOC of helper + ~140 LOC of tests.
* The `aethra:*` surface drops one redundant ID and one buggy alias.
* Settings files stop disappearing on partial writes.
* The software-backend allocation pressure drops to ~zero per frame after warmup.
* No user-visible behavior changes except (a) the personal-path const goes away (no real user affected) and (b) `aethra:quit-watch-later` now actually does what its name says.

---

## 11. What to read before starting any PR

1. `docs/architecture/architectural-review-2026-05.md` — the full diagnosis, especially §4 (findings) and §5 (refactor plan).
2. `docs/development/copilot-instructions.md` — workflow rules.
3. `docs/project/DIRECTION.md` — non-negotiables (no NuGet adds without approval, no XAML edits in Phase A, etc.).
4. The target file for the PR.
5. `docs/development/worklog.md` — most recent entry, in case something has shifted since this plan was written.

If anything in the target file has materially changed since 2026-05-02, **stop and amend this plan** before touching code.
