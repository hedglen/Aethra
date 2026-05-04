using Aethra.Services;
using Xunit;

namespace Aethra.Tests.Services;

public sealed class FolderMediaNavigatorTests
{
    [Fact]
    public void GetFirstInFolder_ReturnsFirstSupportedFileInSortedOrder()
    {
        using var folder = new TemporaryFolder();
        folder.CreateFile("z.txt");
        folder.CreateFile("b.mkv");
        var expected = folder.CreateFile("A.mp4");
        folder.CreateFile("c.flac");

        var result = FolderMediaNavigator.GetFirstInFolder(folder.Path);

        Assert.Equal(FolderMediaNavigationKind.First, result.Kind);
        Assert.Equal(expected, result.Path);
    }

    [Fact]
    public void GetAdjacentFile_SkipsUnsupportedFilesAndReturnsSortedNeighbors()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("A.mp4");
        folder.CreateFile("notes.txt");
        var second = folder.CreateFile("b.mkv");
        var third = folder.CreateFile("c.flac");

        var next = FolderMediaNavigator.GetAdjacentFile(first, FolderMediaNavigationDirection.Next);
        var previous = FolderMediaNavigator.GetAdjacentFile(third, FolderMediaNavigationDirection.Previous);

        Assert.Equal(FolderMediaNavigationKind.Next, next.Kind);
        Assert.Equal(second, next.Path);
        Assert.Equal(FolderMediaNavigationKind.Previous, previous.Kind);
        Assert.Equal(second, previous.Path);
    }

    [Fact]
    public void GetAdjacentFile_ReturnsBoundaryAtFolderEdges()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("A.mp4");
        var last = folder.CreateFile("b.mkv");

        var beforeFirst = FolderMediaNavigator.GetAdjacentFile(first, FolderMediaNavigationDirection.Previous);
        var afterLast = FolderMediaNavigator.GetAdjacentFile(last, FolderMediaNavigationDirection.Next);

        Assert.Equal(FolderMediaNavigationKind.BoundaryBeforeFirst, beforeFirst.Kind);
        Assert.False(beforeFirst.HasPath);
        Assert.Equal(FolderMediaNavigationKind.BoundaryAfterLast, afterLast.Kind);
        Assert.False(afterLast.HasPath);
    }

    [Fact]
    public void GetAdjacentFile_ReturnsNoneForMissingCurrentFile()
    {
        using var folder = new TemporaryFolder();
        var missingPath = System.IO.Path.Combine(folder.Path, "missing.mp4");

        var result = FolderMediaNavigator.GetAdjacentFile(missingPath, FolderMediaNavigationDirection.Next);

        Assert.Equal(FolderMediaNavigationKind.None, result.Kind);
        Assert.False(result.HasPath);
    }

    [Fact]
    public void GetAdjacentFile_ReturnsNoneForNonLocalUri()
    {
        var result = FolderMediaNavigator.GetAdjacentFile("https://example.com/video.mp4", FolderMediaNavigationDirection.Next);

        Assert.Equal(FolderMediaNavigationKind.None, result.Kind);
        Assert.False(result.HasPath);
    }

    [Fact]
    public void GetFirstInFolder_ReturnsNoneForMissingFolder()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));

        var result = FolderMediaNavigator.GetFirstInFolder(path);

        Assert.Equal(FolderMediaNavigationKind.None, result.Kind);
        Assert.False(result.HasPath);
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Aethra.Tests", System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFile(string name)
        {
            var filePath = System.IO.Path.Combine(Path, name);
            System.IO.File.WriteAllText(filePath, "test");
            return filePath;
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
                System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
