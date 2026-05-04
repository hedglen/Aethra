# Phase B — Implementation Plan

Owner: senior-engineer onboarding pass
Source plan: `docs/architecture/architectural-review-2026-05.md` §5, Phase B.
Prereq: `docs/architecture/phase-a-implementation-plan.md` (Phase A must be merged; B1 reuses `AtomicFile` from Phase A).
Workflow rules followed: `docs/development/copilot-instructions.md` ("one small step only", build after every step, worklog entry after every step, no unrelated refactors).

This document is the executable spec for Phase B. Every change below names exact files, exact line ranges, before/after sketches, the test changes that ship with it, and the verification commands to run before declaring done.

---

## 0. Phase B in one paragraph

Phase B is the **test-seam pass**: introduce small interfaces around the static stores and singleton services so the consumer code can be exercised against fakes, without changing any user-visible behavior. Three logical pieces ship: (B1) interface seams over `LocalSettings.Values` and JSON file persistence, (B2) interface seams over `PlaybackOptionsService` and `AccentColorService`, (B3) a registry-based replacement for the 39-positional `AethraCommandContext`. After Phase B, every change Phase D wants to make to MVVM, every regression test we want to write against playback-options forwarding, and every new command we want to add lands without touching five places. **No XAML edits, no NuGet additions, no MVVM yet** — that's still Phase D.

Total scope: **9 PRs, ~−200 LOC net (B3 shrinks the command system substantially), +6 new interfaces, +3 production implementations, +3 in-memory test fakes, and ~10 new test files covering surface that's currently uncovered.**

---

## 1. PR sequence and dependencies

The dependency chain is mostly linear inside each sub-phase, but **B1, B3, and B2 are independent of each other** at the interface level. The recommended landing order minimizes merge conflicts in `MainWindow.xaml.cs`, which both B3 and B2 touch:

| Order | PR    | Item   | Risk  | Touches                                               | Depends on |
| ----- | ----- | ------ | ----- | ----------------------------------------------------- | ---------- |
| 1     | #B1a  | B1     | low   | new files in `Configuration/IO/`                       | Phase A merged |
| 2     | #B1b  | B1     | low   | new files in `Configuration/IO/`                       | Phase A merged (uses `AtomicFile`) |
| 3     | #B1c  | B1     | med   | `PlaybackPersistenceStore`, `ScriptExtensionSettingsStore` + tests | #B1a |
| 4     | #B1d  | B1     | med   | `AccentColorService` (storage half) + tests           | #B1a |
| 5     | #B1e  | B1     | med   | `PreferencesProfilesStore`, `InputBindingSettingsStore`, `PreferencesProfileBundleExchange` + tests | #B1b |
| 6     | #B3a  | B3     | low   | new file `Commands/AethraCommandRegistry.cs` + tests  | none |
| 7     | #B3b  | B3     | med   | `Commands/*` rewrite, `MainWindow.xaml.cs` wiring     | #B3a |
| 8     | #B2a  | B2     | low   | `Services/IPlaybackOptions.cs` + `PlaybackOptionsService` declares it | none |
| 9     | #B2b  | B2     | med   | `IAccentColors`, instance extraction inside `AccentColorService`, `MainWindow`/`FullSettingsPanel` Initialize, `App.xaml.cs` wiring | #B2a |

PRs #B1a-e are fully parallelizable across reviewers. #B3a/b can ship before or after the B1 chain. #B2 ships last because it's the only sub-phase that adds an `Initialize(...)` method to `MainWindow` — touching that file last keeps each preceding PR's diff small.

---

## 2. Standing rules for every PR in Phase B

(Identical to Phase A; restated here so this doc is self-contained.)

1. **Build before opening:** `dotnet build .\Aethra.slnx -p:Platform=x64`. Zero warnings, zero errors.
2. **Test before opening:** `dotnet test .\Aethra.slnx -p:Platform=x64 --no-build`. All green.
3. **Worklog entry:** append a short block to `docs/development/worklog.md`.
4. **PR description:** use `.github/PULL_REQUEST_TEMPLATE.md`, call out the F-numbers being closed (F3, F6, F7, F11 are the relevant ones).
5. **No NuGet adds, no XAML edits, no SDK upgrades, no MVVM** in Phase B.
6. **No unrelated cleanups.** Split if the diff strays.
7. **Public API discipline:** the existing `static` stores keep their public API throughout Phase B. The new interfaces are *internal* — they exist for testing and for Phase D, not for downstream consumers. The only public API change in Phase B is the deletion of `AethraCommandContext` (B3b), which is `internal` already and has no external consumers.

---

## 3. Sub-phase B1 — Settings/file store seams

### 3.1 The problem this sub-phase solves

Today every store reaches `ApplicationData.Current.LocalSettings.Values` or `ApplicationData.Current.LocalFolder` directly (inventory at architectural review §2.3, F6, F7). Consequences:

* Stores cannot be unit-tested without WindowsAppRuntime initialization.
* The existing tests for `PreferencesProfilesStore` and `InputBindingSettingsStore` only exercise `internal` path-taking overloads; the public `Load`/`Save` paths that production hits are uncovered.
* `AccentColorService`, `PlaybackPersistenceStore`, and `ScriptExtensionSettingsStore` have **zero** tests for the same reason.

The seam: two small `internal` interfaces (`ISettingsStore`, `IFileStore<T>`), three production implementations, three in-memory test fakes. The public API of every existing static store stays exactly as it is — production callers don't change. Each store gets a `static internal SetForTesting(...)` hook that swaps the backing instance for tests.

### 3.2 Folder shape after B1

```
src/Aethra/Configuration/
├── AtomicFile.cs                       (Phase A)
├── IO/                                 NEW
│   ├── ISettingsStore.cs               NEW
│   ├── IFileStore.cs                   NEW
│   ├── LocalSettingsStore.cs           NEW
│   ├── JsonFileStore.cs                NEW
│   └── InMemoryStores.cs               NEW (test helper, lives in production assembly so InternalsVisibleTo can reach it)
├── PlaybackPersistenceStore.cs         (refactored to delegate)
├── ScriptExtensionSettingsStore.cs     (refactored to delegate)
├── PreferencesProfilesStore.cs         (refactored to delegate)
├── InputBindingSettingsStore.cs        (refactored to delegate)
├── PreferencesProfileBundleExchange.cs (refactored to delegate)
├── ... (other existing files unchanged)
```

`AccentColorService.cs` stays in `Services/` but its storage helpers (`ReadSavedHex` / `SaveHex` / `ReadFavoriteHexColors` / `SaveFavoriteHexColors`) are rerouted through `ISettingsStore`. The instance-extraction half of `AccentColorService` (the `IAccentColors` interface) ships in B2, not here.

---

### 3.3 PR #B1a — `ISettingsStore` + `LocalSettingsStore` + `InMemorySettingsStore`

**Closes:** F6, F7 (foundation only). **Risk:** low. **Estimated diff:** +130 LOC across 4 new files, +90 LOC of tests.

#### Interface

`src/Aethra/Configuration/IO/ISettingsStore.cs`:

