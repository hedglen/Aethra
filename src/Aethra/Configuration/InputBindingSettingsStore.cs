using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aethra.Commands;
using Aethra.Input;
using Windows.Storage;

namespace Aethra.Configuration;

public static class InputBindingSettingsStore
{
    private const string BindingsFileName = "input-bindings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static IReadOnlyList<InputBindingSetting> Load(IEnumerable<InputBindingSetting> defaults)
    {
        return LoadWithMigration(defaults).Bindings;
    }

    public static InputBindingLoadResult LoadWithMigration(
        IEnumerable<InputBindingSetting> defaults,
        IEnumerable<InputBindingSetting>? legacyDefaults = null)
    {
        var defaultRows = defaults
            .Select(CloneBinding)
            .ToList();
        var path = GetBindingsFilePath();
        if (!File.Exists(path))
        {
            return new InputBindingLoadResult(
                defaultRows,
                wasMigrated: false,
                summary: "Using bundled defaults.",
                warnings: Array.Empty<string>());
        }

        List<InputBindingSetting>? existingRows;
        try
        {
            var json = File.ReadAllText(path);
            existingRows = JsonSerializer.Deserialize<List<InputBindingSetting>>(json, JsonOptions);
        }
        catch
        {
            return new InputBindingLoadResult(
                defaultRows,
                wasMigrated: false,
                summary: "Saved bindings were unreadable; using bundled defaults.",
                warnings: new[] { "Saved binding file was unreadable and could not be migrated." });
        }

        if (existingRows is null || existingRows.Count == 0)
        {
            return new InputBindingLoadResult(
                defaultRows,
                wasMigrated: false,
                summary: "Saved bindings were empty; using bundled defaults.",
                warnings: Array.Empty<string>());
        }

        var result = ApplyMigrationForRows(defaultRows, existingRows, legacyDefaults);
        if (result.WasMigrated)
            Save(result.Bindings);
        return result;
    }

    internal static InputBindingLoadResult ApplyMigrationForRows(
        IEnumerable<InputBindingSetting> defaults,
        IEnumerable<InputBindingSetting> existingRows,
        IEnumerable<InputBindingSetting>? legacyDefaults = null)
    {
        var defaultRows = defaults
            .Select(CloneBinding)
            .ToList();
        var existing = existingRows
            .Select(CloneBinding)
            .ToList();
        var legacyLookup = BuildLegacyLookup(legacyDefaults ?? Array.Empty<InputBindingSetting>());
        var (migratedRows, warnings, replacedLegacyCount, preservedUserCount, unresolvedCount) =
            MigrateRows(defaultRows, existing, legacyLookup);
        var spaceForced = ApplyForcedSpaceBossKeyPolicy(defaultRows, migratedRows);

        var changed = !RowsEquivalent(existing, migratedRows);
        var summary = changed
            ? $"Migrated input bindings: preserved {preservedUserCount} user row(s), replaced {replacedLegacyCount} legacy row(s), retained {unresolvedCount} unresolved row(s)."
            : "Loaded existing bindings without migration changes.";
        if (spaceForced)
            summary = $"{summary} Forced SPACE to {AethraCommandIds.BossKey}.";

        return new InputBindingLoadResult(migratedRows, changed, summary, warnings);
    }

