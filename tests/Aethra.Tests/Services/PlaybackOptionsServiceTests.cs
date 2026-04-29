using Aethra.Profiles;
using Aethra.Services;
using Xunit;

namespace Aethra.Tests.Services;

public sealed class PlaybackOptionsServiceTests
{
    [Fact]
    public void ApplyVideoPreferences_EmitsExpectedProperties()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyVideoPreferences(new VideoPreferencesProfile
            {
                OutputMode = VideoOutputMode.Gpu,
                HardwareDecode = HardwareDecodeMode.Nvdec,
                InterpolationEnabled = true,
                DeinterlaceEnabled = true
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.DoesNotContain(emitted, pair => pair.Property == "vo");
        Assert.Contains(("hwdec", "nvdec"), emitted);
        Assert.Contains(("interpolation", "yes"), emitted);
        Assert.Contains(("deinterlace", "yes"), emitted);
    }

    [Fact]
    public void ApplyAdvancedPreferences_ParsesRawOptionsLines()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyAdvancedPreferences(new AdvancedPreferencesProfile
            {
                LogLevel = AdvancedLogLevel.Verbose,
                ExtraMpvOptionsText = "demuxer-max-bytes=64MiB\n# comment\ncache=yes"
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Contains(("msg-level", "all=v"), emitted);
        Assert.Contains(("demuxer-max-bytes", "64MiB"), emitted);
        Assert.Contains(("cache", "yes"), emitted);
    }

    [Fact]
    public void ApplyVideoEnhancementPreferences_EmitsPresetAndCustomShader()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyVideoEnhancementPreferences(new VideoPreferencesProfile
            {
                QualityPreset = VideoQualityPreset.Cinema,
                ShaderPreset = ShaderChainPreset.None,
                CustomShaderChain = "~~/shaders/custom.glsl"
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Contains(("scale", "ewa_lanczos"), emitted);
        Assert.Contains(("glsl-shaders", "~~/shaders/custom.glsl"), emitted);
    }

    [Fact]
    public void ApplyNetworkPreferences_EmitsExpectedProperties()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyNetworkPreferences(new NetworkPreferencesProfile
            {
                PreferIpv6 = true,
                AllowMeteredConnections = false,
                NetworkTimeoutSeconds = 42,
                ProxyMode = NetworkProxyMode.Http,
                ProxyUrl = "http://127.0.0.1:8080"
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Contains(("network-timeout", "42"), emitted);
        Assert.DoesNotContain(emitted, pair => pair.Property == "ipv6");
        Assert.Contains(("cache-pause-wait", "2"), emitted);
        Assert.Contains(("http-proxy", "http://127.0.0.1:8080"), emitted);
    }

    [Fact]
    public void ApplyNetworkPreferences_ClearsHttpProxyWhenNotUsingHttpProxyMode()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyNetworkPreferences(new NetworkPreferencesProfile
            {
                ProxyMode = NetworkProxyMode.Http,
                ProxyUrl = "http://127.0.0.1:8080"
            });
            service.ApplyNetworkPreferences(new NetworkPreferencesProfile
            {
                ProxyMode = NetworkProxyMode.System,
                ProxyUrl = "http://should-not-stick"
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Contains(("http-proxy", "http://127.0.0.1:8080"), emitted);
        Assert.Contains(("http-proxy", string.Empty), emitted);
    }

    [Fact]
    public void ApplyCustomizationPreferences_EmitsExpectedProperties()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyCustomizationPreferences(new CustomizationPreferencesProfile
            {
                AccentHex = "#334455",
                UseSystemTheme = false,
                DenseLayout = true,
                ShowPlaybackHud = false
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Empty(emitted);
    }

    [Fact]
    public void ApplyCustomizationPreferences_NormalizesInvalidAccentHex()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyCustomizationPreferences(new CustomizationPreferencesProfile
            {
                AccentHex = "bad-value"
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Empty(emitted);
    }

    [Fact]
    public void ApplySubtitlePreferences_EmitsSubtitleDelay()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplySubtitlePreferences(new SubtitlePreferencesProfile
            {
                SubtitleDelaySeconds = 1.75
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.Contains(("sub-delay", "1.75"), emitted);
    }

    [Fact]
    public void ApplyPlaybackPreferences_DoesNotEmitConfigOnlySavePositionOption()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyPlaybackPreferences(new PlaybackPreferencesProfile
            {
                ResumeWhereLeftOff = true,
                AutoplayOnOpen = true
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.DoesNotContain(("save-position-on-quit", "yes"), emitted);
        Assert.DoesNotContain(("save-position-on-quit", "no"), emitted);
        Assert.DoesNotContain(("keep-open", "no"), emitted);
        Assert.DoesNotContain(("keep-open", "yes"), emitted);
        Assert.DoesNotContain(("playlist-next", "force"), emitted);
    }

    [Fact]
    public void ApplyVideoPreferences_DoesNotEmitVoRuntimeProperty()
    {
        var service = PlaybackOptionsService.Instance;
        var emitted = new List<(string Property, string Value)>();
        EventHandler<PlaybackPropertyApplyEventArgs> handler = (_, args) => emitted.Add((args.PropertyName, args.PropertyValue));
        service.PropertyApplyRequested += handler;
        try
        {
            service.ApplyVideoPreferences(new VideoPreferencesProfile
            {
                OutputMode = VideoOutputMode.Gpu
            });
        }
        finally
        {
            service.PropertyApplyRequested -= handler;
        }

        Assert.DoesNotContain(emitted, pair => pair.Property == "vo");
    }
}
