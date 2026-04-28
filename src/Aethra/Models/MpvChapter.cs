namespace Aethra.Models;

/// <summary>
/// A single chapter as reported by mpv via chapter-list/N/{time,title}.
/// Time is in seconds from the start of the file.
/// </summary>
internal readonly record struct MpvChapter(double Time, string? Title);
