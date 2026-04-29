using System;

namespace Aethra.Configuration;

internal static class MpvConfigLineSupport
{
    // Adapted from mpv.net config-line normalization behavior:
    // https://github.com/mpvnet-player/mpv.net/blob/ef45baecbdd8e0a249eca9a621fe608143f75c4b/src/MpvNet/Player.cs
    internal static string NormalizeLine(string? rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
            return string.Empty;

        var line = rawLine.TrimStart(' ', '-').TrimEnd();
        if (line.Length == 0 || line.StartsWith('#'))
            return string.Empty;

        if (line.Contains('#')
            && !line.StartsWith("'#", StringComparison.Ordinal)
            && !line.StartsWith("\"#", StringComparison.Ordinal))
        {
            line = line[..line.IndexOf('#')].Trim();
        }

        return line;
    }

    internal static bool TryParseOptionLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0)
        {
            // mpv allows boolean shorthand: "deband" => "deband=yes"
            key = line.Trim();
            if (!IsWordToken(key))
                return false;

            value = "yes";
            return true;
        }

        if (separatorIndex == 0)
            return false;

        key = line[..separatorIndex].Trim();
        value = line[(separatorIndex + 1)..].Trim();
        return key.Length > 0;
    }

    internal static bool TryParseInputBindingLine(string line, out string gesture, out string command)
    {
        gesture = string.Empty;
        command = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        gesture = parts[0].Trim();
        command = parts[1].Trim();
        return gesture.Length > 0 && command.Length > 0;
    }

    private static bool IsWordToken(string value)
    {
        foreach (var ch in value)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                return false;
        }

        return value.Length > 0;
    }
}
