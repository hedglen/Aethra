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

    public void ApplyPlaybackPreferences(PlaybackPreferencesProfile profile)
    {
        ApplyNumericProperty("speed", Math.Clamp(profile.DefaultPlaybackSpeedPercent, 25, 400) / 100.0);
        var loopValue = profile.EndOfFileAction == PlaybackEndOfFileAction.LoopCurrentFile ? "inf" : "no";
        ApplyStringProperty("loop-file", loopValue);
        ApplyStringProperty("keep-open", profile.EndOfFileAction == PlaybackEndOfFileAction.PlayNextInFolder ? "no" : (profile.AutoplayOnOpen ? "no" : "yes"));
        ApplyStringProperty("playlist-next", profile.EndOfFileAction == PlaybackEndOfFileAction.PlayNextInFolder ? "force" : "no");
        ApplyStringProperty("save-position-on-quit", profile.ResumeWhereLeftOff ? "yes" : "no");
    }

    public void ApplyVideoPreferences(VideoPreferencesProfile profile)
    {
        var vo = profile.OutputMode switch
        {
            VideoOutputMode.Gpu => "gpu",
            _ => "gpu-next"
        };
        ApplyStringProperty("vo", vo);

        var hwdec = profile.HardwareDecode switch
        {
            HardwareDecodeMode.Nvdec => "nvdec",
            HardwareDecodeMode.Dxva2 => "dxva2",
            HardwareDecodeMode.Copy => "auto-copy",
            _ => "auto-safe"
        };
        ApplyStringProperty("hwdec", hwdec);
        ApplyStringProperty("interpolation", profile.InterpolationEnabled ? "yes" : "no");
        ApplyStringProperty("deinterlace", profile.DeinterlaceEnabled ? "yes" : "no");
    }

    public void ApplyAudioPreferences(AudioPreferencesProfile profile)
    {
        var audioDevice = string.IsNullOrWhiteSpace(profile.OutputDevice) || profile.OutputDevice == "System default"
            ? "auto"
            : profile.OutputDevice;
        ApplyStringProperty("audio-device", audioDevice);
        ApplyStringProperty("ad-lavc-ac3drc", profile.DynamicRangeCompression ? "1.0" : "0.0");
        ApplyStringProperty("replaygain", profile.ReplayGainNormalization ? "track" : "no");
        var channels = profile.ChannelLayout switch
        {
            AudioChannelLayout.Stereo => "stereo",
            AudioChannelLayout.Surround51 => "5.1",
            AudioChannelLayout.Surround71 => "7.1",
            _ => "auto"
        };
        ApplyStringProperty("audio-channels", channels);
    }

    public void ApplySubtitlePreferences(SubtitlePreferencesProfile profile)
    {
        ApplyStringProperty("sub-auto", profile.AutoLoadMatchingSubtitles ? "fuzzy" : "no");
        if (!string.IsNullOrWhiteSpace(profile.PreferredLanguagesCsv))
            ApplyStringProperty("slang", profile.PreferredLanguagesCsv);
        ApplyStringProperty("sub-border-style", profile.BorderAndShadow ? "outline-and-shadow" : "none");
        ApplyNumericProperty("sub-font-size", Math.Clamp(profile.FontSize, 12, 100));
    }

    public void ApplyAdvancedPreferences(AdvancedPreferencesProfile profile)
    {
        var msgLevel = profile.LogLevel switch
        {
            AdvancedLogLevel.Off => "all=no",
            AdvancedLogLevel.Verbose => "all=v",
            AdvancedLogLevel.Debug => "all=debug",
            _ => "all=warn"
        };
        ApplyStringProperty("msg-level", msgLevel);

        var lines = (profile.ExtraMpvOptionsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0 || separator >= line.Length - 1)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            ApplyStringProperty(key, value);
        }
    }

    public void ApplyNetworkPreferences(NetworkPreferencesProfile profile)
    {
        ApplyStringProperty("network-timeout", Math.Clamp(profile.NetworkTimeoutSeconds, 5, 600).ToString(System.Globalization.CultureInfo.InvariantCulture));
        ApplyStringProperty("ipv6", profile.PreferIpv6 ? "yes" : "no");
        ApplyStringProperty("cache-pause-wait", profile.AllowMeteredConnections ? "5" : "2");

        var proxy = profile.ProxyMode switch
        {
            NetworkProxyMode.Direct => "no",
            NetworkProxyMode.Http when !string.IsNullOrWhiteSpace(profile.ProxyUrl) => profile.ProxyUrl.Trim(),
            _ => string.Empty
        };
        // Always emit `http-proxy` so switching proxy mode does not leave stale runtime state.
        ApplyStringProperty("http-proxy", proxy);
    }

    public void ApplyCustomizationPreferences(CustomizationPreferencesProfile profile)
    {
        // These are app-owned UX toggles and may later map to explicit runtime properties.
        var accentHex = AccentColorService.TryParseHexColor(profile.AccentHex, out _, out var normalizedHex)
            ? normalizedHex
            : AccentColorService.DefaultAccentHex;
        ApplyStringProperty("aethra-accent-hex", accentHex);
        ApplyStringProperty("aethra-use-system-theme", profile.UseSystemTheme ? "yes" : "no");
        ApplyStringProperty("aethra-dense-layout", profile.DenseLayout ? "yes" : "no");
        ApplyStringProperty("aethra-show-playback-hud", profile.ShowPlaybackHud ? "yes" : "no");
    }
}
