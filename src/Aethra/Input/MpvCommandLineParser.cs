using System;
using System.Collections.Generic;
using System.Text;

namespace Aethra.Input;

internal static class MpvCommandLineParser
{
    internal static bool TryParseCommandChain(string? commandLine, out List<string[]> commands)
    {
        commands = new List<string[]>();
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        foreach (var segment in SplitCommandSegments(commandLine))
        {
            if (!TryTokenizeCommand(segment, out var argv))
                return false;

            if (argv.Length > 0)
                commands.Add(argv);
        }

        return commands.Count > 0;
    }

    private static IEnumerable<string> SplitCommandSegments(string commandLine)
    {
        var builder = new StringBuilder(commandLine.Length);
        var quote = '\0';

        for (var i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];
            if ((ch == '"' || ch == '\'') && !IsEscaped(commandLine, i))
            {
                if (quote == '\0')
                    quote = ch;
                else if (quote == ch)
                    quote = '\0';
            }

            if (ch == ';' && quote == '\0')
            {
                var segment = builder.ToString().Trim();
                if (segment.Length > 0)
                    yield return segment;
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        var tail = builder.ToString().Trim();
        if (tail.Length > 0)
            yield return tail;
    }

    private static bool TryTokenizeCommand(string command, out string[] tokens)
    {
        var values = new List<string>();
        var builder = new StringBuilder(command.Length);
        var quote = '\0';

        for (var i = 0; i < command.Length; i++)
        {
            var ch = command[i];
            if ((ch == '"' || ch == '\'') && !IsEscaped(command, i))
            {
                if (quote == '\0')
                {
                    quote = ch;
                    continue;
                }

                if (quote == ch)
                {
                    quote = '\0';
                    continue;
                }
            }

            if (char.IsWhiteSpace(ch) && quote == '\0')
            {
                FlushToken(values, builder);
                continue;
            }

            builder.Append(ch);
        }

        if (quote != '\0')
        {
            tokens = Array.Empty<string>();
            return false;
        }

        FlushToken(values, builder);
        tokens = values.ToArray();
        return tokens.Length > 0;
    }

    private static void FlushToken(ICollection<string> values, StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        values.Add(Unescape(builder.ToString()));
        builder.Clear();
    }

    private static string Unescape(string value)
    {
        return value.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\'", "'", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static bool IsEscaped(string value, int index)
    {
        var slashCount = 0;
        for (var i = index - 1; i >= 0 && value[i] == '\\'; i--)
            slashCount++;

        return slashCount % 2 == 1;
    }
}
