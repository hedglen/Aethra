using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Aethra.Input;
using Aethra.Profiles;
using Aethra.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aethra.Preferences;

public sealed partial class FullSettingsPanel : UserControl
{
    private readonly List<InputBindingSetting> _inputBindings = new();
    private readonly ObservableCollection<InputBindingSetting> _visibleInputBindings = new();
    private readonly PlaybackOptionsService _playbackOptions = PlaybackOptionsService.Instance;
    private bool _isInitialized;
    private bool _syncingAccentText;

    public event EventHandler? CloseRequested;

    public FullSettingsPanel()
    {
        InitializeComponent();
        InitializeAccentControls();
        AccentColorService.AccentColorChanged += AccentColorService_AccentColorChanged;
        Unloaded += FullSettingsPanel_Unloaded;
        _isInitialized = true;
        InitializeInputBindings();
        SyncVideoQualityPresetSelection();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem selectedItem)
            return;

        var tag = selectedItem.Tag?.ToString() ?? string.Empty;

        PlaybackPanel.Visibility = tag == "Playback" ? Visibility.Visible : Visibility.Collapsed;
        VideoPanel.Visibility = tag == "Video" ? Visibility.Visible : Visibility.Collapsed;
        AudioPanel.Visibility = tag == "Audio" ? Visibility.Visible : Visibility.Collapsed;
        SubtitlesPanel.Visibility = tag == "Subtitles" ? Visibility.Visible : Visibility.Collapsed;
        InputPanel.Visibility = tag == "Input" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPanel.Visibility = tag == "Library" ? Visibility.Visible : Visibility.Collapsed;
        ShadersPanel.Visibility = tag == "Shaders" ? Visibility.Visible : Visibility.Collapsed;
        ProfilesPanel.Visibility = tag == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = tag == "Advanced" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InitializeAccentControls()
    {
        SyncAccentText(AccentColorService.CurrentHex);
        FullAccentStatusText.Text = $"Using {AccentColorService.CurrentHex}";
    }

    private void FullAccentHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingAccentText)
            return;

        var text = FullAccentHexBox.Text;
        FullAccentStatusText.Text = AccentColorService.TryParseHexColor(text, out _, out var normalizedHex)
            ? $"Ready to apply {normalizedHex}"
            : "Enter a hex color like #7B2FFF or #A0F.";
    }

    private void FullApplyAccentButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccentFromText();
    }

    private void FullResetAccentButton_Click(object sender, RoutedEventArgs e)
    {
        AccentColorService.TryApplyHex(AccentColorService.DefaultAccentHex, out var normalizedHex);
        SyncAccentText(normalizedHex);
        FullAccentStatusText.Text = $"Using {normalizedHex}";
    }

    private void ApplyAccentFromText()
    {
        if (AccentColorService.TryApplyHex(FullAccentHexBox.Text, out var normalizedHex))
        {
            SyncAccentText(normalizedHex);
            FullAccentStatusText.Text = $"Using {normalizedHex}";
            return;
        }

        FullAccentStatusText.Text = "Enter a hex color like #7B2FFF or #A0F.";
    }

    private void AccentColorService_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        SyncAccentText(e.Hex);
        FullAccentStatusText.Text = $"Using {e.Hex}";
    }

    private void FullSettingsPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        AccentColorService.AccentColorChanged -= AccentColorService_AccentColorChanged;
    }

    private void SyncVideoQualityPresetSelection()
    {
        VideoQualityPresetCombo.SelectedIndex = _playbackOptions.CurrentVideoQualityPreset switch
        {
            VideoQualityPreset.Reference => 0,
            VideoQualityPreset.Cinema => 1,
            VideoQualityPreset.Anime => 2,
            _ => 0
        };
    }

    private void SyncAccentText(string hex)
    {
        _syncingAccentText = true;
        try
        {
            FullAccentHexBox.Text = hex;
        }
        finally
        {
            _syncingAccentText = false;
        }
    }

    private void InitializeInputBindings()
    {
        _inputBindings.Clear();
        _inputBindings.AddRange(InputBindingCatalog.CreateDefaults());

        InputCategoryFilter.Items.Clear();
        InputCategoryFilter.Items.Add("All");

        foreach (var category in _inputBindings.Select(binding => binding.Category).Distinct().OrderBy(category => category))
            InputCategoryFilter.Items.Add(category);

        InputCategoryFilter.SelectedIndex = 0;
        InputBindingsList.ItemsSource = _visibleInputBindings;
        ApplyInputBindingFilters();
    }

    private void InputSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized)
            return;

        ApplyInputBindingFilters();
    }

    private void InputFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized)
            return;

        ApplyInputBindingFilters();
    }

    private void AddInputBindingButton_Click(object sender, RoutedEventArgs e)
    {
        var binding = new InputBindingSetting("Custom", string.Empty, string.Empty, string.Empty, "Custom");
        _inputBindings.Add(binding);

        if (!InputCategoryFilter.Items.Contains("Custom"))
            InputCategoryFilter.Items.Add("Custom");

        InputCategoryFilter.SelectedItem = "Custom";
        ApplyInputBindingFilters();
    }

    private void ResetInputBindingsButton_Click(object sender, RoutedEventArgs e)
    {
        InitializeInputBindings();
    }

    private void ApplyInputBindingFilters()
    {
        if (InputSearchBox is null
            || InputCategoryFilter is null
            || InputSortCombo is null
            || InputBindingCountText is null
            || InputBindingsList is null)
            return;

        var query = (InputSearchBox.Text ?? string.Empty).Trim();
        var selectedCategory = InputCategoryFilter.SelectedItem as string;

        IEnumerable<InputBindingSetting> bindings = _inputBindings;

        if (!string.IsNullOrWhiteSpace(selectedCategory) && selectedCategory != "All")
            bindings = bindings.Where(binding => string.Equals(binding.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            bindings = bindings.Where(binding =>
                Contains(binding.Category, query)
                || Contains(binding.Gesture, query)
                || Contains(binding.Command, query)
                || Contains(binding.Description, query)
                || Contains(binding.Source, query));
        }

        bindings = GetSelectedInputSort() switch
        {
            "Gesture" => bindings.OrderBy(binding => binding.Gesture).ThenBy(binding => binding.Category),
            "Command" => bindings.OrderBy(binding => binding.Command).ThenBy(binding => binding.Gesture),
            "Source" => bindings.OrderBy(binding => binding.Source).ThenBy(binding => binding.Category).ThenBy(binding => binding.Gesture),
            _ => bindings.OrderBy(binding => binding.Category).ThenBy(binding => binding.Gesture)
        };

        _visibleInputBindings.Clear();
        foreach (var binding in bindings)
            _visibleInputBindings.Add(binding);

        InputBindingCountText.Text = $"{_visibleInputBindings.Count} shown / {_inputBindings.Count} total";
    }

    private string GetSelectedInputSort()
    {
        if (InputSortCombo.SelectedItem is ComboBoxItem item && item.Content is string value)
            return value;

        return "Category";
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrEmpty(value)
            && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void VideoQualityPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var preset = VideoQualityPresetCombo.SelectedIndex switch
        {
            1 => VideoQualityPreset.Cinema,
            2 => VideoQualityPreset.Anime,
            _ => VideoQualityPreset.Reference
        };

        _playbackOptions.ApplyVideoQualityPreset(preset);
    }
}
