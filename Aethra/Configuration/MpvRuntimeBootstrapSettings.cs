using System;
using System.Collections.Generic;

namespace Aethra.Configuration;

public sealed class MpvRuntimeBootstrapSettings
{
    private readonly Dictionary<string, string> _importedMpvOptions = new(StringComparer.OrdinalIgnoreCase);

    private MpvRuntimeBootstrapSettings()
    {
    }

    public static MpvRuntimeBootstrapSettings Instance { get; } = new();

    public string PortableConfigDirectory { get; private set; } = string.Empty;

    public IReadOnlyDictionary<string, string> ImportedMpvOptions => _importedMpvOptions;

    public void ApplyImportedConfig(MpvImportedConfig importedConfig)
    {
        PortableConfigDirectory = importedConfig.SourceDirectory;
        _importedMpvOptions.Clear();
        foreach (var option in importedConfig.MpvOptions)
            _importedMpvOptions[option.Key] = option.Value;
    }
}
