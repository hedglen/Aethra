using System;
using System.Linq;
using Aethra.Commands;
using Aethra.Input;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class InputBindingCatalogTests
{
    [Fact]
    public void CreateDefaults_AllGesturesParse()
    {
        var defaults = InputBindingCatalog.CreateDefaults();

        foreach (var binding in defaults)
        {
            var parsed = InputRuntimeService.TryParseGesture(binding.Gesture, out _);
            Assert.True(parsed, $"Expected gesture '{binding.Gesture}' to parse.");
        }
    }

    [Fact]
    public void CreateDefaults_HasNoNormalizedGestureConflicts()
    {
        var defaults = InputBindingCatalog.CreateDefaults();
        var conflictingDuplicate = defaults
            .Where(binding => InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out _))
            .GroupBy(binding =>
            {
                _ = InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out var key);
                return key;
            }, StringComparer.Ordinal)
            .FirstOrDefault(group =>
                group.Select(binding => binding.Command.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1);

        Assert.Null(conflictingDuplicate);
    }

    [Fact]
    public void CreateDefaults_IncludesPrimaryClickAndDoubleClickBindings()
    {
        var defaults = InputBindingCatalog.CreateDefaults();
        var single = defaults.Single(binding => string.Equals(binding.Gesture, "MBTN_LEFT", StringComparison.Ordinal));
        var doubleClick = defaults.Single(binding => string.Equals(binding.Gesture, "MBTN_LEFT_DBL", StringComparison.Ordinal));
        var space = defaults.Single(binding => string.Equals(binding.Gesture, "SPACE", StringComparison.Ordinal));

        Assert.Equal(AethraCommandIds.TogglePlayPause, single.Command);
        Assert.Equal(AethraCommandIds.ToggleFullscreen, doubleClick.Command);
        Assert.Equal(AethraCommandIds.BossKey, space.Command);
    }

    [Fact]
    public void CreateDefaults_UsesNativeRepeatBinding()
    {
        var defaults = InputBindingCatalog.CreateDefaults();
        var decimalOn = defaults.Single(binding => string.Equals(binding.Gesture, "KP_DEC", StringComparison.Ordinal));
        var decimalOff = defaults.Single(binding => string.Equals(binding.Gesture, "KP_DEL", StringComparison.Ordinal));

        Assert.Equal(AethraCommandIds.CycleRepeat, decimalOn.Command);
        Assert.Equal(AethraCommandIds.CycleRepeat, decimalOff.Command);
        Assert.DoesNotContain("show-text", decimalOn.Command, StringComparison.Ordinal);
        Assert.DoesNotContain("show-text", decimalOff.Command, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDefaults_UsesNativeSubtitleToggleBinding()
    {
        var defaults = InputBindingCatalog.CreateDefaults();
        var subtitleToggle = defaults.Single(binding => string.Equals(binding.Gesture, "v", StringComparison.Ordinal));

        Assert.Equal(AethraCommandIds.ToggleSubtitles, subtitleToggle.Command);
    }
}
