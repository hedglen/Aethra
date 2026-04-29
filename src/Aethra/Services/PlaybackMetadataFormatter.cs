using System;
using System.Globalization;
using Aethra.Models;

namespace Aethra.Services;

internal static class PlaybackMetadataFormatter
{
    internal static string FormatPlaybackTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            return "0:00";

        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    internal static string GetChapterTitle(MpvChapter chapter, int chapterIndex)
    {
        return string.IsNullOrWhiteSpace(chapter.Title)
            ? $"Chapter {chapterIndex + 1}"
            : chapter.Title!;
    }
}