    public static void Save(IEnumerable<InputBindingSetting> bindings)
    {
        var rows = bindings
            .Select(CloneBinding)
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture) && !string.IsNullOrWhiteSpace(binding.Command))
            .ToList();
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        AtomicFile.WriteAllText(GetBindingsFilePath(), json);
    }

    public static string ExportToInputConf(IEnumerable<InputBindingSetting> bindings)
    {
        var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "input.conf");
        var lines = bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture) && !string.IsNullOrWhiteSpace(binding.Command))
            .Select(binding => $"{binding.Gesture.Trim()} {binding.Command.Trim()}");
        AtomicFile.WriteAllLines(path, lines);
        return path;
    }

    public static IReadOnlyList<InputBindingSetting> ImportFromInputConf(string inputConfPath)
    {
        if (string.IsNullOrWhiteSpace(inputConfPath) || !File.Exists(inputConfPath))
            return Array.Empty<InputBindingSetting>();

        var imported = new List<InputBindingSetting>();
        foreach (var rawLine in File.ReadAllLines(inputConfPath))
        {
            var line = MpvConfigLineSupport.NormalizeLine(rawLine);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!MpvConfigLineSupport.TryParseInputBindingLine(line, out var gesture, out var command))
                continue;

            imported.Add(new InputBindingSetting(
                "Imported",
                gesture,
                command,
                "Imported from input.conf",
                "input.conf"));
        }

        return imported;
    }

    private static (List<InputBindingSetting> Rows, List<string> Warnings, int ReplacedLegacyCount, int PreservedUserCount, int UnresolvedCount)
        MigrateRows(
            IReadOnlyList<InputBindingSetting> defaults,
            IReadOnlyList<InputBindingSetting> existing,
            IReadOnlyDictionary<string, HashSet<string>> legacyLookup)
    {
        var warnings = new List<string>();
        var rowByKey = new Dictionary<string, InputBindingSetting>(StringComparer.Ordinal);
        var keyOrder = new List<string>();

        foreach (var defaultRow in defaults)
        {
            var row = CloneBinding(defaultRow);
            if (!TryGetGestureKey(row.Gesture, out var key))
                continue;

            if (!rowByKey.ContainsKey(key))
                keyOrder.Add(key);

            rowByKey[key] = row;
        }

        var unresolved = new List<InputBindingSetting>();
        var replacedLegacyCount = 0;
        var preservedUserCount = 0;

        foreach (var existingRow in existing)
        {
            var row = CloneBinding(existingRow);
            if (string.IsNullOrWhiteSpace(row.Gesture) || string.IsNullOrWhiteSpace(row.Command))
                continue;

            if (!TryGetGestureKey(row.Gesture, out var key))
            {
                unresolved.Add(row);
                warnings.Add($"Unparsed gesture retained: {row.Gesture}");
                continue;
            }

            var normalizedCommand = NormalizeCommand(row.Command);
            if (IsLegacyStockRow(legacyLookup, key, normalizedCommand))
            {
                replacedLegacyCount++;
                continue;
            }

            if (!keyOrder.Any(existingKey => string.Equals(existingKey, key, StringComparison.Ordinal)))
                keyOrder.Add(key);

            rowByKey[key] = row;
            preservedUserCount++;

            if (InputCommandSupport.TryGetUnsupportedReason(row.Command, out var reason))
                warnings.Add($"{row.Gesture}: {reason}");
        }

        var migrated = keyOrder
            .Where(rowByKey.ContainsKey)
            .Select(key => rowByKey[key])
            .ToList();
        migrated.AddRange(unresolved);

        return (migrated, warnings.Distinct(StringComparer.Ordinal).ToList(), replacedLegacyCount, preservedUserCount, unresolved.Count);
    }

    private static bool ApplyForcedSpaceBossKeyPolicy(
        IReadOnlyList<InputBindingSetting> defaults,
        List<InputBindingSetting> rows)
    {
        if (!TryGetGestureKey("SPACE", out var spaceKey))
            return false;

        var desired = defaults.FirstOrDefault(defaultRow =>
        {
            if (!TryGetGestureKey(defaultRow.Gesture, out var key))
                return false;

            return string.Equals(key, spaceKey, StringComparison.Ordinal);
        }) ?? new InputBindingSetting(
            "General",
            "SPACE",
            AethraCommandIds.BossKey,
            "BOSS KEY: pause and minimize",
            "Policy");

        var desiredRow = CloneBinding(desired);
        desiredRow.Command = AethraCommandIds.BossKey;

        var firstSpaceIndex = -1;
        var changed = false;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!TryGetGestureKey(row.Gesture, out var key)
                || !string.Equals(key, spaceKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (firstSpaceIndex < 0)
            {
                firstSpaceIndex = index;
                if (!RowEquivalent(row, desiredRow))
                {
                    rows[index] = CloneBinding(desiredRow);
                    changed = true;
                }
                continue;
            }

            rows.RemoveAt(index);
            index--;
            changed = true;
        }

        if (firstSpaceIndex >= 0)
            return changed;

        rows.Add(CloneBinding(desiredRow));
        return true;
    }

    private static IReadOnlyDictionary<string, HashSet<string>> BuildLegacyLookup(IEnumerable<InputBindingSetting> legacyDefaults)
    {
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var row in legacyDefaults)
        {
            if (!TryGetGestureKey(row.Gesture, out var key))
                continue;

            if (!lookup.TryGetValue(key, out var commands))
            {
                commands = new HashSet<string>(StringComparer.Ordinal);
                lookup[key] = commands;
            }

            commands.Add(NormalizeCommand(row.Command));
        }

        return lookup;
    }

    private static bool IsLegacyStockRow(IReadOnlyDictionary<string, HashSet<string>> legacyLookup, string key, string normalizedCommand)
    {
        if (!legacyLookup.TryGetValue(key, out var commands))
            return false;

        return commands.Contains(normalizedCommand);
    }

    private static bool RowsEquivalent(IReadOnlyList<InputBindingSetting> left, IReadOnlyList<InputBindingSetting> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!RowEquivalent(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool RowEquivalent(InputBindingSetting left, InputBindingSetting right)
    {
        return string.Equals(left.Category, right.Category, StringComparison.Ordinal)
               && string.Equals(left.Gesture, right.Gesture, StringComparison.Ordinal)
               && string.Equals(left.Command, right.Command, StringComparison.Ordinal)
               && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
               && string.Equals(left.Source, right.Source, StringComparison.Ordinal);
    }

    private static bool TryGetGestureKey(string gesture, out string key)
    {
        key = string.Empty;
        if (!InputRuntimeService.TryNormalizeGestureKey(gesture, out key))
            return false;

        return true;
    }

    private static string NormalizeCommand(string command)
    {
        return (command ?? string.Empty).Trim();
    }

    private static string GetBindingsFilePath()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, BindingsFileName);
    }

    private static InputBindingSetting CloneBinding(InputBindingSetting binding)
    {
        return new InputBindingSetting(
            binding.Category ?? string.Empty,
            binding.Gesture ?? string.Empty,
            binding.Command ?? string.Empty,
            binding.Description ?? string.Empty,
            binding.Source ?? string.Empty);
    }
}
