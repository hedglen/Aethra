using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace Aethra.Input;

public readonly record struct InputGesture(VirtualKey Key, bool Ctrl, bool Shift, bool Alt);

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

        var tokens = gestureText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        var ctrl = false;
        var shift = false;
        var alt = false;
        VirtualKey? key = null;

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

            if (TryParseKeyToken(token, out var parsedKey))
                key = parsedKey;
        }

        if (!key.HasValue)
            return false;

        gesture = new InputGesture(key.Value, ctrl, shift, alt);
        return true;
    }

    private static bool TryParseKeyToken(string token, out VirtualKey key)
    {
        key = default;

        if (Enum.TryParse<VirtualKey>(token, ignoreCase: true, out var enumKey))
        {
            key = enumKey;
            return true;
        }

        if (token.Length == 1)
        {
            var ch = token[0];
            if (char.IsLetter(ch))
            {
                key = (VirtualKey)char.ToUpperInvariant(ch);
                return true;
            }

            if (char.IsDigit(ch))
            {
                key = ch switch
                {
                    '0' => VirtualKey.Number0,
                    '1' => VirtualKey.Number1,
                    '2' => VirtualKey.Number2,
                    '3' => VirtualKey.Number3,
                    '4' => VirtualKey.Number4,
                    '5' => VirtualKey.Number5,
                    '6' => VirtualKey.Number6,
                    '7' => VirtualKey.Number7,
                    '8' => VirtualKey.Number8,
                    '9' => VirtualKey.Number9,
                    _ => default
                };
                return key != default;
            }
        }

        return token.ToUpperInvariant() switch
        {
            "LEFT" => AssignKey(VirtualKey.Left, out key),
            "RIGHT" => AssignKey(VirtualKey.Right, out key),
            "UP" => AssignKey(VirtualKey.Up, out key),
            "DOWN" => AssignKey(VirtualKey.Down, out key),
            "ESC" => AssignKey(VirtualKey.Escape, out key),
            "SPACE" => AssignKey(VirtualKey.Space, out key),
            "TAB" => AssignKey(VirtualKey.Tab, out key),
            "PGUP" => AssignKey(VirtualKey.PageUp, out key),
            "PGDWN" => AssignKey(VirtualKey.PageDown, out key),
            "HOME" => AssignKey(VirtualKey.Home, out key),
            "END" => AssignKey(VirtualKey.End, out key),
            "BS" => AssignKey(VirtualKey.Back, out key),
            _ => false
        };
    }

    private static bool AssignKey(VirtualKey value, out VirtualKey target)
    {
        target = value;
        return true;
    }
}
