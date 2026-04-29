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
            Assert.True(imported.ProfileMergedOptions.ContainsKey("cinema"));
            Assert.Equal("ewa_lanczossharp", imported.ProfileMergedOptions["cinema"]["scale"]);
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

    [Fact]
    public void Import_UsesDeterministicIncludeAndProfileMergeRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aethra-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllLines(Path.Combine(root, "mpv.conf"),
            [
                "include=a.conf",
                "include missing.conf",
                "include",
                "[cinema]",
                "profile=base",
                "deband=no",
                "vo=gpu-next"
            ]);
            File.WriteAllLines(Path.Combine(root, "a.conf"),
            [
                "include b.conf",
                "vo=gpu",
                "[base]",
                "deband=yes",
                "scale=ewa_lanczos"
            ]);
            File.WriteAllLines(Path.Combine(root, "b.conf"),
            [
                "include a.conf",
                "hr-seek=yes"
            ]);

            var imported = MpvPortableConfigImporter.Import(root);

            Assert.Equal("gpu", imported.MpvOptions["vo"]);
            Assert.Equal("yes", imported.MpvOptions["hr-seek"]);
            Assert.Equal(2, imported.IncludedMpvConfigFiles.Count);
            Assert.EndsWith("a.conf", imported.IncludedMpvConfigFiles[0], StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("b.conf", imported.IncludedMpvConfigFiles[1], StringComparison.OrdinalIgnoreCase);

            Assert.Contains(imported.UnsupportedMpvRows, row => row.StartsWith("include-missing:", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(imported.UnsupportedMpvRows, row => row.StartsWith("include-cycle:", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(imported.UnsupportedMpvRows, row => string.Equals(row, "include-invalid:include", StringComparison.OrdinalIgnoreCase));

            Assert.True(imported.ProfileMergedOptions.ContainsKey("base"));
            Assert.True(imported.ProfileMergedOptions.ContainsKey("cinema"));
            Assert.Equal("yes", imported.ProfileMergedOptions["base"]["deband"]);
            Assert.Equal("ewa_lanczos", imported.ProfileMergedOptions["base"]["scale"]);
            Assert.Equal("no", imported.ProfileMergedOptions["cinema"]["deband"]);
            Assert.Equal("gpu-next", imported.ProfileMergedOptions["cinema"]["vo"]);
            Assert.Equal("ewa_lanczos", imported.ProfileMergedOptions["cinema"]["scale"]);

            Assert.Equal("no", imported.MpvOptions["profile-merged:cinema:deband"]);
            Assert.Equal("ewa_lanczos", imported.MpvOptions["profile-merged:cinema:scale"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
