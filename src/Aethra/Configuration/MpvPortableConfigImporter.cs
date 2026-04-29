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
        var profileMergedOptions = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(mpvConfPath))
            ParseMpvOptions(mpvConfPath, options, unsupportedMpvRows, includedConfigFiles, profileMergedOptions);
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
            IncludedMpvConfigFiles = includedConfigFiles,
            ProfileMergedOptions = profileMergedOptions
        };
    }

    private static void ParseMpvOptions(
        string mpvConfPath,
        IDictionary<string, string> map,
        ICollection<string> unsupportedRows,
        ICollection<string> includedConfigFiles,
        IDictionary<string, IReadOnlyDictionary<string, string>> profileMergedOptions)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parsingStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? activeProfile = null;
        ParseMpvOptionsFile(
            Path.GetFullPath(mpvConfPath),
            map,
            unsupportedRows,
            includedConfigFiles,
            visited,
            parsingStack,
            includeSet,
            ref activeProfile);

        foreach (var profile in ComputeProfileMergedOptions(map, unsupportedRows))
        {
            profileMergedOptions[profile.Key] = profile.Value;
            foreach (var option in profile.Value)
                map[$"profile-merged:{profile.Key}:{option.Key}"] = option.Value;
        }
    }

    private static void ParseMpvOptionsFile(
        string filePath,
        IDictionary<string, string> map,
        ICollection<string> unsupportedRows,
        ICollection<string> includedConfigFiles,
        ISet<string> visited,
        ISet<string> parsingStack,
        ISet<string> includeSet,
        ref string? activeProfile)
    {
        if (parsingStack.Contains(filePath))
        {
            unsupportedRows.Add($"include-cycle:{filePath}");
            return;
        }

        if (!visited.Add(filePath))
            return;

        if (!File.Exists(filePath))
        {
            unsupportedRows.Add($"include-missing:{filePath}");
            return;
        }

        parsingStack.Add(filePath);
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

            if (TryParseIncludeLine(line, out var includePath, out var includeInvalid) && !includeInvalid)
            {
                var resolvedInclude = ResolveIncludePath(filePath, includePath);
                if (File.Exists(resolvedInclude) && includeSet.Add(resolvedInclude))
                    includedConfigFiles.Add(resolvedInclude);

                ParseMpvOptionsFile(
                    resolvedInclude,
                    map,
                    unsupportedRows,
                    includedConfigFiles,
                    visited,
                    parsingStack,
                    includeSet,
                    ref activeProfile);
                continue;
            }
            else if (includeInvalid)
            {
                unsupportedRows.Add($"include-invalid:{line}");
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

        parsingStack.Remove(filePath);
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

    private static bool TryParseIncludeLine(string line, out string includePath, out bool includeInvalid)
    {
        includePath = string.Empty;
        includeInvalid = false;

        var isIncludeDirective =
            line.StartsWith("include ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("include=", StringComparison.OrdinalIgnoreCase)
            || string.Equals(line, "include", StringComparison.OrdinalIgnoreCase);
        if (!isIncludeDirective)
            return false;

        var candidate = line.Length > "include".Length
            ? line["include".Length..].TrimStart(' ', '=').Trim()
            : string.Empty;
        includePath = candidate.Trim('"');
        includeInvalid = includePath.Length == 0;
        return true;
    }

    private static string ResolveIncludePath(string parentConfigPath, string includePath)
    {
        if (Path.IsPathRooted(includePath))
            return Path.GetFullPath(includePath);

        var parentDir = Path.GetDirectoryName(parentConfigPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(parentDir, includePath));
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> ComputeProfileMergedOptions(
        IDictionary<string, string> map,
        ICollection<string> unsupportedRows)
    {
        var profileOptions = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var profileParents = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in map)
        {
            if (!kvp.Key.StartsWith("profile:", StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = kvp.Key["profile:".Length..];
            var separator = remainder.IndexOf(':');
            if (separator <= 0 || separator >= remainder.Length - 1)
                continue;

            var profile = remainder[..separator].Trim();
            var optionKey = remainder[(separator + 1)..].Trim();
            if (profile.Length == 0 || optionKey.Length == 0)
                continue;

            if (!profileOptions.TryGetValue(profile, out var options))
            {
                options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                profileOptions[profile] = options;
            }

            options[optionKey] = kvp.Value;
            if (string.Equals(optionKey, "profile", StringComparison.OrdinalIgnoreCase))
                profileParents[profile] = SplitProfileParents(kvp.Value);
        }

        var globalOptions = map
            .Where(kvp => !kvp.Key.StartsWith("profile:", StringComparison.OrdinalIgnoreCase))
            .Where(kvp => !kvp.Key.StartsWith("profile-merged:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profileOptions.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase))
        {
            var merged = MergeProfile(profile, profileOptions, profileParents, globalOptions, cache, unsupportedRows, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            result[profile] = merged;
        }

        return result;
    }

    private static Dictionary<string, string> MergeProfile(
        string profile,
        IReadOnlyDictionary<string, Dictionary<string, string>> profileOptions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> profileParents,
        IReadOnlyDictionary<string, string> globalOptions,
        IDictionary<string, Dictionary<string, string>> cache,
        ICollection<string> unsupportedRows,
        ISet<string> visiting)
    {
        if (cache.TryGetValue(profile, out var cached))
            return new Dictionary<string, string>(cached, StringComparer.OrdinalIgnoreCase);

        if (!visiting.Add(profile))
        {
            unsupportedRows.Add($"profile-cycle:{profile}");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var merged = new Dictionary<string, string>(globalOptions, StringComparer.OrdinalIgnoreCase);
        if (profileParents.TryGetValue(profile, out var parents))
        {
            foreach (var parent in parents)
            {
                if (!profileOptions.ContainsKey(parent))
                {
                    unsupportedRows.Add($"profile-missing-base:{profile}:{parent}");
                    continue;
                }

                foreach (var parentOption in MergeProfile(parent, profileOptions, profileParents, globalOptions, cache, unsupportedRows, visiting))
                    merged[parentOption.Key] = parentOption.Value;
            }
        }

        if (profileOptions.TryGetValue(profile, out var localOptions))
        {
            foreach (var kvp in localOptions)
            {
                if (string.Equals(kvp.Key, "profile", StringComparison.OrdinalIgnoreCase))
                    continue;

                merged[kvp.Key] = kvp.Value;
            }
        }

        visiting.Remove(profile);
        cache[profile] = new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
        return merged;
    }

    private static IReadOnlyList<string> SplitProfileParents(string rawValue)
    {
        return rawValue
            .Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(parent => !string.IsNullOrWhiteSpace(parent))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
