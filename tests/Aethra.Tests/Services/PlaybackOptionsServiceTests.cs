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

        Assert.Contains(("vo", "gpu"), emitted);
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
        Assert.Contains(("ipv6", "yes"), emitted);
        Assert.Contains(("cache-pause-wait", "2"), emitted);
        Assert.Contains(("http-proxy", "http://127.0.0.1:8080"), emitted);
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

        Assert.Contains(("aethra-accent-hex", "#334455"), emitted);
        Assert.Contains(("aethra-use-system-theme", "no"), emitted);
        Assert.Contains(("aethra-dense-layout", "yes"), emitted);
        Assert.Contains(("aethra-show-playback-hud", "no"), emitted);
    }
}
