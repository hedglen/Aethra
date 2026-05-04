using System;
using System.IO;
using Xunit;

namespace Aethra.Tests.Views;

public sealed class MainWindowStartupTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void ShouldQueueMediaLoad_DependsOnActiveBackendCount(int activeBackendCount, bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldQueueMediaLoad(activeBackendCount));
    }

    [Fact]
    public void ResolveStartupMediaCandidate_PrefersPreferredPath()
    {
        var preferredPath = CreateTempMediaPath();
        var persistedPath = CreateTempMediaPath();
        try
        {
            var resolved = MainWindow.ResolveStartupMediaCandidate(preferredPath, persistedPath, out var shouldResumePersistedPosition);

            Assert.Equal(preferredPath, resolved);
            Assert.False(shouldResumePersistedPosition);
        }
        finally
        {
            DeleteIfExists(preferredPath);
            DeleteIfExists(persistedPath);
        }
    }

    [Fact]
    public void ResolveStartupMediaCandidate_FallsBackToPersistedPath()
    {
        var persistedPath = CreateTempMediaPath();
        try
        {
            var resolved = MainWindow.ResolveStartupMediaCandidate("Z:\\not-found\\test.mp4", persistedPath, out var shouldResumePersistedPosition);

            Assert.Equal(persistedPath, resolved);
            Assert.True(shouldResumePersistedPosition);
        }
        finally
        {
            DeleteIfExists(persistedPath);
        }
    }

    [Fact]
    public void ResolveStartupMediaCandidate_ReturnsNullWhenNoPathExists()
    {
        var resolved = MainWindow.ResolveStartupMediaCandidate("Z:\\not-found\\test.mp4", "Z:\\not-found\\persisted.mp4", out var shouldResumePersistedPosition);

        Assert.Null(resolved);
        Assert.False(shouldResumePersistedPosition);
    }

    [Fact]
    public void ResolveStartupMediaCandidate_AcceptsHttpUri()
    {
        var resolved = MainWindow.ResolveStartupMediaCandidate("https://example.com/video.mp4", null, out var shouldResumePersistedPosition);

        Assert.Equal("https://example.com/video.mp4", resolved);
        Assert.False(shouldResumePersistedPosition);
    }

    [Fact]
    public void PreferredStartupMediaPath_ReturnsEnvVarValue_WhenSet()
    {
        var sentinel = @"C:\fake\override.mp4";
        var previous = Environment.GetEnvironmentVariable("AETHRA_STARTUP_MEDIA");
        try
        {
            Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", sentinel);

            Assert.Equal(sentinel, MainWindow.PreferredStartupMediaPathForTests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", previous);
        }
    }

    [Fact]
    public void PreferredStartupMediaPath_ReturnsNull_WhenEnvVarUnset()
    {
        var previous = Environment.GetEnvironmentVariable("AETHRA_STARTUP_MEDIA");
        try
        {
            Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", null);

            Assert.Null(MainWindow.PreferredStartupMediaPathForTests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AETHRA_STARTUP_MEDIA", previous);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  C:\\media\\file.mp4 ", "C:\\media\\file.mp4")]
    public void NormalizeMediaTarget_TrimsOrNulls(string? input, string? expected)
    {
        var normalized = MainWindow.NormalizeMediaTarget(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("rtsp://example.com/live", true)]
    [InlineData("https://example.com/v.mp4", true)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("not-a-uri", false)]
    public void IsPlayableMediaTarget_HandlesUriSchemes(string target, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsPlayableMediaTarget(target));
    }

    [Theory]
    [InlineData(null, 100, null)]
    [InlineData(0.0, 100.0, null)]
    [InlineData(-1.0, 100.0, null)]
    [InlineData(30.0, 100.0, 30.0)]
    [InlineData(120.0, 100.0, 99.75)]
    [InlineData(30.0, 0.0, 30.0)]
    public void NormalizeResumeSeekTarget_ClampsToSafePlayableRange(double? seconds, double durationSeconds, double? expected)
    {
        Assert.Equal(expected, MainWindow.NormalizeResumeSeekTarget(seconds, durationSeconds));
    }

    private static string CreateTempMediaPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        File.WriteAllText(path, "test");
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
