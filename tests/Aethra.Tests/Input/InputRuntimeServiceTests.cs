using Aethra.Input;
using Windows.System;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class InputRuntimeServiceTests
{
    [Theory]
    [InlineData("CTRL+SHIFT+q", "Q", true, true, false)]
    [InlineData("ALT+MBTN_RIGHT", "MBTN_RIGHT", false, false, true)]
    [InlineData("WHEEL_DOWN", "WHEEL_DOWN", false, false, false)]
    [InlineData("KP_HOME", "NUMBERPAD7", false, false, false)]
    [InlineData("KP7", "NUMBERPAD7", false, false, false)]
    [InlineData("CTRL+b", "B", true, false, false)]
    [InlineData("CTRL+B", "B", true, true, false)]
    [InlineData("q", "Q", false, false, false)]
    [InlineData("Q", "Q", false, true, false)]
    [InlineData(",", "COMMA", false, false, false)]
    [InlineData("<", "COMMA", false, true, false)]
    [InlineData("[", "OPENBRACKET", false, false, false)]
    [InlineData("]", "CLOSEBRACKET", false, false, false)]
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
        Assert.Equal("NUMBERPAD7", gesture.Primary);
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
            new InputBindingSetting("Aethra", "CTRL+a", "aethra:first", "first", "Native"),
            new InputBindingSetting("Aethra", "CTRL+a", "aethra:second", "second", "Native")
        });

        var found = runtime.TryGetCommand(new InputGesture("A", Ctrl: true, Shift: false, Alt: false), out var command);

        Assert.True(found);
        Assert.Equal("aethra:second", command);
    }

    [Fact]
    public void LoadBindings_PreservesCaseDistinctLetterGestures()
    {
        var runtime = new InputRuntimeService();
        runtime.LoadBindings(new[]
        {
            new InputBindingSetting("Aethra", "q", "aethra:lower", "lower", "Native"),
            new InputBindingSetting("Aethra", "Q", "aethra:upper", "upper", "Native")
        });

        var lowerFound = runtime.TryGetCommand(new InputGesture("Q", Ctrl: false, Shift: false, Alt: false), out var lower);
        var upperFound = runtime.TryGetCommand(new InputGesture("Q", Ctrl: false, Shift: true, Alt: false), out var upper);

        Assert.True(lowerFound);
        Assert.True(upperFound);
        Assert.Equal("aethra:lower", lower);
        Assert.Equal("aethra:upper", upper);
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

    [Fact]
    public void FromVirtualKey_NumberPadUsesDedicatedPrimaryToken()
    {
        var gesture = InputGesture.FromVirtualKey(VirtualKey.NumberPad7, ctrl: false, shift: false, alt: false);

        Assert.Equal("NUMBERPAD7", gesture.Primary);
        Assert.False(gesture.Ctrl);
        Assert.False(gesture.Shift);
        Assert.False(gesture.Alt);
    }
}
