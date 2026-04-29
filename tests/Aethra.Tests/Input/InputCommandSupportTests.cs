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
    [InlineData("script-message-to favorites favorites-open")]
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
}
