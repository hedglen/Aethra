using System;
using System.Collections.Generic;
using Aethra.Profiles;

namespace Aethra.Services;

public sealed class PlaybackPropertyApplyEventArgs(string propertyName, string propertyValue) : EventArgs
{
    public string PropertyName { get; } = propertyName;

    public string PropertyValue { get; } = propertyValue;
}

public sealed class VideoQualityPresetChangedEventArgs(VideoQualityPreset preset) : EventArgs
{
    public VideoQualityPreset Preset { get; } = preset;
}

public sealed class PlaybackOptionsService
{
    private static readonly IReadOnlyDictionary<VideoQualityPreset, IReadOnlyList<(string Property, string Value)>> Presets
        = new Dictionary<VideoQualityPreset, IReadOnlyList<(string Property, string Value)>>
        {
            [VideoQualityPreset.Reference] = new (string, string)[]
            {
                ("scale", "ewa_lanczossharp"),
                ("cscale", "ewa_lanczos"),
                ("deband", "yes"),
                ("interpolation", "no")
            },
            [VideoQualityPreset.Cinema] = new (string, string)[]
            {
                ("scale", "ewa_lanczos"),
                ("cscale", "ewa_lanczos"),
                ("deband", "yes"),
                ("interpolation", "yes")
            },
            [VideoQualityPreset.Anime] = new (string, string)[]
            {
                ("scale", "ewa_lanczossharp"),
                ("cscale", "ewa_lanczossharp"),
                ("deband", "no"),
                ("interpolation", "no")
            }
        };

    private PlaybackOptionsService()
    {
    }

    public static PlaybackOptionsService Instance { get; } = new();

    public VideoQualityPreset CurrentVideoQualityPreset { get; private set; } = VideoQualityPreset.Reference;

    public event EventHandler<PlaybackPropertyApplyEventArgs>? PropertyApplyRequested;
    public event EventHandler<VideoQualityPresetChangedEventArgs>? VideoQualityPresetChanged;

    public void ApplyNumericProperty(string property, double value)
    {
        PropertyApplyRequested?.Invoke(this, new PlaybackPropertyApplyEventArgs(property, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public void ApplyStringProperty(string property, string value)
    {
        PropertyApplyRequested?.Invoke(this, new PlaybackPropertyApplyEventArgs(property, value));
    }

    public void ApplyVideoQualityPreset(VideoQualityPreset preset)
    {
        CurrentVideoQualityPreset = preset;
        if (Presets.TryGetValue(preset, out var values))
        {
            foreach (var (property, value) in values)
                ApplyStringProperty(property, value);
        }

        VideoQualityPresetChanged?.Invoke(this, new VideoQualityPresetChangedEventArgs(preset));
    }
}
