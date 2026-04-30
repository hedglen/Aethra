using System;
using System.Collections.Generic;
using Windows.System;

namespace Aethra.Input;

public readonly record struct InputGesture(string Primary, bool Ctrl, bool Shift, bool Alt)
{
    public static InputGesture FromVirtualKey(VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        return new InputGesture(MapVirtualKeyToPrimaryToken(key), ctrl, shift, alt);
    }

    public static string NormalizePrimaryToken(string token)
    {
        return token.Trim().ToUpperInvariant();
    }

    private static string MapVirtualKeyToPrimaryToken(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Number0 => "NUMBER0",
            VirtualKey.Number1 => "NUMBER1",
            VirtualKey.Number2 => "NUMBER2",
            VirtualKey.Number3 => "NUMBER3",
            VirtualKey.Number4 => "NUMBER4",
            VirtualKey.Number5 => "NUMBER5",
            VirtualKey.Number6 => "NUMBER6",
            VirtualKey.Number7 => "NUMBER7",
            VirtualKey.Number8 => "NUMBER8",
            VirtualKey.Number9 => "NUMBER9",
            VirtualKey.NumberPad0 => "NUMBERPAD0",
            VirtualKey.NumberPad1 => "NUMBERPAD1",
            VirtualKey.NumberPad2 => "NUMBERPAD2",
            VirtualKey.NumberPad3 => "NUMBERPAD3",
            VirtualKey.NumberPad4 => "NUMBERPAD4",
            VirtualKey.NumberPad5 => "NUMBERPAD5",
            VirtualKey.NumberPad6 => "NUMBERPAD6",
            VirtualKey.NumberPad7 => "NUMBERPAD7",
            VirtualKey.NumberPad8 => "NUMBERPAD8",
            VirtualKey.NumberPad9 => "NUMBERPAD9",
            VirtualKey.Decimal => "DECIMAL",
            VirtualKey.Subtract => "SUBTRACT",
            VirtualKey.Add => "ADD",
            VirtualKey.Multiply => "MULTIPLY",
            VirtualKey.Divide => "DIVIDE",
            VirtualKey.Left => "LEFT",
            VirtualKey.Right => "RIGHT",
            VirtualKey.Up => "UP",
            VirtualKey.Down => "DOWN",
            VirtualKey.Escape => "ESCAPE",
            VirtualKey.Space => "SPACE",
            VirtualKey.Tab => "TAB",
            VirtualKey.PageDown => "PAGEDOWN",
            VirtualKey.PageUp => "PAGEUP",
            VirtualKey.Home => "HOME",
            VirtualKey.End => "END",
            VirtualKey.Back => "BACK",
            VirtualKey.Delete => "DELETE",
            VirtualKey.Insert => "INSERT",
            VirtualKey.Enter => "ENTER",
            VirtualKey.Clear => "CLEAR",
            _ => MapVirtualKeyCode((int)key, key)
        };
    }

    private static string MapVirtualKeyCode(int keyCode, VirtualKey key)
    {
        return keyCode switch
        {
            188 => "COMMA",
            190 => "PERIOD",
            219 => "OPENBRACKET",
            221 => "CLOSEBRACKET",
            _ => NormalizePrimaryToken(key.ToString())
        };
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

    public static bool TryNormalizeGestureKey(string gestureText, out string key)
    {
        key = string.Empty;
        if (!TryParseGesture(gestureText, out var gesture))
            return false;

        key = BuildGestureKey(gesture);
        return true;
    }

    public static string BuildGestureKey(InputGesture gesture)
    {
        return $"{gesture.Primary}|{gesture.Ctrl}|{gesture.Shift}|{gesture.Alt}";
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

            if (!TryParsePrimaryToken(token, out var parsedPrimary, out var implicitShift))
                continue;

            primary = parsedPrimary;
            if (implicitShift)
                shift = true;
        }

        if (string.IsNullOrWhiteSpace(primary))
            return false;

        gesture = new InputGesture(primary, ctrl, shift, alt);
        return true;
    }

    private static bool TryParsePrimaryToken(string token, out string primary, out bool implicitShift)
    {
        primary = string.Empty;
        implicitShift = false;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var trimmed = token.Trim();
        var normalized = InputGesture.NormalizePrimaryToken(trimmed);
        if (normalized.Length == 0)
            return false;

        if (normalized is "MBTN_LEFT" or "MBTN_LEFT_DBL" or "MBTN_RIGHT" or "MBTN_MID" or "MBTN_BACK" or "MBTN_FORWARD" or "WHEEL_UP" or "WHEEL_DOWN")
        {
            primary = normalized;
            return true;
        }

        if (TryParsePunctuationToken(trimmed, normalized, out primary, out implicitShift))
            return true;

        if (trimmed.Length == 1)
        {
            var ch = trimmed[0];
            if (char.IsLetter(ch))
            {
                primary = char.ToUpperInvariant(ch).ToString();
                implicitShift = char.IsUpper(ch);
                return true;
            }

            if (char.IsDigit(ch))
            {
                primary = $"NUMBER{ch}";
                return true;
            }
        }

        if (TryMapSpecialToken(normalized, out primary))
            return true;

        if (Enum.TryParse<VirtualKey>(normalized, ignoreCase: true, out var enumKey))
        {
            primary = InputGesture.FromVirtualKey(enumKey, ctrl: false, shift: false, alt: false).Primary;
            return true;
        }

        return false;
    }

    private static bool TryParsePunctuationToken(string rawToken, string normalizedToken, out string primary, out bool implicitShift)
    {
        primary = string.Empty;
        implicitShift = false;

        switch (rawToken)
        {
            case ",":
                primary = "COMMA";
                return true;
            case ".":
                primary = "PERIOD";
                return true;
            case "<":
                primary = "COMMA";
                implicitShift = true;
                return true;
            case ">":
                primary = "PERIOD";
                implicitShift = true;
                return true;
            case "[":
                primary = "OPENBRACKET";
                return true;
            case "]":
                primary = "CLOSEBRACKET";
                return true;
            case "{":
                primary = "OPENBRACKET";
                implicitShift = true;
                return true;
            case "}":
                primary = "CLOSEBRACKET";
                implicitShift = true;
                return true;
        }

        switch (normalizedToken)
        {
            case "COMMA":
                primary = "COMMA";
                return true;
            case "PERIOD":
                primary = "PERIOD";
                return true;
            case "OPENBRACKET":
                primary = "OPENBRACKET";
                return true;
            case "CLOSEBRACKET":
                primary = "CLOSEBRACKET";
                return true;
            default:
                return false;
        }
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
            "ESCAPE" => "ESCAPE",
            "SPACE" => "SPACE",
            "TAB" => "TAB",
            "PGUP" => "PAGEUP",
            "PAGEDOWN" => "PAGEDOWN",
            "PGDWN" => "PAGEDOWN",
            "PAGEUP" => "PAGEUP",
            "HOME" => "HOME",
            "END" => "END",
            "BS" => "BACK",
            "BACK" => "BACK",
            "DEL" => "DELETE",
            "DELETE" => "DELETE",
            "INS" => "INSERT",
            "INSERT" => "INSERT",
            "ENTER" => "ENTER",
            "RETURN" => "ENTER",
            "CLEAR" => "CLEAR",
            "DECIMAL" => "DECIMAL",
            "SUBTRACT" => "SUBTRACT",
            "ADD" => "ADD",
            "MULTIPLY" => "MULTIPLY",
            "DIVIDE" => "DIVIDE",
            "NUMBER0" => "NUMBER0",
            "NUMBER1" => "NUMBER1",
            "NUMBER2" => "NUMBER2",
            "NUMBER3" => "NUMBER3",
            "NUMBER4" => "NUMBER4",
            "NUMBER5" => "NUMBER5",
            "NUMBER6" => "NUMBER6",
            "NUMBER7" => "NUMBER7",
            "NUMBER8" => "NUMBER8",
            "NUMBER9" => "NUMBER9",
            "NUMBERPAD0" => "NUMBERPAD0",
            "NUMBERPAD1" => "NUMBERPAD1",
            "NUMBERPAD2" => "NUMBERPAD2",
            "NUMBERPAD3" => "NUMBERPAD3",
            "NUMBERPAD4" => "NUMBERPAD4",
            "NUMBERPAD5" => "NUMBERPAD5",
            "NUMBERPAD6" => "NUMBERPAD6",
            "NUMBERPAD7" => "NUMBERPAD7",
            "NUMBERPAD8" => "NUMBERPAD8",
            "NUMBERPAD9" => "NUMBERPAD9",
            "KP0" => "NUMBERPAD0",
            "KP1" => "NUMBERPAD1",
            "KP2" => "NUMBERPAD2",
            "KP3" => "NUMBERPAD3",
            "KP4" => "NUMBERPAD4",
            "KP5" => "NUMBERPAD5",
            "KP6" => "NUMBERPAD6",
            "KP7" => "NUMBERPAD7",
            "KP8" => "NUMBERPAD8",
            "KP9" => "NUMBERPAD9",
            "KP_INSERT" => "NUMBERPAD0",
            "KP_END" => "NUMBERPAD1",
            "KP_DOWN" => "NUMBERPAD2",
            "KP_PGDN" => "NUMBERPAD3",
            "KP_LEFT" => "NUMBERPAD4",
            "KP_BEGIN" => "NUMBERPAD5",
            "KP_RIGHT" => "NUMBERPAD6",
            "KP_HOME" => "NUMBERPAD7",
            "KP_UP" => "NUMBERPAD8",
            "KP_PGUP" => "NUMBERPAD9",
            "KP_DEC" => "DECIMAL",
            "KP_DEL" => "DECIMAL",
            "KP_SUBTRACT" => "SUBTRACT",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(primary);
    }
}
