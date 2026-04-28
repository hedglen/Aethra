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
        foreach (var rawLine in File.ReadAllLines(mpvConfPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('['))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Length == 0)
                continue;

            map[key] = value;
        }

        return map;
    }

    private static (List<InputBindingSetting> Bindings, List<string> UnsupportedRows) ParseInputBindings(string inputConfPath)
    {
        var bindings = new List<InputBindingSetting>();
        var unsupportedRows = new List<string>();

        foreach (var rawLine in File.ReadAllLines(inputConfPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                unsupportedRows.Add(line);
                continue;
            }

            var gesture = parts[0].Trim();
            var command = parts[1].Trim();
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
