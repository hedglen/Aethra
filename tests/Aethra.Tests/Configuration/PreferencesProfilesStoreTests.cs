using Aethra.Configuration;
using Aethra.Profiles;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class PreferencesProfilesStoreTests
{
    [Fact]
    public void LoadFromPath_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aethra-pref-missing-{Guid.NewGuid():N}.json");

        var loaded = PreferencesProfilesStore.LoadFromPath(path);

        Assert.Equal("Default", loaded.Profiles.ActiveProfileName);
        Assert.Equal(PlaybackEndOfFileAction.Stop, loaded.Playback.EndOfFileAction);
    }

    [Fact]
    public void SaveToPath_ThenLoadFromPath_RoundTripsProfiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aethra-pref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "prefs.json");
        try
        {
            var expected = PreferencesPageProfiles.CreateDefault();
            expected.Playback.EndOfFileAction = PlaybackEndOfFileAction.LoopCurrentFile;
            expected.Playback.DefaultPlaybackSpeedPercent = 125;
            expected.Video.OutputMode = VideoOutputMode.Gpu;
            expected.Video.HardwareDecode = HardwareDecodeMode.Nvdec;
            expected.Video.InterpolationEnabled = true;
            expected.Video.QualityPreset = VideoQualityPreset.Cinema;
            expected.Video.ShaderPreset = ShaderChainPreset.None;
            expected.Video.CustomShaderChain = "~~/shaders/custom.glsl";
            expected.Audio.DynamicRangeCompression = true;
            expected.Audio.OutputDevice = "wasapi/{test-device}";
            expected.Audio.ChannelLayout = AudioChannelLayout.Surround51;
            expected.Subtitles.PreferredLanguagesCsv = "eng,spa";
            expected.Subtitles.SubtitleDelaySeconds = -1.5;
            expected.Library.WatchFoldersEnabled = true;
            expected.Profiles.ActiveProfileName = "Cinema";
            expected.Profiles.Bundles.Add(new NamedPreferencesProfileBundle
            {
                Name = "Cinema",
                Playback = PlaybackPreferencesProfile.CreateDefault(),
                Video = VideoPreferencesProfile.CreateDefault(),
                Audio = AudioPreferencesProfile.CreateDefault(),
                Subtitles = SubtitlePreferencesProfile.CreateDefault(),
                Library = LibraryPreferencesProfile.CreateDefault(),
                Advanced = AdvancedPreferencesProfile.CreateDefault(),
                Network = NetworkPreferencesProfile.CreateDefault(),
                Customization = CustomizationPreferencesProfile.CreateDefault()
            });
            expected.Advanced.LogLevel = AdvancedLogLevel.Debug;
            expected.Advanced.ExtraMpvOptionsText = "demuxer-max-bytes=128MiB";
            expected.Network.PreferIpv6 = true;
            expected.Network.AllowMeteredConnections = false;
            expected.Network.NetworkTimeoutSeconds = 45;
            expected.Network.ProxyMode = NetworkProxyMode.Http;
            expected.Network.ProxyUrl = "http://127.0.0.1:8080";
            expected.Customization.AccentHex = "#123456";
            expected.Customization.DenseLayout = true;
            expected.Customization.ShowPlaybackHud = false;

            PreferencesProfilesStore.SaveToPath(path, expected);
            var loaded = PreferencesProfilesStore.LoadFromPath(path);

            Assert.Equal(PlaybackEndOfFileAction.LoopCurrentFile, loaded.Playback.EndOfFileAction);
            Assert.Equal(125, loaded.Playback.DefaultPlaybackSpeedPercent);
            Assert.Equal(VideoOutputMode.Gpu, loaded.Video.OutputMode);
            Assert.Equal(HardwareDecodeMode.Nvdec, loaded.Video.HardwareDecode);
            Assert.True(loaded.Video.InterpolationEnabled);
            Assert.Equal(VideoQualityPreset.Cinema, loaded.Video.QualityPreset);
            Assert.Equal(ShaderChainPreset.None, loaded.Video.ShaderPreset);
            Assert.Equal("~~/shaders/custom.glsl", loaded.Video.CustomShaderChain);
            Assert.True(loaded.Audio.DynamicRangeCompression);
            Assert.Equal("wasapi/{test-device}", loaded.Audio.OutputDevice);
            Assert.Equal(AudioChannelLayout.Surround51, loaded.Audio.ChannelLayout);
            Assert.Equal("eng,spa", loaded.Subtitles.PreferredLanguagesCsv);
            Assert.Equal(-1.5, loaded.Subtitles.SubtitleDelaySeconds);
            Assert.True(loaded.Library.WatchFoldersEnabled);
            Assert.Equal("Cinema", loaded.Profiles.ActiveProfileName);
            Assert.Contains(loaded.Profiles.Bundles, bundle => bundle.Name == "Cinema");
            Assert.Equal(AdvancedLogLevel.Debug, loaded.Advanced.LogLevel);
            Assert.Equal("demuxer-max-bytes=128MiB", loaded.Advanced.ExtraMpvOptionsText);
            Assert.True(loaded.Network.PreferIpv6);
            Assert.False(loaded.Network.AllowMeteredConnections);
            Assert.Equal(45, loaded.Network.NetworkTimeoutSeconds);
            Assert.Equal(NetworkProxyMode.Http, loaded.Network.ProxyMode);
            Assert.Equal("http://127.0.0.1:8080", loaded.Network.ProxyUrl);
            Assert.Equal("#123456", loaded.Customization.AccentHex);
            Assert.True(loaded.Customization.DenseLayout);
            Assert.False(loaded.Customization.ShowPlaybackHud);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
