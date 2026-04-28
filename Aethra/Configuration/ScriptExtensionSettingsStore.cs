using Windows.Storage;

namespace Aethra.Configuration;

public static class ScriptExtensionSettingsStore
{
    private const string ScriptsEnabledKey = "Extensions.ScriptsEnabled";
    private const string ScriptsFolderKey = "Extensions.ScriptsFolder";
    private const string PortableConfigPathKey = "Import.PortableConfigPath";

    public static bool ScriptsEnabled
    {
        get
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(ScriptsEnabledKey, out var value) && value is bool enabled)
                return enabled;

            return false;
        }
        set => ApplicationData.Current.LocalSettings.Values[ScriptsEnabledKey] = value;
    }

    public static string ScriptsFolder
    {
        get
        {
            var value = ApplicationData.Current.LocalSettings.Values[ScriptsFolderKey] as string;
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        set => ApplicationData.Current.LocalSettings.Values[ScriptsFolderKey] = value ?? string.Empty;
    }

    public static string PortableConfigPath
    {
        get
        {
            var value = ApplicationData.Current.LocalSettings.Values[PortableConfigPathKey] as string;
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        set => ApplicationData.Current.LocalSettings.Values[PortableConfigPathKey] = value ?? string.Empty;
    }
}
