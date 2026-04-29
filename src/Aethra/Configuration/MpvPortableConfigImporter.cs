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

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unsupportedMpvRows = new List<string>();
        var includedConfigFiles = new List<string>();
        if (File.Exists(mpvConfPath))
            ParseMpvOptions(mpvConfPath, options, unsupportedMpvRows, includedConfigFiles);
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
            UnsupportedInputRows = unsupportedRows,
            UnsupportedMpvRows = unsupportedMpvRows,
            IncludedMpvConfigFiles = includedConfigFiles
        };
    }

    private static void ParseMpvOptions(
        string mpvConfPath,
        IDictionary<string, string> map,
        ICollection<string> unsupportedRows,
        ICollection<string> includedConfigFiles)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ParseMpvOptionsFile(Path.GetFullPath(mpvConfPath), map, unsupportedRows, includedConfigFiles, visited, activeProfile: null);
    }

    private static string? ParseMpvOptionsFile(
        string filePath,
        IDictionary<string, string> map,
        ICollection<string> unsupportedRows,
        ICollection<string> includedConfigFiles,
        ISet<string> visited,
        string? activeProfile)
    {
        if (!visited.Add(filePath))
            return activeProfile;

        if (!File.Exists(filePath))
        {
            unsupportedRows.Add($"include-missing:{filePath}");
            return activeProfile;
        }

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = MpvConfigLineSupport.NormalizeLine(rawLine);
            if (line.Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                activeProfile = line[1..^1].Trim();
                continue;
            }

            if (TryParseIncludeLine(line, out var includePath))
            {
                var resolvedInclude = ResolveIncludePath(filePath, includePath);
                includedConfigFiles.Add(resolvedInclude);
                activeProfile = ParseMpvOptionsFile(resolvedInclude, map, unsupportedRows, includedConfigFiles, visited, activeProfile);
                continue;
            }

            if (!MpvConfigLineSupport.TryParseOptionLine(line, out var key, out var value))
            {
                unsupportedRows.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(activeProfile))
                map[key] = value;

            // Keep profile options too, under an explicit namespaced key.
            if (!string.IsNullOrWhiteSpace(activeProfile))
                map[$"profile:{activeProfile}:{key}"] = value;
        }

        return activeProfile;
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

    private static bool TryParseIncludeLine(string line, out string includePath)
    {
        includePath = string.Empty;
        if (!line.StartsWith("include ", StringComparison.OrdinalIgnoreCase))
            return false;

        includePath = line["include ".Length..].Trim().Trim('"');
        return includePath.Length > 0;
    }

    private static string ResolveIncludePath(string parentConfigPath, string includePath)
    {
        if (Path.IsPathRooted(includePath))
            return Path.GetFullPath(includePath);

        var parentDir = Path.GetDirectoryName(parentConfigPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(parentDir, includePath));
    }
}
