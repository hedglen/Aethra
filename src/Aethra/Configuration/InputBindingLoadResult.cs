using System.Collections.Generic;
using Aethra.Input;

namespace Aethra.Configuration;

public sealed class InputBindingLoadResult
{
    public InputBindingLoadResult(
        IReadOnlyList<InputBindingSetting> bindings,
        bool wasMigrated,
        string summary,
        IReadOnlyList<string> warnings)
    {
        Bindings = bindings;
        WasMigrated = wasMigrated;
        Summary = summary;
        Warnings = warnings;
    }

    public IReadOnlyList<InputBindingSetting> Bindings { get; }

    public bool WasMigrated { get; }

    public string Summary { get; }

    public IReadOnlyList<string> Warnings { get; }
}
