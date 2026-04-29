using System;
using System.IO;
using Aethra.Configuration;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class MpvPortableConfigImporterTests
{
    [Fact]
    public void Import_ParsesOptionShorthandProfilesAndInputRows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aethra-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllLines(Path.Combine(root, "mpv.conf"),
            [
                "deband",
                "vo=gpu-next # comment",
                "include other.conf",
                "[cinema]",
                "profile-desc=\"Cinema\"",
                "scale=ewa_lanczossharp"
            ]);
            File.WriteAllLines(Path.Combine(root, "other.conf"),
            [
                "profile=fast",
                "hr-seek=yes",
                "bad line value"
            ]);
            File.WriteAllLines(Path.Combine(root, "input.conf"),
            [
                "SPACE cycle pause",
                "MBTN_LEFT script-binding uosc/menu",
                "BROKENROW"
            ]);

            var imported = MpvPortableConfigImporter.Import(root);

            Assert.Equal("yes", imported.MpvOptions["deband"]);
            Assert.Equal("gpu-next", imported.MpvOptions["vo"]);
            Assert.Equal("\"Cinema\"", imported.MpvOptions["profile:cinema:profile-desc"]);
            Assert.Equal("ewa_lanczossharp", imported.MpvOptions["profile:cinema:scale"]);
            Assert.Equal("fast", imported.MpvOptions["profile"]);
            Assert.Equal("yes", imported.MpvOptions["hr-seek"]);
            Assert.Equal(2, imported.InputBindings.Count);
            Assert.Single(imported.UnsupportedInputRows);
            Assert.Single(imported.IncludedMpvConfigFiles);
            Assert.Single(imported.UnsupportedMpvRows);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
