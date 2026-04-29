using Aethra.Profiles;
using Xunit;

namespace Aethra.Tests.Profiles;

public sealed class PreferencesPageProfilesTests
{
    [Fact]
    public void CreateDefault_ReturnsExpectedBaselineValues()
    {
        var defaults = PreferencesPageProfiles.CreateDefault();

        Assert.True(defaults.Playback.ResumeWhereLeftOff);
        Assert.True(defaults.Playback.AutoplayOnOpen);
        Assert.Equal(PlaybackEndOfFileAction.Stop, defaults.Playback.EndOfFileAction);
        Assert.Equal(100, defaults.Playback.DefaultPlaybackSpeedPercent);

        Assert.Equal(VideoOutputMode.GpuNext, defaults.Video.OutputMode);
        Assert.Equal(HardwareDecodeMode.Auto, defaults.Video.HardwareDecode);
        Assert.False(defaults.Video.InterpolationEnabled);
        Assert.False(defaults.Video.DeinterlaceEnabled);

        Assert.Equal("System default", defaults.Audio.OutputDevice);
        Assert.Equal(AudioChannelLayout.Auto, defaults.Audio.ChannelLayout);
        Assert.False(defaults.Audio.DynamicRangeCompression);
        Assert.False(defaults.Audio.ReplayGainNormalization);

        Assert.True(defaults.Subtitles.AutoLoadMatchingSubtitles);
        Assert.Equal("eng,jpn", defaults.Subtitles.PreferredLanguagesCsv);
        Assert.Equal(40, defaults.Subtitles.FontSize);
        Assert.True(defaults.Subtitles.BorderAndShadow);

        Assert.False(defaults.Library.WatchFoldersEnabled);
        Assert.True(defaults.Library.RememberRecentFiles);
        Assert.Equal("Default", defaults.Profiles.ActiveProfileName);
        Assert.Single(defaults.Profiles.Bundles);
        Assert.Equal("Default", defaults.Profiles.Bundles[0].Name);

        Assert.Equal(AdvancedLogLevel.Warnings, defaults.Advanced.LogLevel);
        Assert.Equal(string.Empty, defaults.Advanced.ExtraMpvOptionsText);
    }
}
