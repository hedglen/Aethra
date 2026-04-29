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
}
