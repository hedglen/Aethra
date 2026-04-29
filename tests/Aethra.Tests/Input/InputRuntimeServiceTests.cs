using Aethra.Input;
using Windows.System;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class InputRuntimeServiceTests
{
    [Theory]
    [InlineData("CTRL+SHIFT+A", "A", true, true, false)]
    [InlineData("ALT+MBTN_RIGHT", "MBTN_RIGHT", false, false, true)]
    [InlineData("WHEEL_DOWN", "WHEEL_DOWN", false, false, false)]
    [InlineData("KP_HOME", "NUMBER7", false, false, false)]
    [InlineData("KP7", "NUMBER7", false, false, false)]
    [InlineData("ESC", "ESCAPE", false, false, false)]
    public void TryParseGesture_ParsesSupportedTokens(string text, string expectedPrimary, bool ctrl, bool shift, bool alt)
    {
        var parsed = InputRuntimeService.TryParseGesture(text, out var gesture);

        Assert.True(parsed);
        Assert.Equal(expectedPrimary, gesture.Primary);
        Assert.Equal(ctrl, gesture.Ctrl);
        Assert.Equal(shift, gesture.Shift);
        Assert.Equal(alt, gesture.Alt);
    }

    [Fact]
    public void TryParseGesture_UsesFirstAliasSegment_WhenSlashFormatProvided()
    {
        var parsed = InputRuntimeService.TryParseGesture("KP_HOME / KP7", out var gesture);

        Assert.True(parsed);
        Assert.Equal("NUMBER7", gesture.Primary);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("CTRL+")]
    [InlineData("UNKNOWN_TOKEN")]
    public void TryParseGesture_RejectsInvalidText(string text)
    {
        var parsed = InputRuntimeService.TryParseGesture(text, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void LoadBindings_UsesLatestCommand_ForDuplicateGesture()
    {
        var runtime = new InputRuntimeService();
        runtime.LoadBindings(new[]
        {
            new InputBindingSetting("Aethra", "CTRL+A", "aethra:first", "first", "Native"),
            new InputBindingSetting("Aethra", "CTRL+A", "aethra:second", "second", "Native")
        });

        var found = runtime.TryGetCommand(new InputGesture("A", Ctrl: true, Shift: false, Alt: false), out var command);

        Assert.True(found);
        Assert.Equal("aethra:second", command);
    }

    [Fact]
    public void FromVirtualKey_NormalizesToPrimaryToken()
    {
        var gesture = InputGesture.FromVirtualKey(VirtualKey.PageDown, ctrl: false, shift: true, alt: false);

        Assert.Equal("PAGEDOWN", gesture.Primary);
        Assert.False(gesture.Ctrl);
        Assert.True(gesture.Shift);
        Assert.False(gesture.Alt);
    }
}
