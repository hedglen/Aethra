using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Aethra.Configuration;

internal static class AtomicFile
{
    internal static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(contents);

        using (var stream = new FileStream(
                   tempPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 4096,
                   FileOptions.WriteThrough))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    internal static void WriteAllLines(string path, IEnumerable<string> lines, Encoding? encoding = null)
    {
        var joined = string.Join(System.Environment.NewLine, lines);
        WriteAllText(path, joined + System.Environment.NewLine, encoding);
    }
}
