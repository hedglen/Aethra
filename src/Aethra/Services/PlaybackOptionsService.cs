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
    private const double MinimumSubtitleFontSize = 14;
    private const double MaximumSubtitleFontSize = 28;
    private const double DefaultSubtitleFontSize = 20;
    private const string DefaultSubtitleFont = "Segoe UI";

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
        ApplyStringProperty("loop-playlist", "no");
        // `save-position-on-quit` is an mpv config-file option, not a runtime property.
        // Runtime persistence is handled by Aethra-owned PlaybackPersistenceStore.
        _ = profile.ResumeWhereLeftOff;
        _ = profile.AutoplayOnOpen;
        _ = profile.EndOfFileAction;
    }

    public void ApplyVideoPreferences(VideoPreferencesProfile profile)
    {
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

    public void ApplyVideoEnhancementPreferences(VideoPreferencesProfile profile)
    {
        ApplyVideoQualityPreset(profile.QualityPreset);
        if (profile.ShaderPreset == ShaderChainPreset.None && !string.IsNullOrWhiteSpace(profile.CustomShaderChain))
            ApplyCustomShaderChain(profile.CustomShaderChain);
        else
            ApplyShaderPreset(profile.ShaderPreset);
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

        var fontSize = double.IsFinite(profile.FontSize)
            ? Math.Clamp(profile.FontSize, MinimumSubtitleFontSize, MaximumSubtitleFontSize)
            : DefaultSubtitleFontSize;
        var fontSizeText = fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Windows-Media-Player look: white Segoe UI, regular weight, single soft drop
        // shadow (no hard outline), bottom-centered with breathing room above the chrome.
        ApplyStringProperty("sub-font", DefaultSubtitleFont);
        ApplyStringProperty("sub-bold", "no");
        ApplyStringProperty("sub-italic", "no");
        ApplyStringProperty("sub-color", "#FFFFFFFF");
        ApplyStringProperty("sub-back-color", "#00000000");

        // Layout / scaling. Keep subtitle size tied to the video plane so it behaves
        // more like native Windows Media Player subtitles instead of scaling with the
        // outer window chrome/letterbox area.
        ApplyNumericProperty("sub-scale", 1);
        ApplyStringProperty("sub-scale-with-window", "no");
        ApplyStringProperty("sub-ass-scale-with-window", "no");
        ApplyStringProperty("sub-scale-signs", "no");
        ApplyStringProperty("sub-align-x", "center");
        ApplyStringProperty("sub-align-y", "bottom");
        ApplyStringProperty("sub-justify", "center");
        ApplyNumericProperty("sub-margin-x", 64);
        ApplyNumericProperty("sub-margin-y", 96);
        ApplyStringProperty("sub-use-margins", "yes");
        ApplyNumericProperty("sub-spacing", 0);

        if (profile.BorderAndShadow)
        {
            // Soft drop shadow only — matches WMP. A hint of outline (0.6) keeps text
            // legible against bright/white scenes without looking like classic karaoke.
            ApplyStringProperty("sub-border-style", "outline-and-shadow");
            ApplyStringProperty("sub-outline-color", "#FF000000");
            ApplyStringProperty("sub-shadow-color", "#A6000000");
            ApplyNumericProperty("sub-outline-size", 0.6);
            ApplyNumericProperty("sub-shadow-offset", 2.0);
            ApplyNumericProperty("sub-blur", 0.4);
        }
        else
        {
            ApplyStringProperty("sub-border-style", "outline-and-shadow");
            ApplyNumericProperty("sub-outline-size", 0);
            ApplyNumericProperty("sub-shadow-offset", 0);
            ApplyNumericProperty("sub-blur", 0);
        }

        ApplyNumericProperty("sub-font-size", fontSize);

        // For ASS-styled tracks (e.g. embedded .ass / converted SRT inside many mkvs),
        // mpv's `sub-*` properties are ignored unless we force the equivalent ASS
        // style fields. `sub-ass-override=force` plus a complete style override pins
        // everything we care about — font, size, weight, colors, border style — so a
        // stray embedded "FontSize=200" can't hijack the look. Opacity nibble in
        // PrimaryColour stays 00 (fully opaque).
        var outlineAss = (profile.BorderAndShadow ? 0.6 : 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var shadowAss = (profile.BorderAndShadow ? 2.0 : 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var assOverrides = string.Join(",",
            $"FontName={DefaultSubtitleFont}",
            $"FontSize={fontSizeText}",
            "Bold=0",
            "Italic=0",
            "PrimaryColour=&H00FFFFFF",
            "OutlineColour=&H00000000",
            "BackColour=&HA6000000",
            $"Outline={outlineAss}",
            $"Shadow={shadowAss}",
            "BorderStyle=1",
            "Alignment=2",
            "MarginL=64",
            "MarginR=64",
            "MarginV=24",
            "ScaleX=100",
            "ScaleY=100",
            "Spacing=0");
        ApplyStringProperty("sub-ass-style-overrides", assOverrides);
        ApplyStringProperty("sub-ass-override", "force");

        ApplyNumericProperty("sub-delay", Math.Clamp(profile.SubtitleDelaySeconds, -10, 10));
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
        // `ipv6` is treated as an initialization/config option by libmpv and can fail
        // when emitted as a runtime `set` command. Keep it in typed preferences so it
        // can still participate in import/export and future pre-init bootstrap handling.
        _ = profile.PreferIpv6;
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
        // App-owned UX toggles are consumed by UI/services, not forwarded to mpv as runtime properties.
        _ = profile;
    }
}
