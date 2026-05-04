using System;
using System.Collections.Generic;
using Aethra.Commands;

namespace Aethra.Input;

internal static class InputCommandSupport
{
    internal enum CommandClassification
    {
        NativeAlias,
        PassthroughSafe,
        Blocked,
        Invalid
    }

    internal enum NativeAlias
    {
        None,
        TogglePlayPause,
        ToggleMute,
        ToggleFullscreen,
        ExitFullscreen,
        ShowPlaylist,
        ShowFavorites,
        ToggleSettings,
        PreviousFile,
        NextFile,
        Quit,
        QuitWatchLater
    }

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

        var classification = ClassifyCommand(command, out reason);
        return classification is CommandClassification.Invalid or CommandClassification.Blocked;
    }

    internal static CommandClassification ClassifyCommand(string? command, out string reason)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            reason = "Empty command.";
            return CommandClassification.Invalid;
        }

        if (!MpvCommandLineParser.TryParseCommandChain(command, out var chain))
        {
            reason = "Command parse error (check quotes/spacing).";
            return CommandClassification.Invalid;
        }

        var sawPassthrough = false;
        var sawNativeAlias = false;
        foreach (var argv in chain)
        {
            var commandClassification = ClassifyParsedCommand(argv, out reason);
            if (commandClassification is CommandClassification.Invalid or CommandClassification.Blocked)
                return commandClassification;

            if (commandClassification == CommandClassification.NativeAlias)
                sawNativeAlias = true;
            else
                sawPassthrough = true;
        }

        reason = string.Empty;
        if (sawPassthrough)
            return CommandClassification.PassthroughSafe;

        return sawNativeAlias ? CommandClassification.NativeAlias : CommandClassification.Invalid;
    }

    internal static CommandClassification ClassifyParsedCommand(IReadOnlyList<string> argv, out string reason)
    {
        if (argv.Count == 0)
        {
            reason = "Empty command.";
            return CommandClassification.Invalid;
        }

        var verb = argv[0];
        if (AethraCommandIds.IsAethraCommand(verb) || TryGetNativeAlias(argv, out _))
        {
            reason = string.Empty;
            return CommandClassification.NativeAlias;
        }

        if (DeniedCommandVerbs.Contains(verb))
        {
            reason = $"Blocked command verb: {verb}";
            return CommandClassification.Blocked;
        }

        reason = string.Empty;
        return CommandClassification.PassthroughSafe;
    }

    internal static bool IsDeniedCommandVerb(string? commandVerb)
    {
        if (string.IsNullOrWhiteSpace(commandVerb))
            return false;

        return DeniedCommandVerbs.Contains(commandVerb.Trim());
    }

    internal static bool TryGetNativeAlias(IReadOnlyList<string> argv, out NativeAlias alias)
    {
        alias = NativeAlias.None;
        if (argv.Count == 0)
            return false;

        if (string.Equals(argv[0], "quit", StringComparison.OrdinalIgnoreCase))
        {
            alias = NativeAlias.Quit;
            return true;
        }

        if (string.Equals(argv[0], "quit-watch-later", StringComparison.OrdinalIgnoreCase))
        {
            alias = NativeAlias.QuitWatchLater;
            return true;
        }

        if (string.Equals(argv[0], "cycle", StringComparison.OrdinalIgnoreCase) && argv.Count > 1)
        {
            if (string.Equals(argv[1], "pause", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.TogglePlayPause;
                return true;
            }

            if (string.Equals(argv[1], "mute", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ToggleMute;
                return true;
            }

            if (string.Equals(argv[1], "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ToggleFullscreen;
                return true;
            }
        }

        if (string.Equals(argv[0], "set", StringComparison.OrdinalIgnoreCase) && argv.Count > 2)
        {
            if (string.Equals(argv[1], "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(argv[2], "no", StringComparison.OrdinalIgnoreCase))
                {
                    alias = NativeAlias.ExitFullscreen;
                    return true;
                }

                if (string.Equals(argv[2], "yes", StringComparison.OrdinalIgnoreCase))
                {
                    alias = NativeAlias.ToggleFullscreen;
                    return true;
                }
            }
        }

        if (string.Equals(argv[0], "playlist-prev", StringComparison.OrdinalIgnoreCase))
        {
            alias = NativeAlias.PreviousFile;
            return true;
        }

        if (string.Equals(argv[0], "playlist-next", StringComparison.OrdinalIgnoreCase))
        {
            alias = NativeAlias.NextFile;
            return true;
        }
        if (string.Equals(argv[0], "script-binding", StringComparison.OrdinalIgnoreCase) && argv.Count > 1)
        {
            if (string.Equals(argv[1], "playlistmanager/showplaylist", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ShowPlaylist;
                return true;
            }

            if (string.Equals(argv[1], "uosc/menu", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ToggleSettings;
                return true;
            }
        }

        if (string.Equals(argv[0], "script-message-to", StringComparison.OrdinalIgnoreCase) && argv.Count > 2)
        {
            if (string.Equals(argv[1], "favorites", StringComparison.OrdinalIgnoreCase)
                && string.Equals(argv[2], "favorites-open", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ShowFavorites;
                return true;
            }

            if (string.Equals(argv[1], "playlistmanager", StringComparison.OrdinalIgnoreCase)
                && string.Equals(argv[2], "showplaylist", StringComparison.OrdinalIgnoreCase))
            {
                alias = NativeAlias.ShowPlaylist;
                return true;
            }
        }

        return false;
    }
}
