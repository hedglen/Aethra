using Aethra.Input;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class InputCommandSupportTests
{
    [Theory]
    [InlineData("aethra:play-pause")]
    [InlineData("cycle pause")]
    [InlineData("cycle mute")]
    [InlineData("seek -10")]
    [InlineData("seek 40 absolute-percent")]
    [InlineData("add volume 5")]
    [InlineData("set pause yes")]
    [InlineData("show-text \"Ready\"")]
    [InlineData("change-list glsl-shaders clr ; show-text \"Reset\"")]
    [InlineData("loadfile C:/video.mp4 replace")]
    [InlineData("change-list glsl-shaders set \"~~/shaders/test.glsl\"")]
    [InlineData("script-binding playlistmanager/showplaylist")]
    [InlineData("script-message-to favorites favorites-open")]
    [InlineData("playlist-prev")]
    [InlineData("playlist-next")]
    public void IsSupportedCommand_ReturnsTrue_ForSupportedPatterns(string command)
    {
        Assert.True(InputCommandSupport.IsSupportedCommand(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("set \"bad")]
    [InlineData("run calc.exe")]
    [InlineData("subprocess ffmpeg -version")]
    [InlineData("script-message-to stats display-page-4")]
    public void IsSupportedCommand_ReturnsFalse_ForUnsupportedPatterns(string command)
    {
        Assert.False(InputCommandSupport.IsSupportedCommand(command));
    }

    [Theory]
    [InlineData("run calc.exe", "Blocked command verb: run")]
    [InlineData("set \"bad", "Command parse error (check quotes/spacing).")]
    public void TryGetUnsupportedReason_ReturnsExpectedMessage(string command, string expectedReason)
    {
        var unsupported = InputCommandSupport.TryGetUnsupportedReason(command, out var reason);

        Assert.True(unsupported);
        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void ClassifyCommand_ReturnsExpectedClassification()
    {
        Assert.Equal(InputCommandSupport.CommandClassification.NativeAlias, InputCommandSupport.ClassifyCommand("cycle pause", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.NativeAlias, InputCommandSupport.ClassifyCommand("aethra:play-pause", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.NativeAlias, InputCommandSupport.ClassifyCommand("script-message-to favorites favorites-open", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.NativeAlias, InputCommandSupport.ClassifyCommand("playlist-prev", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.NativeAlias, InputCommandSupport.ClassifyCommand("playlist-next", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.PassthroughSafe, InputCommandSupport.ClassifyCommand("show-text \"ok\"", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.Blocked, InputCommandSupport.ClassifyCommand("run calc.exe", out _));
        Assert.Equal(InputCommandSupport.CommandClassification.Invalid, InputCommandSupport.ClassifyCommand("set \"bad", out _));
    }

    [Fact]
    public void TryGetNativeAlias_ReturnsExpectedAlias()
    {
        AssertAlias("script-binding uosc/menu", InputCommandSupport.NativeAlias.ToggleSettings);
        AssertAlias("set fullscreen no", InputCommandSupport.NativeAlias.ExitFullscreen);
        AssertAlias("quit", InputCommandSupport.NativeAlias.Quit);
        AssertAlias("quit-watch-later", InputCommandSupport.NativeAlias.QuitWatchLater);
        AssertAlias("script-message-to favorites favorites-open", InputCommandSupport.NativeAlias.ShowFavorites);
        AssertAlias("playlist-prev", InputCommandSupport.NativeAlias.PreviousFile);
        AssertAlias("playlist-next", InputCommandSupport.NativeAlias.NextFile);
    }

    private static void AssertAlias(string command, InputCommandSupport.NativeAlias expectedAlias)
    {
        _ = MpvCommandLineParser.TryParseCommandChain(command, out var commands);
        Assert.True(InputCommandSupport.TryGetNativeAlias(commands[0], out var alias));
        Assert.Equal(expectedAlias, alias);
    }
}
