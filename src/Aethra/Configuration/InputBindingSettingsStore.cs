using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        var fallback = defaults
            .Select(CloneBinding)
            .ToList();
        var path = GetBindingsFilePath();
        if (!File.Exists(path))
            return fallback;

        try
        {
            var json = File.ReadAllText(path);
            var bindings = JsonSerializer.Deserialize<List<InputBindingSetting>>(json, JsonOptions);
            if (bindings is null || bindings.Count == 0)
                return fallback;

            return bindings;
        }
        catch
        {
            return fallback;
        }
    }

    public static void Save(IEnumerable<InputBindingSetting> bindings)
    {
        var rows = bindings
            .Select(CloneBinding)
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture) && !string.IsNullOrWhiteSpace(binding.Command))
            .ToList();
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        File.WriteAllText(GetBindingsFilePath(), json, Encoding.UTF8);
    }

    public static string ExportToInputConf(IEnumerable<InputBindingSetting> bindings)
    {
        var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "input.conf");
        var lines = bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture) && !string.IsNullOrWhiteSpace(binding.Command))
            .Select(binding => $"{binding.Gesture.Trim()} {binding.Command.Trim()}");
        File.WriteAllLines(path, lines);
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
