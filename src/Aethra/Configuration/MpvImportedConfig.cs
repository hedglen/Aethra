using System.Collections.Generic;
using Aethra.Input;

namespace Aethra.Configuration;

public sealed class MpvImportedConfig
{
    public string SourceDirectory { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> MpvOptions { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<InputBindingSetting> InputBindings { get; init; } = new List<InputBindingSetting>();

    public IReadOnlyList<string> ShaderFiles { get; init; } = new List<string>();

    public IReadOnlyList<string> ScriptFiles { get; init; } = new List<string>();

    public IReadOnlyList<string> UnsupportedInputRows { get; init; } = new List<string>();

    public IReadOnlyList<string> UnsupportedMpvRows { get; init; } = new List<string>();

    public IReadOnlyList<string> IncludedMpvConfigFiles { get; init; } = new List<string>();
}
