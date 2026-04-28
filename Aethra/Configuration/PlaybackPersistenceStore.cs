using System;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Aethra.Configuration;

public sealed class PlaybackPersistenceSnapshot
{
    public string? LastMediaPath { get; init; }

    public double LastVolume { get; init; } = 100;

    public double LastPositionSeconds { get; init; }

    public int? WindowX { get; init; }

    public int? WindowY { get; init; }

    public int? WindowWidth { get; init; }

    public int? WindowHeight { get; init; }
}

public static class PlaybackPersistenceStore
{
    private const string LastMediaPathKey = "Playback.LastMediaPath";
    private const string LastVolumeKey = "Playback.LastVolume";
    private const string LastPositionKey = "Playback.LastPosition";
    private const string WindowXKey = "Window.X";
    private const string WindowYKey = "Window.Y";
    private const string WindowWidthKey = "Window.Width";
    private const string WindowHeightKey = "Window.Height";

    public static PlaybackPersistenceSnapshot Load()
    {
        var settings = ApplicationData.Current.LocalSettings.Values;
        return new PlaybackPersistenceSnapshot
        {
            LastMediaPath = settings[LastMediaPathKey] as string,
            LastVolume = ReadDouble(settings, LastVolumeKey, 100),
            LastPositionSeconds = ReadDouble(settings, LastPositionKey, 0),
            WindowX = ReadInt(settings, WindowXKey),
            WindowY = ReadInt(settings, WindowYKey),
            WindowWidth = ReadInt(settings, WindowWidthKey),
            WindowHeight = ReadInt(settings, WindowHeightKey)
        };
    }

    public static void SaveLastMedia(string? path, double positionSeconds)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var settings = ApplicationData.Current.LocalSettings.Values;
        settings[LastMediaPathKey] = path;
        settings[LastPositionKey] = Math.Max(0, positionSeconds);
    }

    public static void SaveVolume(double volume)
    {
        ApplicationData.Current.LocalSettings.Values[LastVolumeKey] = Math.Clamp(volume, 0, 100);
    }

    public static void SaveWindow(int x, int y, int width, int height)
    {
        var settings = ApplicationData.Current.LocalSettings.Values;
        settings[WindowXKey] = x;
        settings[WindowYKey] = y;
        settings[WindowWidthKey] = Math.Max(320, width);
        settings[WindowHeightKey] = Math.Max(200, height);
    }

    private static double ReadDouble(IPropertySet settings, string key, double fallback)
    {
        if (settings.TryGetValue(key, out var value))
        {
            if (value is double d)
                return d;
            if (value is float f)
                return f;
            if (value is int i)
                return i;
        }

        return fallback;
    }

    private static int? ReadInt(IPropertySet settings, string key)
    {
        if (!settings.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            int i => i,
            long l => (int)l,
            _ => null
        };
    }
}
