using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aethra.Services;

internal enum FolderMediaNavigationDirection
{
    Previous = -1,
    Next = 1
}

internal enum FolderMediaNavigationKind
{
    None,
    First,
    Previous,
    Next,
    BoundaryBeforeFirst,
    BoundaryAfterLast
}

internal readonly record struct FolderMediaNavigationResult(FolderMediaNavigationKind Kind, string? Path)
{
    internal bool HasPath => !string.IsNullOrWhiteSpace(Path);
}

internal static class FolderMediaNavigator
{
    private static readonly string[] SupportedExtensions =
    {
        ".mp4",
        ".mkv",
        ".mov",
        ".avi",
        ".webm",
        ".m4v",
        ".mp3",
        ".flac",
        ".wav",
        ".m4a"
    };

    internal static bool IsSupportedMediaPath(string? path)
    {
        if (!TryNormalizeLocalPath(path, out var normalizedPath))
            return false;

        var extension = Path.GetExtension(normalizedPath);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    internal static FolderMediaNavigationResult GetFirstInFolder(string? folderPath)
    {
        if (!TryNormalizeLocalPath(folderPath, out var normalizedFolderPath))
            return default;

        var orderedFiles = GetOrderedSupportedMediaFiles(normalizedFolderPath);
        if (orderedFiles.Count == 0)
            return default;

        return new FolderMediaNavigationResult(FolderMediaNavigationKind.First, orderedFiles[0]);
    }

    internal static FolderMediaNavigationResult GetAdjacentFile(string? currentPath, FolderMediaNavigationDirection direction)
    {
        if (!TryNormalizeLocalPath(currentPath, out var normalizedCurrentPath) || !File.Exists(normalizedCurrentPath))
            return default;

        var folderPath = Path.GetDirectoryName(normalizedCurrentPath);
        if (string.IsNullOrWhiteSpace(folderPath))
            return default;

        var orderedFiles = GetOrderedSupportedMediaFiles(folderPath);
        if (orderedFiles.Count == 0)
            return default;

        var currentIndex = orderedFiles.FindIndex(
            candidate => string.Equals(candidate, normalizedCurrentPath, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
            return default;

        var targetIndex = currentIndex + (int)direction;
        if (targetIndex < 0)
            return new FolderMediaNavigationResult(FolderMediaNavigationKind.BoundaryBeforeFirst, null);

        if (targetIndex >= orderedFiles.Count)
            return new FolderMediaNavigationResult(FolderMediaNavigationKind.BoundaryAfterLast, null);

        var kind = direction == FolderMediaNavigationDirection.Previous
            ? FolderMediaNavigationKind.Previous
            : FolderMediaNavigationKind.Next;
        return new FolderMediaNavigationResult(kind, orderedFiles[targetIndex]);
    }

    private static List<string> GetOrderedSupportedMediaFiles(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                return [];

            return Directory
                .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedMediaPath)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    private static bool TryNormalizeLocalPath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            {
                if (!absoluteUri.IsFile)
                    return false;

                normalizedPath = Path.GetFullPath(absoluteUri.LocalPath);
                return true;
            }

            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalizedPath = string.Empty;
            return false;
        }
    }
}
