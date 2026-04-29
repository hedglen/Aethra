using Aethra.Configuration;
using Aethra.Profiles;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class PreferencesProfileBundleExchangeTests
{
    [Fact]
    public void ExportThenImport_RoundTripsProfileBundles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aethra-profile-exchange-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "bundles.json");
        try
        {
            var profiles = ProfilesPreferencesProfile.CreateDefault();
            profiles.ActiveProfileName = "Cinema";
            profiles.Bundles = new List<NamedPreferencesProfileBundle>
            {
                new()
                {
                    Name = "Default",
                    Playback = PlaybackPreferencesProfile.CreateDefault()
                },
                new()
                {
                    Name = "Cinema",
                    Playback = new PlaybackPreferencesProfile { DefaultPlaybackSpeedPercent = 125 },
                    Network = new NetworkPreferencesProfile
                    {
                        NetworkTimeoutSeconds = 50,
                        ProxyMode = NetworkProxyMode.Http,
                        ProxyUrl = "http://127.0.0.1:8080"
                    },
                    Customization = new CustomizationPreferencesProfile
                    {
                        AccentHex = "#223344",
                        DenseLayout = true
                    }
                }
            };

            PreferencesProfileBundleExchange.ExportToPath(path, profiles);
            var ok = PreferencesProfileBundleExchange.TryImportFromPath(path, out var imported, out var error);

            Assert.True(ok, error);
            Assert.Equal("Cinema", imported.ActiveProfileName);
            Assert.Equal(2, imported.Bundles.Count);
            var cinema = Assert.Single(imported.Bundles, bundle => bundle.Name == "Cinema");
            Assert.Equal(125, cinema.Playback.DefaultPlaybackSpeedPercent);
            Assert.Equal(50, cinema.Network.NetworkTimeoutSeconds);
            Assert.Equal(NetworkProxyMode.Http, cinema.Network.ProxyMode);
            Assert.Equal("#223344", cinema.Customization.AccentHex);
            Assert.True(cinema.Customization.DenseLayout);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryImportFromPath_RejectsUnsupportedSchema()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aethra-profile-exchange-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "bundles.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "SchemaVersion": 99,
                  "ActiveProfileName": "Default",
                  "Bundles": []
                }
                """);
            var ok = PreferencesProfileBundleExchange.TryImportFromPath(path, out _, out var error);

            Assert.False(ok);
            Assert.Contains("Unsupported schema version", error);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
