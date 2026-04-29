# mpv.net Reuse Intake Map

This file records direct C# source reuse/adaptation from `mpvnet-player/mpv.net` into Aethra.

## Intake Baseline

- Upstream repository: `https://github.com/mpvnet-player/mpv.net`
- Upstream license: GPL-2.0
- Intake commit baseline: `ef45baecbdd8e0a249eca9a621fe608143f75c4b`

## Source -> Target Mapping

1) `mpv.net/src/MpvNet/Player.cs`
- Upstream behavior reused: config line normalization used when reading text config files (trim handling, comment stripping behavior).
- Aethra targets:
  - `src/Aethra/Configuration/MpvConfigLineSupport.cs`
  - `src/Aethra/Configuration/InputBindingSettingsStore.cs`
  - `src/Aethra/Configuration/MpvPortableConfigImporter.cs`
- Adaptation notes:
  - Applied to `input.conf` import path (gesture + command rows) and `mpv.conf` import path.
  - Includes permissive boolean shorthand support (e.g. `deband` -> `deband=yes`) and inline-comment trimming.
  - Keeps profile-section options under namespaced keys (`profile:<name>:<key>`) for explicit round-trip visibility.
  - Added Aethra model mapping to `InputBindingSetting`.
  - Preserved provenance in-code with a source comment.

2) `mpv.net/src/MpvNet/Player.cs` (same intake chunk)
- Upstream behavior reused: permissive config row handling for user-maintained text config files.
- Aethra targets:
  - `src/Aethra/Views/FullSettingsPanel.xaml`
  - `src/Aethra/Preferences/FullSettingsPanel.xaml.cs`
- Adaptation notes:
  - Added explicit `input.conf` import controls in Preferences > Input.
  - Wires parser output into Aethra input-binding runtime/persistence flow.
  - Adds import status reporting for commands blocked by Aethra runtime safety policy.

3) `mpv.net` command execution model (behavioral alignment, no direct file transplant)
- Upstream behavior aligned: execute parsed mpv commands as argv vectors via backend command queue rather than hard-coding many verb-specific handlers.
- Aethra targets:
  - `src/Aethra/Input/MpvCommandLineParser.cs`
  - `src/Aethra/Views/MainWindow.xaml.cs`
  - `src/Aethra/Native/INativeMpvPlayerBackend.cs`
  - `src/Aethra/Native/NativeMpvSoftwarePlayer.cs`
  - `src/Aethra/Native/NativeMpvOpenGlPlayer.cs`
  - `src/Aethra/Input/InputCommandSupport.cs`
- Adaptation notes:
  - Keeps `aethra:*` commands first-class and preserves native alias handling for fullscreen/settings/playlist.
  - Uses a denylist safety policy for risky verbs (`run`, `subprocess`, `script-message-to`) in runtime/validation surfaces.
  - Preserves non-blocking command dispatch by routing through existing backend queues.

## Verification Scope

- Automated: `tests/Aethra.Tests/Configuration/InputBindingSettingsStoreTests.cs`
  - parses valid rows
  - strips comments
  - ignores invalid/missing inputs
- Automated: `tests/Aethra.Tests/Configuration/MpvPortableConfigImporterTests.cs`
  - parses mpv option shorthand, profile-scoped options, and invalid input rows
- Automated: `tests/Aethra.Tests/Input/MpvCommandLineParserTests.cs`
  - verifies semicolon splitting and quote safety
- Automated: `tests/Aethra.Tests/Input/InputCommandSupportTests.cs`
  - verifies safe-passthrough support and denylist behavior
- Manual: Preferences > Input -> import a real `input.conf`, verify rows, save, and execute.
