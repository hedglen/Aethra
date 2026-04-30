using System;
using System.IO;
using System.Linq;
using Aethra.Commands;
using Aethra.Configuration;
using Aethra.Input;
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

    [Fact]
    public void ApplyMigrationForRows_UserBindingsWinAndLegacyRowsAreReplaced()
    {
        var defaults = InputBindingCatalog.CreateDefaults();
        var existing = new[]
        {
            new InputBindingSetting("Mouse", "MBTN_RIGHT", "aethra:adjustments", "custom", "Custom"),
            new InputBindingSetting("Mouse", "MBTN_MID", "quit", "legacy", "Imported")
        };

        var result = InputBindingSettingsStore.ApplyMigrationForRows(
            defaults,
            existing,
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());

        Assert.True(result.WasMigrated);
        Assert.Equal("aethra:adjustments", GetCommand(result.Bindings, "MBTN_RIGHT"));
        Assert.Equal("aethra:quit", GetCommand(result.Bindings, "MBTN_MID"));
    }

    [Fact]
    public void ApplyMigrationForRows_IsIdempotentAfterMigration()
    {
        var firstPass = InputBindingSettingsStore.ApplyMigrationForRows(
            InputBindingCatalog.CreateDefaults(),
            new[]
            {
                new InputBindingSetting("Mouse", "MBTN_RIGHT", "aethra:adjustments", "custom", "Custom"),
                new InputBindingSetting("General", "SPACE", "quit", "legacy", "Imported")
            },
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());

        var secondPass = InputBindingSettingsStore.ApplyMigrationForRows(
            InputBindingCatalog.CreateDefaults(),
            firstPass.Bindings,
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());

        Assert.False(secondPass.WasMigrated);
    }

    [Fact]
    public void ApplyMigrationForRows_AlwaysForcesSpaceToBossKey()
    {
        var result = InputBindingSettingsStore.ApplyMigrationForRows(
            InputBindingCatalog.CreateDefaults(),
            new[]
            {
                new InputBindingSetting("General", "SPACE", "aethra:quit", "custom", "Custom"),
                new InputBindingSetting("General", "q", "aethra:quit-watch-later", "custom", "Custom")
            },
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());

        Assert.True(result.WasMigrated);
        Assert.Equal(AethraCommandIds.BossKey, GetCommand(result.Bindings, "SPACE"));
        Assert.Contains("Forced SPACE", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyMigrationForRows_RetainsUnparseableRowsWithWarning()
    {
        var result = InputBindingSettingsStore.ApplyMigrationForRows(
            InputBindingCatalog.CreateDefaults(),
            new[]
            {
                new InputBindingSetting("Custom", "???", "show-text \"oops\"", "bad gesture", "Custom")
            },
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());

        Assert.True(result.WasMigrated);
        Assert.Contains(result.Bindings, binding => binding.Gesture == "???");
        Assert.Contains(result.Warnings, warning => warning.Contains("Unparsed gesture retained", StringComparison.Ordinal));
    }

    private static string GetCommand(System.Collections.Generic.IReadOnlyList<InputBindingSetting> bindings, string gesture)
    {
        _ = InputRuntimeService.TryNormalizeGestureKey(gesture, out var targetKey);
        return bindings
            .Last(binding =>
                InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out var key)
                && string.Equals(key, targetKey, StringComparison.Ordinal))
            .Command;
    }
}
