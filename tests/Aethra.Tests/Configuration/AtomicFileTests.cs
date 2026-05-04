using System.IO;
using Aethra.Configuration;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class AtomicFileTests
{
    [Fact]
    public void WriteAllText_CreatesFileWithExpectedContents()
    {
        var path = TempFile();
        try
        {
            AtomicFile.WriteAllText(path, "hello\nworld");

            Assert.Equal("hello\nworld", File.ReadAllText(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void WriteAllText_OverwritesExistingFile()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "old contents");
            AtomicFile.WriteAllText(path, "new contents");

            Assert.Equal("new contents", File.ReadAllText(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void WriteAllText_LeavesNoTempFileBehind_OnSuccess()
    {
        var path = TempFile();
        try
        {
            AtomicFile.WriteAllText(path, "x");

            Assert.False(File.Exists(path + ".tmp"), "temp file should be renamed away");
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void WriteAllText_PreservesPriorContents_WhenTempWriteThrows()
    {
        var path = TempFile();
        var tempPath = path + ".tmp";
        FileStream? blocker = null;
        try
        {
            File.WriteAllText(path, "old contents");
            blocker = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllText(path, "new contents"));
            Assert.Equal("old contents", File.ReadAllText(path));
        }
        finally
        {
            blocker?.Dispose();
            Cleanup(path);
        }
    }

    [Fact]
    public void WriteAllText_CreatesParentDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aethra-atomic-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "nested", "file.txt");
        try
        {
            AtomicFile.WriteAllText(path, "x");

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WriteAllLines_WritesPlatformSeparatedLines()
    {
        var path = TempFile();
        try
        {
            AtomicFile.WriteAllLines(path, new[] { "one", "two" });

            Assert.Equal($"one{Environment.NewLine}two{Environment.NewLine}", File.ReadAllText(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string TempFile()
    {
        return Path.Combine(Path.GetTempPath(), $"aethra-atomic-{Guid.NewGuid():N}.txt");
    }

    private static void Cleanup(string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        if (File.Exists(path + ".tmp"))
            File.Delete(path + ".tmp");
    }
}
