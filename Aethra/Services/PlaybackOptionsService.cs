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

public sealed class ShaderPresetChangedEventArgs(ShaderChainPreset preset, string chain) : EventArgs
{
    public ShaderChainPreset Preset { get; } = preset;

    public string Chain { get; } = chain;
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
                ("dscale", "mitchell"),
                ("tscale", "oversample"),
                ("deband", "yes"),
                ("interpolation", "no"),
                ("target-peak", "400"),
                ("target-colorspace-hint", "no")
            },
            [VideoQualityPreset.Cinema] = new (string, string)[]
            {
                ("scale", "ewa_lanczos"),
                ("cscale", "ewa_lanczos"),
                ("dscale", "mitchell"),
                ("tscale", "oversample"),
                ("deband", "yes"),
                ("interpolation", "yes"),
                ("target-peak", "400"),
                ("target-colorspace-hint", "no")
            },
            [VideoQualityPreset.Anime] = new (string, string)[]
            {
                ("scale", "ewa_lanczossharp"),
                ("cscale", "ewa_lanczossharp"),
                ("dscale", "mitchell"),
                ("tscale", "oversample"),
                ("deband", "no"),
                ("interpolation", "no"),
                ("target-peak", "400"),
                ("target-colorspace-hint", "no")
            },
            [VideoQualityPreset.LowResBoost] = new (string, string)[]
            {
                ("scale", "ewa_lanczossharp"),
                ("cscale", "ewa_lanczos"),
                ("dscale", "mitchell"),
                ("tscale", "oversample"),
                ("deband", "yes"),
                ("interpolation", "no"),
                ("target-peak", "400"),
                ("target-colorspace-hint", "no")
            },
            [VideoQualityPreset.NativeClean] = new (string, string)[]
            {
                ("scale", "bilinear"),
                ("cscale", "bilinear"),
                ("dscale", "mitchell"),
                ("tscale", "oversample"),
                ("deband", "no"),
                ("interpolation", "no"),
                ("target-peak", "400"),
                ("target-colorspace-hint", "no")
            }
        };
    private static readonly IReadOnlyDictionary<ShaderChainPreset, string> ShaderChains = new Dictionary<ShaderChainPreset, string>
    {
        [ShaderChainPreset.None] = string.Empty,
        [ShaderChainPreset.Fsrcnnx] = "~~/shaders/FSRCNNX_x2_56-16-4-1.glsl",
        [ShaderChainPreset.Anime4k] = "~~/shaders/Anime4K_Clamp_Highlights.glsl:~~/shaders/Anime4K_Restore_CNN_VL.glsl:~~/shaders/Anime4K_Upscale_CNN_x2_VL.glsl:~~/shaders/Anime4K_AutoDownscalePre_x2.glsl:~~/shaders/Anime4K_AutoDownscalePre_x4.glsl:~~/shaders/Anime4K_Upscale_CNN_x2_M.glsl",
        [ShaderChainPreset.SsimFsrcnnx] = "~~/shaders/SSimDownscaler.glsl:~~/shaders/FSRCNNX_x2_56-16-4-1.glsl"
    };

    private PlaybackOptionsService()
    {
    }

    public static PlaybackOptionsService Instance { get; } = new();

    public VideoQualityPreset CurrentVideoQualityPreset { get; private set; } = VideoQualityPreset.Reference;
    public ShaderChainPreset CurrentShaderPreset { get; private set; } = ShaderChainPreset.None;
    public string CurrentCustomShaderChain { get; private set; } = string.Empty;

    public event EventHandler<PlaybackPropertyApplyEventArgs>? PropertyApplyRequested;
    public event EventHandler<VideoQualityPresetChangedEventArgs>? VideoQualityPresetChanged;
    public event EventHandler<ShaderPresetChangedEventArgs>? ShaderPresetChanged;

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

    public void ApplyShaderPreset(ShaderChainPreset preset)
    {
        CurrentShaderPreset = preset;
        CurrentCustomShaderChain = string.Empty;
        var chain = ShaderChains.TryGetValue(preset, out var value) ? value : string.Empty;
        ApplyStringProperty("glsl-shaders", chain);
        ShaderPresetChanged?.Invoke(this, new ShaderPresetChangedEventArgs(preset, chain));
    }

    public void ApplyCustomShaderChain(string chain)
    {
        CurrentShaderPreset = ShaderChainPreset.None;
        CurrentCustomShaderChain = chain ?? string.Empty;
        ApplyStringProperty("glsl-shaders", CurrentCustomShaderChain);
        ShaderPresetChanged?.Invoke(this, new ShaderPresetChangedEventArgs(CurrentShaderPreset, CurrentCustomShaderChain));
    }
}
