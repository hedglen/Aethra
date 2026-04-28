using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI;

namespace Aethra.Services;

public sealed class AccentColorChangedEventArgs(string hex, Color color) : EventArgs
{
    public string Hex { get; } = hex;

    public Color Color { get; } = color;
}

public static class AccentColorService
{
    private const string SettingsKey = "AccentColorHex";
    public const string DefaultAccentHex = "#7B2FFF";

    public static event EventHandler<AccentColorChangedEventArgs>? AccentColorChanged;

    public static string CurrentHex { get; private set; } = DefaultAccentHex;

    public static void Initialize()
    {
        var savedHex = ReadSavedHex();

        if (!TryParseHexColor(savedHex ?? DefaultAccentHex, out var color, out var normalizedHex))
        {
            color = CreateColor(0x7B, 0x2F, 0xFF);
            normalizedHex = DefaultAccentHex;
        }

        CurrentHex = normalizedHex;
        ApplyColor(color);
        AccentColorChanged?.Invoke(null, new AccentColorChangedEventArgs(normalizedHex, color));
    }

    public static bool TryApplyHex(string input, out string normalizedHex)
    {
        if (!TryParseHexColor(input, out var color, out normalizedHex))
            return false;

        CurrentHex = normalizedHex;
        SaveHex(normalizedHex);
        ApplyColor(color);
        AccentColorChanged?.Invoke(null, new AccentColorChangedEventArgs(normalizedHex, color));
        return true;
    }

    public static bool TryParseHexColor(string? input, out Color color, out string normalizedHex)
    {
        color = default;
        normalizedHex = string.Empty;

        var value = (input ?? string.Empty).Trim();
        if (value.StartsWith('#'))
            value = value[1..];

        if (value.Length == 3)
        {
            value = string.Concat(
                value[0], value[0],
                value[1], value[1],
                value[2], value[2]);
        }

        if (value.Length != 6)
            return false;

        if (!byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        color = CreateColor(r, g, b);
        normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
        return true;
    }

    private static Color CreateColor(byte r, byte g, byte b)
    {
        return Color.FromArgb(0xFF, r, g, b);
    }

    private static void ApplyColor(Color color)
    {
        SetBrushColor("AethraAccentBrush", color);
        SetBrushColor("AethraAccentHoverBrush", Color.FromArgb(0xE6, color.R, color.G, color.B));
        SetBrushColor("AethraAccentPressedBrush", Color.FromArgb(0xCC, color.R, color.G, color.B));
        SetBrushColor("AethraAccentSoftBrush", Color.FromArgb(0x66, color.R, color.G, color.B));
        SetBrushColor("AethraAccentSubtleBrush", Color.FromArgb(0x26, color.R, color.G, color.B));
    }

    private static void SetBrushColor(string key, Color color)
    {
        try
        {
            var resources = Application.Current?.Resources;
            if (resources is null)
                return;

            if (resources.TryGetValue(key, out var resource)
                && resource is SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"Accent brush lookup failed for {key}. {ex}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Accent brush update was skipped for {key}. {ex}");
        }
    }

    private static string? ReadSavedHex()
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[SettingsKey] as string;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveHex(string hex)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = hex;
        }
        catch
        {
            // Local settings can be unavailable in unusual unpackaged/debug contexts.
        }
    }
}
