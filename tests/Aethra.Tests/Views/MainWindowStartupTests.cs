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
