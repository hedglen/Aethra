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
            expected.Audio.DynamicRangeCompression = true;
            expected.Audio.ChannelLayout = AudioChannelLayout.Surround51;
            expected.Subtitles.PreferredLanguagesCsv = "eng,spa";
            expected.Library.WatchFoldersEnabled = true;
            expected.Profiles.ActiveProfileName = "Cinema";

            PreferencesProfilesStore.SaveToPath(path, expected);
            var loaded = PreferencesProfilesStore.LoadFromPath(path);

            Assert.Equal(PlaybackEndOfFileAction.LoopCurrentFile, loaded.Playback.EndOfFileAction);
            Assert.Equal(125, loaded.Playback.DefaultPlaybackSpeedPercent);
            Assert.True(loaded.Audio.DynamicRangeCompression);
            Assert.Equal(AudioChannelLayout.Surround51, loaded.Audio.ChannelLayout);
            Assert.Equal("eng,spa", loaded.Subtitles.PreferredLanguagesCsv);
            Assert.True(loaded.Library.WatchFoldersEnabled);
            Assert.Equal("Cinema", loaded.Profiles.ActiveProfileName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
