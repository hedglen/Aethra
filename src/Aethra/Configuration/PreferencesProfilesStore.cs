using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aethra.Profiles;
using Windows.Storage;

namespace Aethra.Configuration;

public static class PreferencesProfilesStore
{
    private const string FileName = "preferences-profiles.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static PreferencesPageProfiles Load()
    {
        var path = GetStorePath();
        return LoadFromPath(path);
    }

    public static void Save(PreferencesPageProfiles profiles)
    {
        var path = GetStorePath();
        SaveToPath(path, profiles);
    }

    internal static PreferencesPageProfiles LoadFromPath(string path)
    {
        if (!File.Exists(path))
            return PreferencesPageProfiles.CreateDefault();

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<PreferencesPageProfiles>(json, JsonOptions);
            if (loaded is null)
                return PreferencesPageProfiles.CreateDefault();

            NormalizeSubtitleFontSizes(loaded);
            return loaded;
        }
        catch
        {
            return PreferencesPageProfiles.CreateDefault();
        }
    }

    internal static void SaveToPath(string path, PreferencesPageProfiles profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static string GetStorePath()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
    }

    private static void NormalizeSubtitleFontSizes(PreferencesPageProfiles profiles)
    {
        var bundles = profiles.Profiles.Bundles;
        if (bundles is null)
            return;

        foreach (var bundle in bundles)
        {
            if (bundle?.Subtitles is null)
                continue;

            var fontSize = bundle.Subtitles.FontSize;
            if (!double.IsFinite(fontSize))
            {
                bundle.Subtitles.FontSize = SubtitlePreferencesProfile.CreateDefault().FontSize;
                continue;
            }

            bundle.Subtitles.FontSize = Math.Clamp(fontSize, 14, 28);
        }
    }
}
