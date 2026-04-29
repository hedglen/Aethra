using System;
using System.IO;
using System.Linq;
using Aethra.Configuration;
using Xunit;

namespace Aethra.Tests.Configuration;

public sealed class InputBindingSettingsStoreTests
{
    [Fact]
    public void ImportFromInputConf_ParsesBindingsAndSkipsComments()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                "# comment",
                "SPACE cycle pause",
                "WHEEL_UP seek 5 # inline comment",
                "  MBTN_LEFT    script-binding uosc/menu  ",
                "",
                "'#still-data' seek -5"
            ]);

            var imported = InputBindingSettingsStore.ImportFromInputConf(path);

            Assert.Equal(4, imported.Count);
            Assert.Equal("SPACE", imported[0].Gesture);
            Assert.Equal("cycle pause", imported[0].Command);
            Assert.Equal("WHEEL_UP", imported[1].Gesture);
            Assert.Equal("seek 5", imported[1].Command);
            Assert.All(imported, binding => Assert.Equal("input.conf", binding.Source));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportFromInputConf_ReturnsEmptyForMissingFile()
    {
        var imported = InputBindingSettingsStore.ImportFromInputConf(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.conf"));
        Assert.Empty(imported);
    }
}
