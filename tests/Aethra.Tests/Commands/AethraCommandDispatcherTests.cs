using Aethra.Commands;
using Xunit;

namespace Aethra.Tests.Commands;

public sealed class AethraCommandDispatcherTests
{
    [Fact]
    public void Execute_ReturnsFalse_ForUnknownCommand()
    {
        var invocations = new Dictionary<string, int>(StringComparer.Ordinal);
        var dispatcher = new AethraCommandDispatcher(CreateContext(invocations));

        var handled = dispatcher.Execute("aethra:unknown");

        Assert.False(handled);
        Assert.Empty(invocations);
    }

    [Theory]
    [MemberData(nameof(CommandCases))]
    public void Execute_InvokesExpectedAction_ForKnownCommand(string command, params string[] expectedActions)
    {
        var invocations = new Dictionary<string, int>(StringComparer.Ordinal);
        var dispatcher = new AethraCommandDispatcher(CreateContext(invocations));

        var handled = dispatcher.Execute(command);

        Assert.True(handled);
        Assert.Equal(expectedActions.Length, invocations.Values.Sum());
        foreach (var expected in expectedActions)
            Assert.Equal(1, invocations.GetValueOrDefault(expected));
    }

    [Theory]
    [InlineData("aethra:settings", true)]
    [InlineData("script-binding stats/display-stats-toggle", false)]
    public void IsAethraCommand_UsesAethraPrefix(string command, bool expected)
    {
        Assert.Equal(expected, AethraCommandIds.IsAethraCommand(command));
    }

    public static IEnumerable<object[]> CommandCases()
    {
        yield return new object[] { AethraCommandIds.BossKey, new[] { "PausePlayback", "MinimizeWindow" } };
        yield return new object[] { AethraCommandIds.ToggleSettings, new[] { "ToggleSettings" } };
        yield return new object[] { AethraCommandIds.ToggleFullscreen, new[] { "ToggleFullscreen" } };
        yield return new object[] { AethraCommandIds.TogglePlayPause, new[] { "TogglePlayPause" } };
        yield return new object[] { AethraCommandIds.Quit, new[] { "Quit" } };
        yield return new object[] { AethraCommandIds.QuitWatchLater, new[] { "QuitWatchLater" } };
        yield return new object[] { AethraCommandIds.SeekBack5, new[] { "SeekBack5" } };
        yield return new object[] { AethraCommandIds.SeekForward5, new[] { "SeekForward5" } };
        yield return new object[] { AethraCommandIds.SeekBack10, new[] { "SeekBack10" } };
        yield return new object[] { AethraCommandIds.SeekForward30, new[] { "SeekForward30" } };
        yield return new object[] { AethraCommandIds.SeekBack60, new[] { "SeekBack60" } };
        yield return new object[] { AethraCommandIds.SeekForward60, new[] { "SeekForward60" } };
        yield return new object[] { AethraCommandIds.SeekBack300, new[] { "SeekBack300" } };
        yield return new object[] { AethraCommandIds.SeekForward300, new[] { "SeekForward300" } };
        yield return new object[] { AethraCommandIds.VolumeUp2, new[] { "VolumeUp2" } };
        yield return new object[] { AethraCommandIds.VolumeDown2, new[] { "VolumeDown2" } };
        yield return new object[] { AethraCommandIds.VolumeUp5, new[] { "VolumeUp5" } };
        yield return new object[] { AethraCommandIds.VolumeDown5, new[] { "VolumeDown5" } };
        yield return new object[] { AethraCommandIds.VolumeUp10, new[] { "VolumeUp10" } };
        yield return new object[] { AethraCommandIds.VolumeDown10, new[] { "VolumeDown10" } };
        yield return new object[] { AethraCommandIds.ToggleMute, new[] { "ToggleMute" } };
        yield return new object[] { AethraCommandIds.ExitOverlayOrFullscreen, new[] { "EscapeAction" } };
        yield return new object[] { AethraCommandIds.MarkLoopA, new[] { "MarkLoopA" } };
        yield return new object[] { AethraCommandIds.MarkLoopB, new[] { "MarkLoopB" } };
        yield return new object[] { AethraCommandIds.ResetLoop, new[] { "ResetLoop" } };
        yield return new object[] { AethraCommandIds.ToggleLoopFile, new[] { "ToggleLoopFile" } };
        yield return new object[] { AethraCommandIds.OpenFile, new[] { "OpenFile" } };
        yield return new object[] { AethraCommandIds.OpenFolder, new[] { "OpenFolder" } };
        yield return new object[] { AethraCommandIds.OpenRecent, new[] { "OpenRecent" } };
        yield return new object[] { AethraCommandIds.ShowPlaylist, new[] { "ShowPlaylist" } };
        yield return new object[] { AethraCommandIds.ShowTools, new[] { "ShowTools" } };
        yield return new object[] { AethraCommandIds.ShowHelp, new[] { "ShowHelp" } };
        yield return new object[] { AethraCommandIds.ShowFavorites, new[] { "ShowFavorites" } };
        yield return new object[] { AethraCommandIds.ToggleAdjustments, new[] { "ToggleAdjustments" } };
        yield return new object[] { AethraCommandIds.ToggleCommandRail, new[] { "ToggleCommandRail" } };
    }

    private static AethraCommandContext CreateContext(IDictionary<string, int> invocations)
    {
        void Mark(string name)
        {
            invocations.TryGetValue(name, out var current);
            invocations[name] = current + 1;
        }

        return new AethraCommandContext(
            () => Mark("PausePlayback"),
            () => Mark("MinimizeWindow"),
            () => Mark("ToggleSettings"),
            () => Mark("ToggleFullscreen"),
            () => Mark("TogglePlayPause"),
            () => Mark("Quit"),
            () => Mark("QuitWatchLater"),
            () => Mark("SeekBack5"),
            () => Mark("SeekForward5"),
            () => Mark("SeekBack10"),
            () => Mark("SeekForward30"),
            () => Mark("SeekBack60"),
            () => Mark("SeekForward60"),
            () => Mark("SeekBack300"),
            () => Mark("SeekForward300"),
            () => Mark("VolumeUp2"),
            () => Mark("VolumeDown2"),
            () => Mark("VolumeUp5"),
            () => Mark("VolumeDown5"),
            () => Mark("VolumeUp10"),
            () => Mark("VolumeDown10"),
            () => Mark("ToggleMute"),
            () => Mark("EscapeAction"),
            () => Mark("MarkLoopA"),
            () => Mark("MarkLoopB"),
            () => Mark("ResetLoop"),
            () => Mark("ToggleLoopFile"),
            () => Mark("OpenFile"),
            () => Mark("OpenFolder"),
            () => Mark("OpenRecent"),
            () => Mark("ShowPlaylist"),
            () => Mark("ShowTools"),
            () => Mark("ShowHelp"),
            () => Mark("ShowFavorites"),
            () => Mark("ToggleAdjustments"),
            () => Mark("ToggleCommandRail"));
    }
}
