using Aethra.Models;
using Aethra.Services;
using Xunit;

namespace Aethra.Tests.Services;

public sealed class PlaybackMetadataFormatterTests
{
    [Theory]
    [InlineData(-1, "0:00")]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(61, "1:01")]
    [InlineData(3661, "1:01:01")]
    public void FormatPlaybackTime_ReturnsExpectedText(double seconds, string expected)
    {
        Assert.Equal(expected, PlaybackMetadataFormatter.FormatPlaybackTime(seconds));
    }

    [Fact]
    public void GetChapterTitle_FallsBackToIndexedTitle()
    {
        var title = PlaybackMetadataFormatter.GetChapterTitle(new MpvChapter(12, null), 2);
        Assert.Equal("Chapter 3", title);
    }
}