```csharp
namespace Aethra.Configuration.IO;

/// <summary>
/// Abstraction over a key/value settings backend. The production implementation
/// (LocalSettingsStore) wraps ApplicationData.Current.LocalSettings.Values.
/// Phase B introduces this seam so the static stores can be tested without
/// initializing the WindowsAppRuntime.
/// </summary>
internal interface ISettingsStore
{
    /// <summary>Returns the stored value or null if the key is missing or the read fails.</summary>
    object? TryRead(string key);

    /// <summary>Writes the value. Silently no-ops if the underlying backend is unavailable.</summary>
    void Write(string key, object value);

    /// <summary>Removes the key. Silently no-ops if the key is missing or the backend is unavailable.</summary>
    void Remove(string key);
}
```

#### Production implementation

`src/Aethra/Configuration/IO/LocalSettingsStore.cs`:

```csharp
using Windows.Storage;

namespace Aethra.Configuration.IO;

internal sealed class LocalSettingsStore : ISettingsStore
{
    public object? TryRead(string key)
    {
        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return values.TryGetValue(key, out var value) ? value : null;
        }
        catch
        {
            // LocalSettings can be unavailable in unusual unpackaged/debug contexts.
            return null;
        }
    }

    public void Write(string key, object value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch
        {
            // Mirror the existing AccentColorService swallow — Phase B preserves behavior.
        }
    }

    public void Remove(string key)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
        }
        catch
        {
            // Same swallow rationale.
        }
    }
}
```

The `try/catch` swallows are intentional: they preserve the behavior of the existing static stores. PR #B1a is **strictly** a seam, not a behavior change.

#### Test fake

`src/Aethra/Configuration/IO/InMemoryStores.cs` (lives in the production assembly under `internal` visibility — `Aethra.Tests` already has `InternalsVisibleTo` per `Aethra.csproj:79-81`):

```csharp
using System.Collections.Generic;

namespace Aethra.Configuration.IO;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object> _values = new();

    public IReadOnlyDictionary<string, object> Snapshot => _values;

    public void Seed(string key, object value) => _values[key] = value;

    public object? TryRead(string key) => _values.TryGetValue(key, out var v) ? v : null;

    public void Write(string key, object value) => _values[key] = value;

    public void Remove(string key) => _values.Remove(key);
}
```

