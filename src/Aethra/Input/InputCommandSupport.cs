using System;
using System.Collections.Generic;
using Aethra.Commands;

namespace Aethra.Input;

internal static class InputCommandSupport
{
    private static readonly HashSet<string> DeniedCommandVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "run",
        "subprocess",
        "script-message-to"
    };

    internal static bool IsSupportedCommand(string? command)
    {
        return !TryGetUnsupportedReason(command, out _);
    }

    internal static bool TryGetUnsupportedReason(string? command, out string reason)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            reason = "Empty command.";
            return true;
        }

        if (!MpvCommandLineParser.TryParseCommandChain(command, out var chain))
        {
            reason = "Command parse error (check quotes/spacing).";
            return true;
        }

        foreach (var argv in chain)
        {
            if (argv.Length == 0)
                continue;

            var verb = argv[0];
            if (AethraCommandIds.IsAethraCommand(verb))
                continue;

            if (DeniedCommandVerbs.Contains(verb))
            {
                reason = $"Blocked command verb: {verb}";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    internal static bool IsDeniedCommandVerb(string? commandVerb)
    {
        if (string.IsNullOrWhiteSpace(commandVerb))
            return false;

        return DeniedCommandVerbs.Contains(commandVerb.Trim());
    }
}
