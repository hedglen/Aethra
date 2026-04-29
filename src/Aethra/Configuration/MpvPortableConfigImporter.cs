using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aethra.Input;

namespace Aethra.Configuration;

public static class MpvPortableConfigImporter
{
    public static MpvImportedConfig Import(string portableConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(portableConfigDirectory))
            throw new ArgumentException("Portable config directory is required.", nameof(portableConfigDirectory));

        var sourceDirectory = Path.GetFullPath(portableConfigDirectory);
        var mpvConfPath = Path.Combine(sourceDirectory, "mpv.conf");
        var inputConfPath = Path.Combine(sourceDirectory, "input.conf");
        var shadersDir = Path.Combine(sourceDirectory, "shaders");
        var scriptsDir = Path.Combine(sourceDirectory, "scripts");

        var options = File.Exists(mpvConfPath)
            ? ParseMpvOptions(mpvConfPath)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (bindings, unsupportedRows) = File.Exists(inputConfPath)
            ? ParseInputBindings(inputConfPath)
            : (new List<InputBindingSetting>(), new List<string>());
        var shaders = Directory.Exists(shadersDir)
            ? Directory.GetFiles(shadersDir, "*.glsl", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();
        var scripts = Directory.Exists(scriptsDir)
            ? Directory.GetFiles(scriptsDir, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(scriptsDir, path).Replace('\\', '/'))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        return new MpvImportedConfig
        {
            SourceDirectory = sourceDirectory,
            MpvOptions = options,
            InputBindings = bindings,
            ShaderFiles = shaders,
            ScriptFiles = scripts,
            UnsupportedInputRows = unsupportedRows
        };
    }

    private static Dictionary<string, string> ParseMpvOptions(string mpvConfPath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? activeProfile = null;
        foreach (var rawLine in File.ReadAllLines(mpvConfPath))
        {
            var line = MpvConfigLineSupport.NormalizeLine(rawLine);
            if (line.Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                activeProfile = line[1..^1].Trim();
                continue;
            }

            if (!MpvConfigLineSupport.TryParseOptionLine(line, out var key, out var value))
                continue;

            if (string.IsNullOrWhiteSpace(activeProfile))
                map[key] = value;

            // Keep profile options too, under an explicit namespaced key.
            if (!string.IsNullOrWhiteSpace(activeProfile))
                map[$"profile:{activeProfile}:{key}"] = value;
        }

        return map;
    }

    private static (List<InputBindingSetting> Bindings, List<string> UnsupportedRows) ParseInputBindings(string inputConfPath)
    {
        var bindings = new List<InputBindingSetting>();
        var unsupportedRows = new List<string>();

        foreach (var rawLine in File.ReadAllLines(inputConfPath))
        {
            var line = MpvConfigLineSupport.NormalizeLine(rawLine);
            if (line.Length == 0)
                continue;

            if (!MpvConfigLineSupport.TryParseInputBindingLine(line, out var gesture, out var command))
            {
                unsupportedRows.Add(line);
                continue;
            }

            var category = CategorizeGesture(gesture);
            bindings.Add(new InputBindingSetting(category, gesture, command, $"Imported from {Path.GetFileName(inputConfPath)}", "Imported"));
        }

        return (bindings, unsupportedRows);
    }

    private static string CategorizeGesture(string gesture)
    {
        if (gesture.StartsWith("MBTN_", StringComparison.OrdinalIgnoreCase)
            || gesture.StartsWith("WHEEL_", StringComparison.OrdinalIgnoreCase))
        {
            return "Mouse";
        }

        if (gesture.Contains("KP", StringComparison.OrdinalIgnoreCase))
            return "Scimitar";

        return "Imported";
    }
}