(File also hosts `InMemoryFileStore<T>` once #B1b lands. PR #B1a creates the file with only `InMemorySettingsStore`.)

#### Tests

`tests/Aethra.Tests/Configuration/IO/InMemorySettingsStoreTests.cs` — round-trip, missing-key, overwrite, remove.

`tests/Aethra.Tests/Configuration/IO/LocalSettingsStoreTests.cs` — **skipped at runtime** in the unit-test project because it requires `ApplicationData.Current`. Use `[Fact(Skip = "Requires WindowsAppRuntime; smoke-test manually after every change.")]` and document the manual smoke procedure inside the test file. We get to claim coverage of the contract via the in-memory test; the production class is provably small enough (~30 LOC) to manually verify.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

#### Rollback

Single revert. No production code is wired to the new interfaces yet.

---

### 3.4 PR #B1b — `IFileStore<T>` + `JsonFileStore<T>` + `InMemoryFileStore<T>`

**Closes:** F6, F7 (foundation only). **Risk:** low. **Estimated diff:** +160 LOC across 3 file additions, +110 LOC of tests.

#### Interface

`src/Aethra/Configuration/IO/IFileStore.cs`:

```csharp
namespace Aethra.Configuration.IO;

/// <summary>
/// Abstraction over a single-file persistence target keyed by absolute path.
/// JsonFileStore is the production implementation; InMemoryFileStore is the test fake.
/// Writes are atomic per AtomicFile (Phase A).
/// </summary>
/// <typeparam name="T">The serialized payload type.</typeparam>
internal interface IFileStore<T> where T : class
{
    /// <summary>True if the file currently exists at <paramref name="path"/>.</summary>
    bool Exists(string path);

    /// <summary>
    /// Loads from <paramref name="path"/>. Returns the deserialized payload, or null if the
    /// file is missing OR the file is unreadable. The boolean out-parameter distinguishes the
    /// two cases so callers can report a structured warning when relevant.
    /// </summary>
    T? TryLoad(string path, out bool wasUnreadable);

    /// <summary>Atomically writes <paramref name="payload"/> to <paramref name="path"/>.</summary>
    void Save(string path, T payload);
}
```

The `out bool wasUnreadable` is the structured-warning seam called out in the architectural review §5 B1. Today `PreferencesProfilesStore.LoadFromPath` swallows errors and returns defaults silently — after B1e the consumer can opt into surfacing the warning.

#### Production implementation

`src/Aethra/Configuration/IO/JsonFileStore.cs`:

```csharp
using System.IO;
using System.Text.Json;

namespace Aethra.Configuration.IO;

internal sealed class JsonFileStore<T> : IFileStore<T> where T : class
{
    private readonly JsonSerializerOptions _options;

    internal JsonFileStore(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public bool Exists(string path) => File.Exists(path);

    public T? TryLoad(string path, out bool wasUnreadable)
    {
        wasUnreadable = false;
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch
        {
            wasUnreadable = true;
            return null;
        }
    }

    public void Save(string path, T payload)
    {
        var json = JsonSerializer.Serialize(payload, _options);
        AtomicFile.WriteAllText(path, json);
    }
}
```

#### Test fake

Append to `src/Aethra/Configuration/IO/InMemoryStores.cs`:

```csharp
internal sealed class InMemoryFileStore<T> : IFileStore<T> where T : class
{
    private readonly Dictionary<string, T> _payloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unreadablePaths = new(StringComparer.OrdinalIgnoreCase);

    public void Seed(string path, T payload) => _payloads[path] = payload;

    public void MarkUnreadable(string path) => _unreadablePaths.Add(path);

    public bool Exists(string path) => _payloads.ContainsKey(path) || _unreadablePaths.Contains(path);

    public T? TryLoad(string path, out bool wasUnreadable)
    {
        if (_unreadablePaths.Contains(path))
        {
            wasUnreadable = true;
            return null;
        }

        wasUnreadable = false;
        return _payloads.TryGetValue(path, out var payload) ? payload : null;
    }

    public void Save(string path, T payload)
    {
        _unreadablePaths.Remove(path);
        _payloads[path] = payload;
    }
}
```

#### Tests

`tests/Aethra.Tests/Configuration/IO/JsonFileStoreTests.cs` — round-trip a small POCO, confirm `wasUnreadable=true` for a deliberately-corrupted JSON file (write garbage via `File.WriteAllText`, then `TryLoad`), confirm `Save` is atomic by reading back after multiple saves.

`tests/Aethra.Tests/Configuration/IO/InMemoryFileStoreTests.cs` — same contract validation with the fake.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

#### Rollback

Single revert. No production code is wired to `IFileStore<T>` yet.

---

### 3.5 PR #B1c — Wire `PlaybackPersistenceStore` and `ScriptExtensionSettingsStore` to `ISettingsStore`

**Closes:** F6, F7, F11 (closes the test gap for two stores). **Risk:** medium. **Estimated diff:** ~40 LOC changed in 2 store files, +180 LOC of new tests.

#### Pattern

Each store keeps its public API exactly as today. Internally it routes every `ApplicationData.Current.LocalSettings.Values` access through a `static ISettingsStore _backing = new LocalSettingsStore();` field, with a `static internal` test seam:

```csharp
internal static void SetBackingForTests(ISettingsStore? backing) =>
    _backing = backing ?? new LocalSettingsStore();
```

Tests use a `try/finally` to swap the backing for the duration of the test:

```csharp
var fake = new InMemorySettingsStore();
PlaybackPersistenceStore.SetBackingForTests(fake);
try
{
    // exercise behavior, assert against fake.Snapshot
}
finally
{
    PlaybackPersistenceStore.SetBackingForTests(null); // restore production
}
```

A small xUnit fixture (`tests/Aethra.Tests/Configuration/SettingsStoreTestFixture.cs`) wraps that try/finally to keep tests terse.

#### `PlaybackPersistenceStore.cs` changes

Replace each `ApplicationData.Current.LocalSettings.Values[X]` with `_backing.TryRead(X)` / `_backing.Write(X, value)` / `_backing.Remove(X)`. Specifically:

| Current code (line) | Replace with |
| --- | --- |
| `var settings = ApplicationData.Current.LocalSettings.Values;` (lines 36, 54, 73) | `var s = _backing;` (rename inside method) |
| `settings[K] as string` | `(string?)_backing.TryRead(K)` |
| `settings[K] = value` | `_backing.Write(K, value)` |
| `settings.Remove(K)` | `_backing.Remove(K)` |
| `settings.TryGetValue(K, out var value)` (lines 82, 97) | inline as `var value = _backing.TryRead(K); if (value is null) return …;` |

The two private helpers `ReadDouble` and `ReadInt` (lines 80-106) become slightly simpler because `TryRead` returns `null` on miss instead of using `TryGetValue`'s out-bool pattern.

#### `ScriptExtensionSettingsStore.cs` changes

Identical pattern. The three property pairs (lines 12-43) become single-expression getters/setters that delegate to `_backing`.

#### New tests

`tests/Aethra.Tests/Configuration/PlaybackPersistenceStoreTests.cs` (NEW):

* `Load_ReturnsDefaults_WhenSettingsAreEmpty` — fake with no entries, all snapshot fields equal default.
* `SaveLastMedia_WritesPathAndPosition` — call `SaveLastMedia("C:/x.mp4", 42.0)`, assert fake snapshot has both keys.
* `SaveLastMedia_NoOps_WhenPathIsWhitespace` — pre-existing keys must remain untouched.
* `ClearLastMedia_RemovesPathAndPosition` — pre-seed values, call clear, assert keys gone, but volume key untouched.
* `SaveVolume_ClampsTo0To100` — `SaveVolume(150)` writes 100; `SaveVolume(-5)` writes 0.
* `SaveWindow_ClampsMinDimensions` — `SaveWindow(0,0,100,50)` writes width=320, height=200 per the existing min clamps at lines 76-77.
* `Load_ReturnsLastWrittenValues` — full round-trip through the fake.

`tests/Aethra.Tests/Configuration/ScriptExtensionSettingsStoreTests.cs` (NEW):

* `ScriptsEnabled_DefaultsToFalse` per the existing fallback at line 20.
* `ScriptsFolder_DefaultsToEmptyString` per the existing fallback at line 31.
* `ResolveEffectiveScriptsFolder_PrefersConfiguredScriptsFolder`.
* `ResolveEffectiveScriptsFolder_FallsBackToPortableScriptsSubdir_WhenItExists` (use a temp directory).
* `ResolveEffectiveScriptsFolder_ReturnsEmpty_WhenNeitherIsConfigured`.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke: launch the app, change accent color and volume, close, relaunch — both must persist exactly as before. (No behavior change is intended; the smoke is to catch wiring errors.)

#### Rollback

Each store's diff is self-contained; revert is a one-file change per store. The new tests can stay if the rollback only touches the production code, since the in-memory store still works against any future `ISettingsStore` consumer.

---

### 3.6 PR #B1d — Reroute `AccentColorService` storage half through `ISettingsStore`

**Closes:** F6, F7, F11 (accent storage is currently untested). **Risk:** medium. **Estimated diff:** ~25 LOC changed in `AccentColorService.cs`, +120 LOC tests.

#### Scope clarification

This PR touches **only** the four storage helpers in `AccentColorService` (`ReadSavedHex`, `SaveHex`, `ReadFavoriteHexColors`, `SaveFavoriteHexColors` — lines 162-219). The brush-application half (`ApplyColor`, `SetBrushColor` — lines 129-160) stays untouched because it depends on `Application.Current.Resources` which is a separate concern handled in B2.

#### Pattern

Same `static ISettingsStore _backing` field with `SetBackingForTests` seam. Each `ApplicationData.Current.LocalSettings.Values[X]` access becomes `_backing.TryRead(X)` / `_backing.Write(X, value)`. The `try/catch` in each helper goes away — `LocalSettingsStore` already swallows. Tests can cover both the happy path and (via the in-memory store) the missing-key path.

#### New tests

`tests/Aethra.Tests/Services/AccentColorServiceTests.cs` (NEW). Coverage targets:

* `TryParseHexColor_AcceptsThreeAndSixDigitForms` — pure function, no fake needed.
* `TryParseHexColor_AcceptsLeadingHash` and `RejectsInvalidLengths`.
* `LoadFavoriteHexColors_ReturnsEmpty_WhenSettingIsAbsent`.
* `LoadFavoriteHexColors_ParsesPipeSeparatedHexList`.
* `LoadFavoriteHexColors_DropsInvalidEntries`.
* `LoadFavoriteHexColors_DeduplicatesAndCapsAtMaxFavoriteAccentColors` (cap is 12 per `MaxFavoriteAccentColors` const).
* `TryAddFavoriteHex_PrependsAndDeduplicates` — verifies the LRU semantics at lines 71-72.
* `TryAddFavoriteHex_TrimsToMaxFavoriteAccentColors` — add 13 distinct entries, snapshot has 12.
* `TryRemoveFavoriteHex_RemovesByNormalizedHexCaseInsensitive`.

The `Initialize`, `TryApplyHex`, and brush-update paths stay untested in this PR — they touch `Application.Current.Resources`, which lands in B2.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke: launch the app, set a custom accent color, add it to favorites, close, relaunch. Color and favorites must restore.

#### Rollback

Single-file revert. Tests stay green against the in-memory store.

---

### 3.7 PR #B1e — Wire JSON stores to `IFileStore<T>`

**Closes:** F6, F7, F11. **Risk:** medium. **Estimated diff:** ~50 LOC changed across 3 store files, +200 LOC of new tests for the public Load/Save paths.

#### Stores affected

| Store                                  | Payload type                           | File path                                 |
| -------------------------------------- | -------------------------------------- | ----------------------------------------- |
| `PreferencesProfilesStore`             | `PreferencesPageProfiles`              | `LocalFolder\preferences-profiles.json`   |
| `InputBindingSettingsStore`            | `List<InputBindingSetting>`            | `LocalFolder\input-bindings.json`         |
| `PreferencesProfileBundleExchange`     | `PreferencesProfileBundleExchangeDocument` | user-chosen path                      |

#### Pattern

Each store gains a `static IFileStore<T> _backing = new JsonFileStore<T>(JsonOptions);` field plus `SetBackingForTests`. The existing `internal` `LoadFromPath`/`SaveToPath` overloads become thin wrappers around `_backing.TryLoad` / `_backing.Save`. The `JsonOptions` per store stay where they live (each has its own converter setup; `JsonFileStore<T>` accepts `JsonSerializerOptions?` in its ctor).

#### `PreferencesProfilesStore.cs` changes

Replace lines 32-58 (`LoadFromPath` and `SaveToPath` bodies) with:

```csharp
internal static PreferencesPageProfiles LoadFromPath(string path)
{
    var loaded = _backing.TryLoad(path, out var wasUnreadable);
    if (loaded is null)
        return PreferencesPageProfiles.CreateDefault();

    NormalizeSubtitleFontSizes(loaded);
    return loaded;
    // wasUnreadable is intentionally not surfaced here — Phase B preserves behavior.
    // Phase D's view-model layer will surface a structured warning to the UI.
}

internal static void SaveToPath(string path, PreferencesPageProfiles profiles)
{
    _backing.Save(path, profiles);
}
```

The `Directory.CreateDirectory` call inside the original `SaveToPath` becomes redundant (the underlying `AtomicFile.WriteAllText` already does it). Remove it as part of this PR — it's a directly-related cleanup, not a tangent.

#### `InputBindingSettingsStore.cs` changes

This file is more complex (~346 LOC) because it owns migration logic. The `_backing` seam covers only the read/write of the JSON file:

* `LoadWithMigration` (lines 26-71) — replace the inline `File.ReadAllText` + `JsonSerializer.Deserialize` block (lines 44-56) with a `_backing.TryLoad(path, out var wasUnreadable)` call. The "saved bindings were unreadable" branch becomes `wasUnreadable == true`. The "saved bindings were empty" branch becomes `loaded?.Count == 0`.
* `Save` (lines 99-107) — replace `File.WriteAllText(GetBindingsFilePath(), json, Encoding.UTF8)` with `_backing.Save(GetBindingsFilePath(), rows)`.
* `ExportToInputConf` (lines 109-117) — **leave alone**. It writes `input.conf` (text, not JSON) via `AtomicFile.WriteAllLines` (which already lands in Phase A). It is not a `JsonFileStore<T>` consumer.

#### `PreferencesProfileBundleExchange.cs` changes

Replace the JSON write at line 42-43 with `_backing.Save(path, document)`. The `TryImportFromPath` at line 46 becomes `_backing.TryLoad(path, out var wasUnreadable)` — and **here** we surface the warning, because the user explicitly imported a file and deserves to know if it was malformed:

```csharp
public static bool TryImportFromPath(string path, out ProfilesPreferencesProfile profiles, out string error)
{
    profiles = ProfilesPreferencesProfile.CreateDefault();
    error = string.Empty;

    if (!_backing.Exists(path))
    {
        error = "File not found.";
        return false;
    }

    var document = _backing.TryLoad(path, out var wasUnreadable);
    if (document is null)
    {
        error = wasUnreadable ? "File is not a valid Aethra preferences bundle." : "File was empty.";
        return false;
    }

    // ... existing mapping code ...
}
```

This is the **one** behavior change in Phase B's B1 sub-phase: the user-facing import error now distinguishes "empty" from "malformed". Worklog must call this out.

#### New tests

`tests/Aethra.Tests/Configuration/PreferencesProfilesStoreLoadSaveTests.cs` (NEW) — covers the public `Load()` / `Save(profiles)` paths via the in-memory file store seam. The existing `PreferencesProfilesStoreTests` only covers the `internal` path overloads; this PR adds the missing coverage.

`tests/Aethra.Tests/Configuration/InputBindingSettingsStoreLoadWithMigrationTests.cs` (NEW) — covers the public `LoadWithMigration()` path via the seam. Specifically:

* Returns defaults summary when the fake has no entry for the bindings path.
* Returns "unreadable" warning when `MarkUnreadable(path)` is set.
* Returns "empty" summary when the fake holds an empty list.
* Round-trips through migration when the fake holds a previously-saved set.

`tests/Aethra.Tests/Configuration/PreferencesProfileBundleExchangeTests.cs` (existing, 106 LOC) — extend to cover the new structured-warning behavior in `TryImportFromPath`.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke: launch the app, change a preference, close, relaunch — preference must persist. Then export a bundle, edit the JSON to make it malformed, re-import — the error message must say "not a valid Aethra preferences bundle" rather than "File was empty."

#### Rollback

Each store's diff is self-contained. The structured-warning behavior change is the only user-visible delta and is bounded to the import flow.

---

## 4. Sub-phase B3 — Command registry

### 4.1 The problem this sub-phase solves

`AethraCommandContext` (39-positional-`Action` constructor, 166 LOC) and `AethraCommandDispatcher` (39-case switch, 138 LOC) together require touching five places to add a command (architectural review §4 F3). The wiring at `MainWindow.xaml.cs:180-219` passes 39 positional `Action`s with no compiler-enforced binding — swapping any two adjacent slots silently misroutes commands at runtime. Phase A fixed two specific symptoms (`ToggleLoopFile` retired, `Quit`/`QuitWatchLater` decoupled). Phase B fixes the root cause.

### 4.2 Target shape

A small registry. Adding a command becomes a two-step operation: add a const to `AethraCommandIds`, register a handler at the call site by ID. The compiler enforces the type of the handler; the binding is by string so order doesn't matter. The dispatcher class collapses to ~20 LOC. `AethraCommandContext` is deleted.

```
src/Aethra/Commands/
├── AethraCommandIds.cs                 (unchanged — public-ish surface)
├── AethraCommandRegistry.cs            NEW (~50 LOC)
├── AethraCommandDispatcher.cs          (rewritten, ~25 LOC)
├── AethraCommandContext.cs             DELETED (was 166 LOC)
```

---

### 4.3 PR #B3a — Add `AethraCommandRegistry` (parallel surface)

**Closes:** F3 (foundation only). **Risk:** low. **Estimated diff:** +60 LOC new file, +100 LOC tests.

#### Implementation

`src/Aethra/Commands/AethraCommandRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Aethra.Commands;

/// <summary>
/// Replaces the 39-positional-Action AethraCommandContext. Handlers register
/// themselves by aethra:* command ID; the dispatcher looks them up by string.
/// Unknown IDs return false and are not invoked. Registering the same ID twice
/// throws — the wiring site is expected to be authoritative.
/// </summary>
internal sealed class AethraCommandRegistry
{
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.Ordinal);

    /// <summary>Register a handler for an aethra:* command ID. Throws if the ID is already registered.</summary>
    internal void Register(string commandId, Action handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(handler);

        if (_handlers.ContainsKey(commandId))
            throw new InvalidOperationException($"Command '{commandId}' is already registered.");

        _handlers[commandId] = handler;
    }

    /// <summary>True if a handler is registered for <paramref name="commandId"/>.</summary>
    internal bool Contains(string commandId) => _handlers.ContainsKey(commandId);

    /// <summary>
    /// Invoke the handler for <paramref name="commandId"/>. Returns true if a handler
    /// existed and was invoked; false if the ID is unknown.
    /// </summary>
    internal bool TryExecute(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId) || !_handlers.TryGetValue(commandId, out var handler))
            return false;

        handler();
        return true;
    }

    /// <summary>The IDs currently registered. Stable for diagnostic logging and tests.</summary>
    internal IReadOnlyCollection<string> RegisteredIds => _handlers.Keys;
}
```

#### Tests

`tests/Aethra.Tests/Commands/AethraCommandRegistryTests.cs`:

* `Register_ThrowsOnNullOrWhitespaceId`.
* `Register_ThrowsOnDuplicateId`.
* `TryExecute_ReturnsFalseForUnknownId`.
* `TryExecute_ReturnsTrueAndInvokesHandler` — registers a handler, calls `TryExecute`, asserts handler invoked exactly once.
* `TryExecute_AllowsRepeatedExecution` — same ID, called twice, handler runs twice.
* `Contains_TracksRegistration`.
* `RegisteredIds_ReflectsRegistrationOrderInsensitive` (Dictionary doesn't guarantee order; just assert membership).

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

#### Rollback

Single revert. No production consumer yet.

---

### 4.4 PR #B3b — Migrate dispatcher and `MainWindow` wiring; delete `AethraCommandContext`

**Closes:** F3 (root cause). **Risk:** medium. **Estimated diff:** −166 LOC (`AethraCommandContext.cs` deleted), −115 LOC (`AethraCommandDispatcher.cs` shrinks from 138 to ~25 LOC), ~50 LOC changed in `MainWindow.xaml.cs`, ~50 LOC changed in `AethraCommandDispatcherTests.cs`. Net diff: **~−250 LOC**.

#### Step 1 — rewrite `AethraCommandDispatcher.cs`

```csharp
namespace Aethra.Commands;

/// <summary>
/// Thin facade over AethraCommandRegistry, kept for symmetry with the prior surface
/// and to give the input runtime a single dispatcher reference per window.
/// </summary>
internal sealed class AethraCommandDispatcher
{
    private readonly AethraCommandRegistry _registry;

    internal AethraCommandDispatcher(AethraCommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    internal bool Execute(string command) => _registry.TryExecute(command);
}
```

#### Step 2 — update `MainWindow.xaml.cs:180-219`

Replace the 39-positional `new AethraCommandContext(...)` block with explicit registrations. Order of registration is irrelevant (registry is keyed by ID), but **alphabetize by command ID** for human scanability:

**Before** (lines 180-219):

```csharp
_commandDispatcher = new AethraCommandDispatcher(new AethraCommandContext(
    PausePlayback,
    MinimizeWindow,
    ToggleSettingsPanel,
    ToggleFullscreen,
    TogglePlayback,
    QuitDiscardingResumeFromCommand,    // (per Phase A #A6)
    CloseWindowFromCommand,             // (per Phase A #A6)
    () => SeekRelative(-5),
    // ... 31 more positional args ...
));
```

**After:**

```csharp
var registry = new AethraCommandRegistry();
registry.Register(AethraCommandIds.BossKey,                    () => { PausePlayback(); MinimizeWindow(); });
registry.Register(AethraCommandIds.CycleRepeat,                CycleRepeatMode);
registry.Register(AethraCommandIds.ExitOverlayOrFullscreen,    HandleEscapeCommand);
registry.Register(AethraCommandIds.MarkLoopA,                  ToggleLoopPointA);
registry.Register(AethraCommandIds.MarkLoopB,                  ToggleLoopPointB);
registry.Register(AethraCommandIds.NextFile,                   NavigateNextFileFromCommand);
registry.Register(AethraCommandIds.OpenFile,                   OpenFileFromCommand);
registry.Register(AethraCommandIds.OpenFolder,                 OpenFolderFromCommand);
registry.Register(AethraCommandIds.OpenRecent,                 OpenRecentFromCommand);
registry.Register(AethraCommandIds.PreviousFile,               NavigatePreviousFileFromCommand);
registry.Register(AethraCommandIds.Quit,                       QuitDiscardingResumeFromCommand);
registry.Register(AethraCommandIds.QuitWatchLater,             CloseWindowFromCommand);
registry.Register(AethraCommandIds.ResetLoop,                  ResetLoopPoints);
registry.Register(AethraCommandIds.SeekBack5,                  () => SeekRelative(-5));
registry.Register(AethraCommandIds.SeekBack10,                 () => SeekRelative(-10));
registry.Register(AethraCommandIds.SeekBack60,                 () => SeekRelative(-60));
registry.Register(AethraCommandIds.SeekBack300,                () => SeekRelative(-300));
registry.Register(AethraCommandIds.SeekForward5,               () => SeekRelative(5));
registry.Register(AethraCommandIds.SeekForward30,              () => SeekRelative(30));
registry.Register(AethraCommandIds.SeekForward60,              () => SeekRelative(60));
registry.Register(AethraCommandIds.SeekForward300,             () => SeekRelative(300));
registry.Register(AethraCommandIds.ShowFavorites,              ShowFavoritesFromCommand);
registry.Register(AethraCommandIds.ShowHelp,                   ShowHelpFromCommand);
registry.Register(AethraCommandIds.ShowPlaylist,               ShowPlaylistFromCommand);
registry.Register(AethraCommandIds.ShowTools,                  ShowToolsFromCommand);
registry.Register(AethraCommandIds.ToggleAdjustments,          ToggleAdjustmentsFromCommand);
registry.Register(AethraCommandIds.ToggleCommandRail,          ToggleCommandRailFromCommand);
registry.Register(AethraCommandIds.ToggleFullscreen,           ToggleFullscreen);
registry.Register(AethraCommandIds.ToggleMute,                 ToggleMute);
registry.Register(AethraCommandIds.TogglePlayPause,            TogglePlayback);
registry.Register(AethraCommandIds.ToggleSettings,             ToggleSettingsPanel);
registry.Register(AethraCommandIds.ToggleSubtitles,            ToggleSubtitles);
registry.Register(AethraCommandIds.VolumeDown2,                () => AddVolume(-2));
registry.Register(AethraCommandIds.VolumeDown5,                () => AddVolume(-5));
registry.Register(AethraCommandIds.VolumeDown10,               () => AddVolume(-10));
registry.Register(AethraCommandIds.VolumeUp2,                  () => AddVolume(2));
registry.Register(AethraCommandIds.VolumeUp5,                  () => AddVolume(5));
registry.Register(AethraCommandIds.VolumeUp10,                 () => AddVolume(10));
_commandDispatcher = new AethraCommandDispatcher(registry);
```

`BossKey` previously called both `PausePlayback` and `MinimizeWindow` via two separate Actions in the context (positions 1 and 2 of the dispatcher). The registry collapses this into one handler that does both.

The `BossKey` test in `AethraCommandDispatcherTests.cs:45` currently asserts `expectedActions = ["PausePlayback", "MinimizeWindow"]`. After this PR, it asserts a single `"BossKey"` invocation (or you can keep two separate actions and call them sequentially in the lambda; the test must agree).

#### Step 3 — delete `AethraCommandContext.cs`

```
D src/Aethra/Commands/AethraCommandContext.cs
```

#### Step 4 — rewrite `AethraCommandDispatcherTests.cs`

The test fixture today builds a 39-`Action` `AethraCommandContext` (lines 86-134). That whole helper is replaced by a registry built directly:

```csharp
private static (AethraCommandDispatcher Dispatcher, Dictionary<string, int> Invocations) CreateDispatcher()
{
    var invocations = new Dictionary<string, int>(StringComparer.Ordinal);
    var registry = new AethraCommandRegistry();

    void Register(string id) => registry.Register(id, () =>
    {
        invocations.TryGetValue(id, out var n);
        invocations[id] = n + 1;
    });

    foreach (var id in AllAethraCommandIds())
        Register(id);

    return (new AethraCommandDispatcher(registry), invocations);
}

private static IEnumerable<string> AllAethraCommandIds()
{
    // Reflect over AethraCommandIds for "every const string starting with aethra:".
    return typeof(AethraCommandIds)
        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Where(s => s.StartsWith("aethra:", StringComparison.Ordinal));
}
```

Existing test theory cases (`CommandCases`) collapse to:

```csharp
[Theory]
[MemberData(nameof(AllCommandIds))]
public void Execute_InvokesHandler_ForEachKnownCommandId(string commandId)
{
    var (dispatcher, invocations) = CreateDispatcher();

    var handled = dispatcher.Execute(commandId);

    Assert.True(handled, $"dispatcher should handle {commandId}");
    Assert.Equal(1, invocations.GetValueOrDefault(commandId));
    Assert.Equal(1, invocations.Values.Sum());
}

public static IEnumerable<object[]> AllCommandIds() =>
    AllAethraCommandIds().Select(id => new object[] { id });
```

The "distinct route for Quit vs QuitWatchLater" guard from Phase A #A6 stays as a separate explicit test:

```csharp
[Fact]
public void Quit_AndQuitWatchLater_RouteToDistinctHandlers()
{
    // Same fixture as above; verify each ID was invoked exactly once.
    // Reflection-based generation guarantees they were registered as distinct handlers.
    var (dispatcher, invocations) = CreateDispatcher();
    dispatcher.Execute(AethraCommandIds.Quit);
    dispatcher.Execute(AethraCommandIds.QuitWatchLater);
    Assert.Equal(1, invocations[AethraCommandIds.Quit]);
    Assert.Equal(1, invocations[AethraCommandIds.QuitWatchLater]);
}
```

The reflection helper also gives us a free **completeness test** that catches future divergence:

```csharp
[Fact]
public void EveryRegisteredCommandIsHandledByMainWindow_NotJustDispatcher()
{
    // This test cannot run inside the unit-test project because MainWindow needs the
    // WindowsAppRuntime. Mark [Fact(Skip="...")] and document that the equivalent
    // check is the build/run of the app: any AethraCommandIds member that MainWindow
    // does not Register() will throw at the first dispatch. Phase D will move this
    // into an integration test fixture.
}
```

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke: launch the app and exercise each binding category (mouse left/right click, keyboard `q`/`Q`/`SPACE`/`f`/seek arrows/volume arrows/`v` for subtitles). Every binding must behave exactly as before this PR. **No new behavior** — this is a pure refactor.

#### Risk and rollback

Risk: medium because the diff touches the most-trafficked file (`MainWindow.xaml.cs`). The risk is that an `AethraCommandIds` member is forgotten in the new registration block — at runtime that command silently no-ops (registry returns false). Mitigation: a startup-time assertion in `MainWindow.Initialize` (or in a debug-only check at the bottom of the registration block) compares `registry.RegisteredIds` against the reflection-derived full set:

```csharp
#if DEBUG
var allIds = typeof(AethraCommandIds)
    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
    .Where(f => f.IsLiteral && f.FieldType == typeof(string))
    .Select(f => (string)f.GetRawConstantValue()!)
    .Where(s => s.StartsWith("aethra:", StringComparison.Ordinal))
    .ToHashSet(StringComparer.Ordinal);
allIds.ExceptWith(registry.RegisteredIds);
if (allIds.Count > 0)
    throw new InvalidOperationException("Unregistered aethra:* command IDs: " + string.Join(", ", allIds));
#endif
```

This catches the omission at first run in a Debug build.

Rollback: single revert restores `AethraCommandContext.cs` and the dispatcher switch. Test rewrites revert with the production code.

---

## 5. Sub-phase B2 — Service interfaces and `Initialize` pattern

### 5.1 The problem this sub-phase solves

`MainWindow` and `FullSettingsPanel` reach into `PlaybackOptionsService.Instance` (lines `MainWindow.xaml.cs:140` and `FullSettingsPanel.xaml.cs:40`) and `AccentColorService.*` directly (16 call sites across the two files). The singletons are easy to hide a fake behind, but the consumers can't be verified — there's no DI seam, no constructor parameter, and the singleton state leaks across tests if any test ever touches them.

### 5.2 Strategy

Introduce two `internal` interfaces (`IPlaybackOptions`, `IAccentColors`). The production singletons implement them. `MainWindow` and `FullSettingsPanel` keep parameterless constructors (XAML requires this) but each gets an `Initialize(IPlaybackOptions, IAccentColors)` method that the host calls before showing the page. All field assignments today happen inside the constructor (`_playbackOptions = PlaybackOptionsService.Instance;`); they move into `Initialize` and assign from the parameters.

`App.OnLaunched` becomes the composition root. Tests can construct fakes and call `Initialize` directly — but the heavier "instantiate the page" tests still need WindowsAppRuntime. The win in Phase B is **the seam exists**, so Phase D can lift logic out of these pages into testable view-models with a clear boundary.

### 5.3 Folder shape after B2

```
src/Aethra/Services/
├── IPlaybackOptions.cs                 NEW
├── IAccentColors.cs                    NEW
├── AccentColorService.cs               (refactored: thin static shim over instance)
├── PlaybackOptionsService.cs           (now declares : IPlaybackOptions)
├── ... (other existing files unchanged)
```

---

### 5.4 PR #B2a — `IPlaybackOptions` interface; `PlaybackOptionsService` declares it

**Closes:** F7 (foundation only). **Risk:** low. **Estimated diff:** +60 LOC new interface, +2 LOC changed in `PlaybackOptionsService`.

#### Interface

`src/Aethra/Services/IPlaybackOptions.cs`:

```csharp
using System;
using Aethra.Profiles;

namespace Aethra.Services;

/// <summary>
/// The seam over PlaybackOptionsService. Mirrors the public surface the View layer
/// consumes today. PlaybackOptionsService.Instance remains the production binding;
/// tests can pass a fake to MainWindow/FullSettingsPanel via their Initialize(...).
/// </summary>
internal interface IPlaybackOptions
{
    VideoQualityPreset CurrentVideoQualityPreset { get; }
    ShaderChainPreset CurrentShaderPreset { get; }
    string CurrentCustomShaderChain { get; }

    event EventHandler<PlaybackPropertyApplyEventArgs>? PropertyApplyRequested;
    event EventHandler<VideoQualityPresetChangedEventArgs>? VideoQualityPresetChanged;
    event EventHandler<ShaderPresetChangedEventArgs>? ShaderPresetChanged;

    void ApplyNumericProperty(string property, double value);
    void ApplyStringProperty(string property, string value);
    void ApplyVideoQualityPreset(VideoQualityPreset preset);
    void ApplyShaderPreset(ShaderChainPreset preset);
    void ApplyCustomShaderChain(string chain);
    void ApplyPlaybackPreferences(PlaybackPreferencesProfile profile);
    void ApplyVideoPreferences(VideoPreferencesProfile profile);
    void ApplyVideoEnhancementPreferences(VideoPreferencesProfile profile);
    void ApplyAudioPreferences(AudioPreferencesProfile profile);
    void ApplySubtitlePreferences(SubtitlePreferencesProfile profile);
    void ApplyAdvancedPreferences(AdvancedPreferencesProfile profile);
    void ApplyNetworkPreferences(NetworkPreferencesProfile profile);
    void ApplyCustomizationPreferences(CustomizationPreferencesProfile profile);
}
```

The interface mirrors the surface used by `MainWindow.xaml.cs` (16 call sites verified) and `FullSettingsPanel.xaml.cs` (10 call sites verified). Every member is already implemented by `PlaybackOptionsService`.

#### `PlaybackOptionsService` change

Single-line:

```csharp
public sealed class PlaybackOptionsService : IPlaybackOptions
//                                         ^^^^^^^^^^^^^^^^^^^
```

The interface is `internal`; the class stays `public`. Implementing an internal interface from a public class is legal — the interface members are accessed via interface-typed references inside the assembly only.

#### Tests

`tests/Aethra.Tests/Services/IPlaybackOptionsTests.cs` — a tiny `FakePlaybackOptions : IPlaybackOptions` class that records every call. One test per surface method confirming the call lands. This is preparation for Phase D's view-model tests.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

#### Rollback

Single revert. No consumer change.

---

### 5.5 PR #B2b — `IAccentColors` interface; instance extraction; `Initialize` plumbed through

**Closes:** F7, F11. **Risk:** medium-high. **Estimated diff:** +90 LOC new interface, ~80 LOC changed in `AccentColorService`, ~50 LOC changed in `MainWindow.xaml.cs`, ~30 LOC changed in `FullSettingsPanel.xaml.cs`, ~10 LOC changed in `App.xaml.cs`.

#### Interface

`src/Aethra/Services/IAccentColors.cs`:

```csharp
using System;
using System.Collections.Generic;
using Windows.UI;

namespace Aethra.Services;

internal interface IAccentColors
{
    string CurrentHex { get; }

    event EventHandler<AccentColorChangedEventArgs>? AccentColorChanged;

    void Initialize();
    bool TryApplyHex(string input, out string normalizedHex);
    IReadOnlyList<string> LoadFavoriteHexColors();
    bool TryAddFavoriteHex(string input, out string normalizedHex);
    bool TryRemoveFavoriteHex(string input, out string normalizedHex);
    bool TryParseHexColor(string? input, out Color color, out string normalizedHex);
}
```

#### `AccentColorService` refactor

The existing `static class AccentColorService` becomes a thin shim. The actual logic moves into a new `internal sealed class AccentColorsImpl : IAccentColors` (in the same file). The shim's static methods/properties delegate to a `static internal IAccentColors _instance = new AccentColorsImpl();` field with a `SetInstanceForTests` seam.

The two static consts that other code reads (`DefaultAccentHex`, `MaxFavoriteAccentColors`) stay as public consts on the static shim — they're truly constant.

This preserves every existing static call site in `FullSettingsPanel`, `Profiles/PreferencesPageProfiles.cs:119`, and `App.xaml.cs:52` while letting `IAccentColors` be passed to `Initialize`.

#### `MainWindow.Initialize`

Add (immediately after the existing constructor):

```csharp
internal void Initialize(IPlaybackOptions playbackOptions, IAccentColors accentColors)
{
    ArgumentNullException.ThrowIfNull(playbackOptions);
    ArgumentNullException.ThrowIfNull(accentColors);

    _playbackOptions = playbackOptions;
    _accentColors = accentColors;

    _playbackOptions.PropertyApplyRequested += PlaybackOptions_PropertyApplyRequested;
    // The accent color subscription stays where it is today (line 1809) — it's set
    // up lazily when the loop accent gradient first needs to react to color changes.
}
```

Change the corresponding fields:

```csharp
// Before (lines 74-75):
private readonly PlaybackOptionsService _playbackOptions;
// (was set in the constructor at line 140)

// After:
private IPlaybackOptions _playbackOptions = null!; // set in Initialize
private IAccentColors _accentColors = null!;       // set in Initialize
```

Remove from the constructor (lines 140-141):

```csharp
_playbackOptions = PlaybackOptionsService.Instance;
_playbackOptions.PropertyApplyRequested += PlaybackOptions_PropertyApplyRequested;
```

The `null!` initializer is the standard C# pattern for "set in Initialize, not constructor." It documents the contract: callers must call `Initialize(...)` before `Activate()`.

#### `FullSettingsPanel.Initialize`

Add (immediately after the existing constructor):

```csharp
internal void Initialize(IPlaybackOptions playbackOptions, IAccentColors accentColors)
{
    ArgumentNullException.ThrowIfNull(playbackOptions);
    ArgumentNullException.ThrowIfNull(accentColors);

    _playbackOptions = playbackOptions;
    _accentColors = accentColors;

    _accentColors.AccentColorChanged += AccentColorService_AccentColorChanged;
    // (was: AccentColorService.AccentColorChanged += ...; at line 60)
}
```

Change the field:

```csharp
// Before (line 40):
private readonly PlaybackOptionsService _playbackOptions = PlaybackOptionsService.Instance;

// After:
private IPlaybackOptions _playbackOptions = null!;
private IAccentColors _accentColors = null!;
```

Remove the subscription from the constructor (line 60), move it into Initialize.

Replace each `AccentColorService.X` call site in the file with `_accentColors.X`. The 16 call sites listed in the surface map (§14 in this plan's prep notes) all become instance calls. The two const reads (`AccentColorService.DefaultAccentHex` at lines 285, 596) stay as static reads — they're truly constant.

#### `MainWindow_Closed` change

Update the unsubscribe at line 258:

```csharp
// Before:
AccentColorService.AccentColorChanged -= OnAccentColorChangedForLoopGradient;
// After:
_accentColors.AccentColorChanged -= OnAccentColorChangedForLoopGradient;
```

And similarly the subscribe at line 1809.

#### `App.OnLaunched` becomes the composition root

`src/Aethra/App.xaml.cs:46-53`:

**Before:**

```csharp
protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
{
    NativeRuntimeLoader.Install();

    _window = new MainWindow();
    _window.Activate();
    _window.DispatcherQueue.TryEnqueue(AccentColorService.Initialize);
}
```

**After:**

```csharp
protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
{
    NativeRuntimeLoader.Install();

    var playbackOptions = PlaybackOptionsService.Instance;
    var accentColors = AccentColorService.Instance;  // new — the IAccentColors instance

    var mainWindow = new MainWindow();
    mainWindow.Initialize(playbackOptions, accentColors);
    mainWindow.FullSettings.Initialize(playbackOptions, accentColors);
    _window = mainWindow;
    _window.Activate();
    _window.DispatcherQueue.TryEnqueue(accentColors.Initialize);
}
```

`MainWindow.FullSettings` is the XAML-named child — it's already accessible because XAML generates the field. Calling `Initialize` after `new MainWindow()` but before `Activate()` is the correct ordering: `InitializeComponent` has finished (the panel exists), but the window has not been shown.

#### Tests

`tests/Aethra.Tests/Services/IAccentColorsTests.cs` (NEW) — a `FakeAccentColors : IAccentColors` plus tests that confirm `MainWindow`'s subscription/unsubscription pattern is honored. The tests can't instantiate `MainWindow` (XAML pages need WindowsAppRuntime), but they can construct an `AccentColorsImpl` directly and verify the `AccentColorChanged` event fires when expected.

The existing `AccentColorServiceTests` (added in #B1d) keep working because the static shim still exposes `TryParseHexColor` etc.

#### Verification

```powershell
dotnet build .\Aethra.slnx -p:Platform=x64
dotnet test  .\Aethra.slnx -p:Platform=x64 --no-build
```

Manual smoke (required):

1. Launch the app — accent color must be applied immediately as before.
2. Open Preferences → Customization, change accent — UI must update across the entire window.
3. Pick a color, add to favorites, restart — favorites must persist.
4. Trigger a video that uses A/B loop gradient (mark loop A, observe gradient color); change accent; the gradient must update on the live frame.

If any of those break, the wiring of `AccentColorChanged` event subscriptions has drifted.

#### Risk and rollback

Risk: medium-high because it's the only Phase B PR that meaningfully touches `MainWindow`'s constructor wiring. The principal failure mode is forgetting to call `Initialize` in `App.OnLaunched`, which surfaces at first dispatch as a `NullReferenceException` from `_playbackOptions.X`. Mitigation: the `null!` field initializers are explicit; the `Initialize` method has `ArgumentNullException.ThrowIfNull` guards; smoke covers it.

Rollback: revert. The static shim around `AccentColorService` means a partial revert of just the App.xaml.cs/Initialize wiring would leave the static-method call sites still functional in `FullSettingsPanel` (they delegate to the same instance). Full revert restores the pre-B2 state cleanly.

---

## 6. Cross-cutting concerns and what Phase B explicitly does NOT do

### 6.1 Out of scope

* **No MVVM.** No `INotifyPropertyChanged`, no view-models, no XAML binding changes. That is Phase D.
* **No god-class breakup.** `MainWindow.xaml.cs` (3,153 LOC) and `FullSettingsPanel.xaml.cs` (1,957 LOC) stay roughly the same size. They get a small `Initialize` method appended; nothing else moves out. Phase D.
* **No GPU/native player changes.** That's Phase C.
* **No dropping of the static stores.** They keep their public API throughout Phase B; only their internals route through interfaces. Removing the static shims is out of scope and would be a Phase E cleanup.
* **No DI container.** We do not introduce Microsoft.Extensions.DependencyInjection or any other container. The composition root is `App.OnLaunched` and the wiring is hand-rolled — appropriate for an app this size.
* **No SDK upgrade, no NuGet additions, no XAML edits.**

### 6.2 What changes in `docs/development/worklog.md`

Each PR appends one entry, same template as Phase A.

### 6.3 What changes in `docs/architecture/agent-sitemap.md`

After the entire Phase B chain lands, append under "Important Entry Points":

```
- Settings/file persistence seams: `src/Aethra/Configuration/IO/`.
- Service interfaces over playback options and accent colors: `src/Aethra/Services/IPlaybackOptions.cs`, `src/Aethra/Services/IAccentColors.cs`.
- Command registry: `src/Aethra/Commands/AethraCommandRegistry.cs`.
- App composition root: `src/Aethra/App.xaml.cs` `OnLaunched` constructs services and calls `MainWindow.Initialize(...)`.
```

Remove the line referencing `AethraCommandContext` from the sitemap if present; the file no longer exists.

### 6.4 What changes in `docs/project/roadmap.md`

If the roadmap tracks Phase B by name, mark items B1/B2/B3 as shipped. Otherwise no edit.

---

## 7. Definition of done for Phase B

Phase B is complete when **all nine** boxes are true:

- [ ] PR #B1a merged: `ISettingsStore` + `LocalSettingsStore` + `InMemorySettingsStore` exist; CI green.
- [ ] PR #B1b merged: `IFileStore<T>` + `JsonFileStore<T>` + `InMemoryFileStore<T>` exist; CI green.
- [ ] PR #B1c merged: `PlaybackPersistenceStore` and `ScriptExtensionSettingsStore` route through `ISettingsStore`; new tests cover both stores' production paths; CI green.
- [ ] PR #B1d merged: `AccentColorService` storage half routes through `ISettingsStore`; new tests cover favorites/parse logic; CI green.
- [ ] PR #B1e merged: `PreferencesProfilesStore`, `InputBindingSettingsStore`, `PreferencesProfileBundleExchange` route through `IFileStore<T>`; new public-path tests added; user-facing import error distinguishes "empty" vs "malformed"; CI green.
- [ ] PR #B3a merged: `AethraCommandRegistry` exists with full test coverage; CI green.
- [ ] PR #B3b merged: dispatcher rewritten, MainWindow wiring migrated, `AethraCommandContext.cs` deleted, dispatcher tests rewritten; manual smoke confirms every binding still works; CI green.
- [ ] PR #B2a merged: `IPlaybackOptions` exists, `PlaybackOptionsService` implements it, `FakePlaybackOptions` test fake exists; CI green.
- [ ] PR #B2b merged: `IAccentColors` exists, `AccentColorService` refactored to instance behind shim, `MainWindow.Initialize` and `FullSettingsPanel.Initialize` exist, `App.OnLaunched` is the composition root; manual smoke confirms accent + playback + closing all behave as before; CI green.

After Phase B:

* Repo gains 6 new interfaces, 3 production implementations, 3 in-memory test fakes, and ~10 new test files.
* Repo loses ~250 LOC net (B3 dominates the savings; `AethraCommandContext` gone, `AethraCommandDispatcher` shrunk).
* The 4 stores that had **zero tests** now have meaningful coverage of their production Load/Save paths.
* Adding a new `aethra:*` command is now two places (const + register call), down from five.
* `MainWindow` and `FullSettingsPanel` have a documented dependency surface (`Initialize(IPlaybackOptions, IAccentColors)`); Phase D can lift logic out of them with confidence that the dependencies are explicit.
* The only user-visible behavior change in all of Phase B is the structured import error in `PreferencesProfileBundleExchange.TryImportFromPath` (B1e).

---

## 8. What to read before starting any PR

1. `docs/architecture/architectural-review-2026-05.md` — the diagnosis, especially §4 F3, F6, F7, F11.
2. `docs/architecture/phase-a-implementation-plan.md` — must be merged; B1 reuses `AtomicFile`.
3. `docs/development/copilot-instructions.md` — workflow rules.
4. `docs/project/DIRECTION.md` — non-negotiables (no NuGet adds, no XAML edits in this phase).
5. The target file(s) for the PR.
6. `docs/development/worklog.md` — most recent entry, in case something has shifted.

If anything in the target file has materially changed since 2026-05-02, **stop and amend this plan** before touching code.
