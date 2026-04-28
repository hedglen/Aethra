using System;
using System.Collections.Generic;
using Windows.System;

namespace Aethra.Input;

public readonly record struct InputGesture(string Primary, bool Ctrl, bool Shift, bool Alt)
{
    public static InputGesture FromVirtualKey(VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        return new InputGesture(NormalizePrimaryToken(key.ToString()), ctrl, shift, alt);
    }

    public static string NormalizePrimaryToken(string token)
    {
        return token.Trim().ToUpperInvariant();
    }
}

public sealed class InputRuntimeService
{
    private readonly Dictionary<InputGesture, string> _commandByGesture = new();

    public void LoadBindings(IEnumerable<InputBindingSetting> bindings)
    {
        _commandByGesture.Clear();
        foreach (var binding in bindings)
        {
            if (!TryParseGesture(binding.Gesture, out var gesture))
                continue;

            _commandByGesture[gesture] = binding.Command;
        }
    }

    public bool TryGetCommand(InputGesture gesture, out string command)
    {
        return _commandByGesture.TryGetValue(gesture, out command!);
    }

    public static bool TryParseGesture(string gestureText, out InputGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(gestureText))
            return false;

        var normalizedText = gestureText.Contains('/')
            ? gestureText.Split('/', 2, StringSplitOptions.TrimEntries)[0]
            : gestureText;
        var tokens = normalizedText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        var ctrl = false;
        var shift = false;
        var alt = false;
        string? primary = null;

        foreach (var token in tokens)
        {
            if (token.Equals("CTRL", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (token.Equals("SHIFT", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (token.Equals("ALT", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (TryParsePrimaryToken(token, out var parsedPrimary))
                primary = parsedPrimary;
        }

        if (string.IsNullOrWhiteSpace(primary))
            return false;

        gesture = new InputGesture(primary, ctrl, shift, alt);
        return true;
    }

    private static bool TryParsePrimaryToken(string token, out string primary)
    {
        primary = string.Empty;
        var normalized = InputGesture.NormalizePrimaryToken(token);
        if (normalized.Length == 0)
            return false;

        if (normalized is "MBTN_LEFT" or "MBTN_RIGHT" or "MBTN_MID" or "MBTN_BACK" or "MBTN_FORWARD" or "WHEEL_UP" or "WHEEL_DOWN")
        {
            primary = normalized;
            return true;
        }

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (char.IsLetter(ch))
            {
                primary = ch.ToString();
                return true;
            }

            if (char.IsDigit(ch))
            {
                primary = ch switch
                {
                    '0' => "NUMBER0",
                    '1' => "NUMBER1",
                    '2' => "NUMBER2",
                    '3' => "NUMBER3",
                    '4' => "NUMBER4",
                    '5' => "NUMBER5",
                    '6' => "NUMBER6",
                    '7' => "NUMBER7",
                    '8' => "NUMBER8",
                    '9' => "NUMBER9",
                    _ => string.Empty
                };
                return !string.IsNullOrWhiteSpace(primary);
            }
        }

        if (TryMapSpecialToken(normalized, out primary))
            return true;

        if (Enum.TryParse<VirtualKey>(normalized, ignoreCase: true, out var enumKey))
        {
            primary = InputGesture.NormalizePrimaryToken(enumKey.ToString());
            return true;
        }

        return false;
    }

    private static bool TryMapSpecialToken(string token, out string primary)
    {
        primary = token switch
        {
            "LEFT" => "LEFT",
            "RIGHT" => "RIGHT",
            "UP" => "UP",
            "DOWN" => "DOWN",
            "ESC" => "ESCAPE",
            "SPACE" => "SPACE",
            "TAB" => "TAB",
            "PGUP" => "PAGEUP",
            "PGDWN" => "PAGEDOWN",
            "HOME" => "HOME",
            "END" => "END",
            "BS" => "BACK",
            "DEL" => "DELETE",
            "INS" => "INSERT",
            "ENTER" => "ENTER",
            "RETURN" => "ENTER",
            "KP_DEC" => "DECIMAL",
            "KP_SUBTRACT" => "SUBTRACT",
            "KP0" => "NUMBER0",
            "KP1" => "NUMBER1",
            "KP2" => "NUMBER2",
            "KP3" => "NUMBER3",
            "KP4" => "NUMBER4",
            "KP5" => "NUMBER5",
            "KP6" => "NUMBER6",
            "KP7" => "NUMBER7",
            "KP8" => "NUMBER8",
            "KP9" => "NUMBER9",
            "KP_INSERT" => "NUMBER0",
            "KP_END" => "NUMBER1",
            "KP_DOWN" => "NUMBER2",
            "KP_PGDN" => "NUMBER3",
            "KP_LEFT" => "NUMBER4",
            "KP_BEGIN" => "NUMBER5",
            "KP_RIGHT" => "NUMBER6",
            "KP_HOME" => "NUMBER7",
            "KP_UP" => "NUMBER8",
            "KP_PGUP" => "NUMBER9",
            "KP_DEL" => "DELETE",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(primary);
    }
}
