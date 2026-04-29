using Aethra.Input;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class MpvCommandLineParserTests
{
    [Fact]
    public void TryParseCommandChain_SplitsSemicolonOutsideQuotes()
    {
        var parsed = MpvCommandLineParser.TryParseCommandChain(
            "change-list glsl-shaders set \"a;b\" ; show-text \"done\"",
            out var commands);

        Assert.True(parsed);
        Assert.Equal(2, commands.Count);
        Assert.Equal("change-list", commands[0][0]);
        Assert.Equal("a;b", commands[0][3]);
        Assert.Equal("show-text", commands[1][0]);
        Assert.Equal("done", commands[1][1]);
    }

    [Fact]
    public void TryParseCommandChain_ReturnsFalse_ForUnterminatedQuote()
    {
        var parsed = MpvCommandLineParser.TryParseCommandChain("set title \"abc", out _);
        Assert.False(parsed);
    }
}
