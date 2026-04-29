using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aethra.Profiles;

namespace Aethra.Configuration;

public sealed class PreferencesProfileBundleExchangeDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ExportedAtUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string ActiveProfileName { get; set; } = "Default";
    public List<NamedPreferencesProfileBundle> Bundles { get; set; } = new();
}

public static class PreferencesProfileBundleExchange
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void ExportToPath(string path, ProfilesPreferencesProfile profiles)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var document = new PreferencesProfileBundleExchangeDocument
        {
            ActiveProfileName = string.IsNullOrWhiteSpace(profiles.ActiveProfileName) ? "Default" : profiles.ActiveProfileName.Trim(),
            Bundles = profiles.Bundles.Select(CloneBundle).ToList()
        };
        if (document.Bundles.Count == 0)
            document.Bundles.Add(NamedPreferencesProfileBundle.CreateDefault());

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public static bool TryImportFromPath(string path, out ProfilesPreferencesProfile profiles, out string error)
    {
        profiles = ProfilesPreferencesProfile.CreateDefault();
        error = string.Empty;

        if (!File.Exists(path))
        {
            error = "File not found.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<PreferencesProfileBundleExchangeDocument>(json, JsonOptions);
            if (document is null)
            {
                error = "Invalid or empty profile bundle document.";
                return false;
            }

            if (document.SchemaVersion != 1)
            {
                error = $"Unsupported schema version: {document.SchemaVersion}.";
                return false;
            }

            var importedBundles = NormalizeBundles(document.Bundles);
            if (importedBundles.Count == 0)
            {
                error = "No profile bundles were found in the document.";
                return false;
            }

            profiles.Bundles = importedBundles;
            var requestedActive = string.IsNullOrWhiteSpace(document.ActiveProfileName) ? "Default" : document.ActiveProfileName.Trim();
            profiles.ActiveProfileName = importedBundles.Any(bundle => string.Equals(bundle.Name, requestedActive, StringComparison.OrdinalIgnoreCase))
                ? requestedActive
                : importedBundles[0].Name;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static List<NamedPreferencesProfileBundle> NormalizeBundles(List<NamedPreferencesProfileBundle>? bundles)
    {
        var normalized = new List<NamedPreferencesProfileBundle>();
        if (bundles is null)
            return normalized;

        foreach (var bundle in bundles)
        {
            var clone = CloneBundle(bundle);
            var baseName = string.IsNullOrWhiteSpace(clone.Name) ? "Profile" : clone.Name.Trim();
            var name = baseName;
            var suffix = 2;
            while (normalized.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName} {suffix}";
                suffix++;
            }

            clone.Name = name;
            normalized.Add(clone);
        }

        return normalized;
    }

    private static NamedPreferencesProfileBundle CloneBundle(NamedPreferencesProfileBundle source)
    {
        return new NamedPreferencesProfileBundle
        {
            Name = source.Name,
            Playback = new PlaybackPreferencesProfile
            {
                ResumeWhereLeftOff = source.Playback.ResumeWhereLeftOff,
                AutoplayOnOpen = source.Playback.AutoplayOnOpen,
                EndOfFileAction = source.Playback.EndOfFileAction,
                DefaultPlaybackSpeedPercent = source.Playback.DefaultPlaybackSpeedPercent
            },
            Video = new VideoPreferencesProfile
            {
                OutputMode = source.Video.OutputMode,
                HardwareDecode = source.Video.HardwareDecode,
                InterpolationEnabled = source.Video.InterpolationEnabled,
                DeinterlaceEnabled = source.Video.DeinterlaceEnabled,
                QualityPreset = source.Video.QualityPreset,
                ShaderPreset = source.Video.ShaderPreset,
                CustomShaderChain = source.Video.CustomShaderChain
            },
            Audio = new AudioPreferencesProfile
            {
                OutputDevice = source.Audio.OutputDevice,
                DynamicRangeCompression = source.Audio.DynamicRangeCompression,
                ReplayGainNormalization = source.Audio.ReplayGainNormalization,
                ChannelLayout = source.Audio.ChannelLayout
            },
            Subtitles = new SubtitlePreferencesProfile
            {
                AutoLoadMatchingSubtitles = source.Subtitles.AutoLoadMatchingSubtitles,
                PreferredLanguagesCsv = source.Subtitles.PreferredLanguagesCsv,
                FontSize = source.Subtitles.FontSize,
                BorderAndShadow = source.Subtitles.BorderAndShadow,
                SubtitleDelaySeconds = source.Subtitles.SubtitleDelaySeconds
            },
            Library = new LibraryPreferencesProfile
            {
                WatchFoldersEnabled = source.Library.WatchFoldersEnabled,
                RememberRecentFiles = source.Library.RememberRecentFiles
            },
            Advanced = new AdvancedPreferencesProfile
            {
                LogLevel = source.Advanced.LogLevel,
                ExtraMpvOptionsText = source.Advanced.ExtraMpvOptionsText
            },
            Network = new NetworkPreferencesProfile
            {
                PreferIpv6 = source.Network.PreferIpv6,
                AllowMeteredConnections = source.Network.AllowMeteredConnections,
                NetworkTimeoutSeconds = source.Network.NetworkTimeoutSeconds,
                ProxyMode = source.Network.ProxyMode,
                ProxyUrl = source.Network.ProxyUrl
            },
            Customization = new CustomizationPreferencesProfile
            {
                AccentHex = source.Customization.AccentHex,
                UseSystemTheme = source.Customization.UseSystemTheme,
                DenseLayout = source.Customization.DenseLayout,
                ShowPlaybackHud = source.Customization.ShowPlaybackHud
            }
        };
    }
}
