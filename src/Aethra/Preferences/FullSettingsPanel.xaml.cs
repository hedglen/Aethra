using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Aethra.Configuration;
using Aethra.Input;
using Aethra.Profiles;
using Aethra.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Aethra.Preferences;

public sealed partial class FullSettingsPanel : UserControl
{
    private readonly List<InputBindingSetting> _inputBindings = new();
    private readonly ObservableCollection<InputBindingSetting> _visibleInputBindings = new();
    private readonly PlaybackOptionsService _playbackOptions = PlaybackOptionsService.Instance;
    private PreferencesPageProfiles _pageProfiles = PreferencesPageProfiles.CreateDefault();
    private MpvImportedConfig? _importedConfig;
    private bool _isInitialized;
    private bool _syncingAccentText;
    private bool _inputBindingsDirty;
    private bool _isHydratingPageControls;

    public event EventHandler? CloseRequested;
    public event EventHandler<IReadOnlyList<InputBindingSetting>>? InputBindingsChanged;

    public FullSettingsPanel()
    {
        InitializeComponent();
        InitializeAccentControls();
        AccentColorService.AccentColorChanged += AccentColorService_AccentColorChanged;
        Unloaded += FullSettingsPanel_Unloaded;
        _isInitialized = false;
        InitializePageProfiles();
        InitializeInputBindings();
        SyncVideoQualityPresetSelection();
        SyncShaderPresetSelection();
        InitializeExtensionControls();
        _isInitialized = true;
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
        NetworkPanel.Visibility = tag == "Network" ? Visibility.Visible : Visibility.Collapsed;
        ShadersPanel.Visibility = tag == "Shaders" ? Visibility.Visible : Visibility.Collapsed;
        ProfilesPanel.Visibility = tag == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        CustomizationPanel.Visibility = tag == "Customization" ? Visibility.Visible : Visibility.Collapsed;
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
        SavePageProfiles();
        PersistExtensionControls();
        PersistInputBindings();
    }

    private void SyncVideoQualityPresetSelection()
    {
        VideoQualityPresetCombo.SelectedIndex = _playbackOptions.CurrentVideoQualityPreset switch
        {
            VideoQualityPreset.Reference => 0,
            VideoQualityPreset.Cinema => 1,
            VideoQualityPreset.Anime => 2,
            VideoQualityPreset.LowResBoost => 3,
            VideoQualityPreset.NativeClean => 4,
            _ => 0
        };
    }

    private void SyncShaderPresetSelection()
    {
        ShaderPresetCombo.SelectedIndex = _playbackOptions.CurrentShaderPreset switch
        {
            ShaderChainPreset.None => 0,
            ShaderChainPreset.Fsrcnnx => 1,
            ShaderChainPreset.Anime4k => 2,
            ShaderChainPreset.SsimFsrcnnx => 3,
            _ => 0
        };
        CustomShaderChainBox.Text = _playbackOptions.CurrentCustomShaderChain;
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

    private void InitializePageProfiles()
    {
        _pageProfiles = PreferencesProfilesStore.Load();
        EnsureDefaultProfileBundle();
        PopulateProfilesCombo();
        HydratePageControlsFromProfiles();
        ApplyProfilesToPlaybackRuntime();
        if (string.IsNullOrWhiteSpace(ProfilesExchangePathBox.Text))
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ProfilesExchangePathBox.Text = Path.Combine(docs, "aethra-profiles.json");
        }
    }

    private void HydratePageControlsFromProfiles()
    {
        _isHydratingPageControls = true;
        try
        {
            PlaybackResumeToggle.IsOn = _pageProfiles.Playback.ResumeWhereLeftOff;
            PlaybackAutoplayToggle.IsOn = _pageProfiles.Playback.AutoplayOnOpen;
            PlaybackEndOfFileCombo.SelectedIndex = _pageProfiles.Playback.EndOfFileAction switch
            {
                PlaybackEndOfFileAction.PlayNextInFolder => 1,
                PlaybackEndOfFileAction.LoopCurrentFile => 2,
                _ => 0
            };
            PlaybackDefaultSpeedSlider.Value = _pageProfiles.Playback.DefaultPlaybackSpeedPercent;
            PlaybackStatusText.Text = "Playback settings loaded.";

            VideoOutputCombo.SelectedIndex = _pageProfiles.Video.OutputMode switch
            {
                VideoOutputMode.Gpu => 1,
                _ => 0
            };
            VideoHardwareDecodeCombo.SelectedIndex = _pageProfiles.Video.HardwareDecode switch
            {
                HardwareDecodeMode.Nvdec => 1,
                HardwareDecodeMode.Dxva2 => 2,
                HardwareDecodeMode.Copy => 3,
                _ => 0
            };
            VideoInterpolationToggle.IsOn = _pageProfiles.Video.InterpolationEnabled;
            VideoDeinterlaceToggle.IsOn = _pageProfiles.Video.DeinterlaceEnabled;
            VideoStatusText.Text = "Video settings loaded.";

            AudioDeviceCombo.SelectedIndex = 0;
            AudioDrcToggle.IsOn = _pageProfiles.Audio.DynamicRangeCompression;
            AudioReplayGainToggle.IsOn = _pageProfiles.Audio.ReplayGainNormalization;
            AudioChannelLayoutCombo.SelectedIndex = _pageProfiles.Audio.ChannelLayout switch
            {
                AudioChannelLayout.Stereo => 1,
                AudioChannelLayout.Surround51 => 2,
                AudioChannelLayout.Surround71 => 3,
                _ => 0
            };
            AudioStatusText.Text = "Audio settings loaded.";

            SubtitlesAutoLoadToggle.IsOn = _pageProfiles.Subtitles.AutoLoadMatchingSubtitles;
            SubtitlesLanguagesTextBox.Text = _pageProfiles.Subtitles.PreferredLanguagesCsv;
            SubtitlesFontSizeSlider.Value = _pageProfiles.Subtitles.FontSize;
            SubtitlesBorderShadowToggle.IsOn = _pageProfiles.Subtitles.BorderAndShadow;
            SubtitlesStatusText.Text = "Subtitle settings loaded.";

            LibraryWatchFoldersToggle.IsOn = _pageProfiles.Library.WatchFoldersEnabled;
            LibraryRememberRecentToggle.IsOn = _pageProfiles.Library.RememberRecentFiles;
            LibraryStatusText.Text = "Library settings loaded.";

            NetworkPreferIpv6Toggle.IsOn = _pageProfiles.Network.PreferIpv6;
            NetworkAllowMeteredToggle.IsOn = _pageProfiles.Network.AllowMeteredConnections;
            NetworkTimeoutSecondsBox.Text = Math.Clamp(_pageProfiles.Network.NetworkTimeoutSeconds, 5, 600).ToString();
            NetworkProxyModeCombo.SelectedIndex = _pageProfiles.Network.ProxyMode switch
            {
                NetworkProxyMode.Direct => 1,
                NetworkProxyMode.Http => 2,
                _ => 0
            };
            NetworkProxyUrlBox.Text = _pageProfiles.Network.ProxyUrl;
            NetworkStatusText.Text = "Network settings loaded.";

            ProfilesActiveProfileTextBox.Text = _pageProfiles.Profiles.ActiveProfileName;
            SelectActiveProfileInCombo();
            ProfilesStatusText.Text = "Profile settings loaded.";
            ProfilesImportExportStatusText.Text = "Use export/import to share bundle sets.";

            SyncAccentText(_pageProfiles.Customization.AccentHex);
            CustomizationUseSystemThemeToggle.IsOn = _pageProfiles.Customization.UseSystemTheme;
            CustomizationDenseLayoutToggle.IsOn = _pageProfiles.Customization.DenseLayout;
            CustomizationShowHudToggle.IsOn = _pageProfiles.Customization.ShowPlaybackHud;
            CustomizationStatusText.Text = "Customization settings loaded.";

            AdvancedLogLevelCombo.SelectedIndex = _pageProfiles.Advanced.LogLevel switch
            {
                AdvancedLogLevel.Off => 0,
                AdvancedLogLevel.Verbose => 2,
                AdvancedLogLevel.Debug => 3,
                _ => 1
            };
            AdvancedExtraOptionsTextBox.Text = _pageProfiles.Advanced.ExtraMpvOptionsText;
            AdvancedStatusText.Text = "Advanced settings loaded.";
        }
        finally
        {
            _isHydratingPageControls = false;
        }
    }

    private void ReadPageProfilesFromControls()
    {
        _pageProfiles.Playback.ResumeWhereLeftOff = PlaybackResumeToggle.IsOn;
        _pageProfiles.Playback.AutoplayOnOpen = PlaybackAutoplayToggle.IsOn;
        _pageProfiles.Playback.EndOfFileAction = PlaybackEndOfFileCombo.SelectedIndex switch
        {
            1 => PlaybackEndOfFileAction.PlayNextInFolder,
            2 => PlaybackEndOfFileAction.LoopCurrentFile,
            _ => PlaybackEndOfFileAction.Stop
        };
        _pageProfiles.Playback.DefaultPlaybackSpeedPercent = Math.Clamp(PlaybackDefaultSpeedSlider.Value, 50, 200);

        _pageProfiles.Video.OutputMode = VideoOutputCombo.SelectedIndex switch
        {
            1 => VideoOutputMode.Gpu,
            _ => VideoOutputMode.GpuNext
        };
        _pageProfiles.Video.HardwareDecode = VideoHardwareDecodeCombo.SelectedIndex switch
        {
            1 => HardwareDecodeMode.Nvdec,
            2 => HardwareDecodeMode.Dxva2,
            3 => HardwareDecodeMode.Copy,
            _ => HardwareDecodeMode.Auto
        };
        _pageProfiles.Video.InterpolationEnabled = VideoInterpolationToggle.IsOn;
        _pageProfiles.Video.DeinterlaceEnabled = VideoDeinterlaceToggle.IsOn;

        _pageProfiles.Audio.OutputDevice = "System default";
        _pageProfiles.Audio.DynamicRangeCompression = AudioDrcToggle.IsOn;
        _pageProfiles.Audio.ReplayGainNormalization = AudioReplayGainToggle.IsOn;
        _pageProfiles.Audio.ChannelLayout = AudioChannelLayoutCombo.SelectedIndex switch
        {
            1 => AudioChannelLayout.Stereo,
            2 => AudioChannelLayout.Surround51,
            3 => AudioChannelLayout.Surround71,
            _ => AudioChannelLayout.Auto
        };

        _pageProfiles.Subtitles.AutoLoadMatchingSubtitles = SubtitlesAutoLoadToggle.IsOn;
        _pageProfiles.Subtitles.PreferredLanguagesCsv = (SubtitlesLanguagesTextBox.Text ?? string.Empty).Trim();
        _pageProfiles.Subtitles.FontSize = Math.Clamp(SubtitlesFontSizeSlider.Value, 20, 80);
        _pageProfiles.Subtitles.BorderAndShadow = SubtitlesBorderShadowToggle.IsOn;

        _pageProfiles.Library.WatchFoldersEnabled = LibraryWatchFoldersToggle.IsOn;
        _pageProfiles.Library.RememberRecentFiles = LibraryRememberRecentToggle.IsOn;

        if (!int.TryParse(NetworkTimeoutSecondsBox.Text?.Trim(), out var timeoutSeconds))
            timeoutSeconds = 30;
        _pageProfiles.Network.PreferIpv6 = NetworkPreferIpv6Toggle.IsOn;
        _pageProfiles.Network.AllowMeteredConnections = NetworkAllowMeteredToggle.IsOn;
        _pageProfiles.Network.NetworkTimeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
        _pageProfiles.Network.ProxyMode = NetworkProxyModeCombo.SelectedIndex switch
        {
            1 => NetworkProxyMode.Direct,
            2 => NetworkProxyMode.Http,
            _ => NetworkProxyMode.System
        };
        _pageProfiles.Network.ProxyUrl = (NetworkProxyUrlBox.Text ?? string.Empty).Trim();

        _pageProfiles.Customization.AccentHex = (FullAccentHexBox.Text ?? AccentColorService.DefaultAccentHex).Trim();
        _pageProfiles.Customization.UseSystemTheme = CustomizationUseSystemThemeToggle.IsOn;
        _pageProfiles.Customization.DenseLayout = CustomizationDenseLayoutToggle.IsOn;
        _pageProfiles.Customization.ShowPlaybackHud = CustomizationShowHudToggle.IsOn;

        var activeProfileName = (ProfilesActiveProfileTextBox.Text ?? string.Empty).Trim();
        _pageProfiles.Profiles.ActiveProfileName = string.IsNullOrWhiteSpace(activeProfileName) ? "Default" : activeProfileName;

        _pageProfiles.Advanced.LogLevel = AdvancedLogLevelCombo.SelectedIndex switch
        {
            0 => AdvancedLogLevel.Off,
            2 => AdvancedLogLevel.Verbose,
            3 => AdvancedLogLevel.Debug,
            _ => AdvancedLogLevel.Warnings
        };
        _pageProfiles.Advanced.ExtraMpvOptionsText = (AdvancedExtraOptionsTextBox.Text ?? string.Empty).Trim();
    }

    private void SavePageProfiles()
    {
        ReadPageProfilesFromControls();
        PreferencesProfilesStore.Save(_pageProfiles);
        ApplyProfilesToPlaybackRuntime();
    }

    private void ApplyProfilesToPlaybackRuntime()
    {
        _playbackOptions.ApplyPlaybackPreferences(_pageProfiles.Playback);
        _playbackOptions.ApplyVideoPreferences(_pageProfiles.Video);
        _playbackOptions.ApplyAudioPreferences(_pageProfiles.Audio);
        _playbackOptions.ApplySubtitlePreferences(_pageProfiles.Subtitles);
        _playbackOptions.ApplyAdvancedPreferences(_pageProfiles.Advanced);
        _playbackOptions.ApplyNetworkPreferences(_pageProfiles.Network);
        _playbackOptions.ApplyCustomizationPreferences(_pageProfiles.Customization);
        AccentColorService.TryApplyHex(_pageProfiles.Customization.AccentHex, out var normalizedHex);
        SyncAccentText(normalizedHex);
    }

    private void EnsureDefaultProfileBundle()
    {
        if (_pageProfiles.Profiles.Bundles is null)
            _pageProfiles.Profiles.Bundles = new List<NamedPreferencesProfileBundle>();

        if (_pageProfiles.Profiles.Bundles.Count == 0)
            _pageProfiles.Profiles.Bundles.Add(NamedPreferencesProfileBundle.CreateDefault());
    }

    private void PopulateProfilesCombo()
    {
        ProfilesActiveProfileCombo.Items.Clear();
        foreach (var bundle in _pageProfiles.Profiles.Bundles.OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase))
            ProfilesActiveProfileCombo.Items.Add(bundle.Name);
    }

    private void SelectActiveProfileInCombo()
    {
        var activeName = _pageProfiles.Profiles.ActiveProfileName;
        if (string.IsNullOrWhiteSpace(activeName))
            activeName = "Default";

        ProfilesActiveProfileCombo.SelectedItem = activeName;
        if (ProfilesActiveProfileCombo.SelectedIndex < 0 && ProfilesActiveProfileCombo.Items.Count > 0)
            ProfilesActiveProfileCombo.SelectedIndex = 0;
    }

    private void ApplyBundle(NamedPreferencesProfileBundle bundle)
    {
        _pageProfiles.Playback = Clone(bundle.Playback);
        _pageProfiles.Video = Clone(bundle.Video);
        _pageProfiles.Audio = Clone(bundle.Audio);
        _pageProfiles.Subtitles = Clone(bundle.Subtitles);
        _pageProfiles.Library = Clone(bundle.Library);
        _pageProfiles.Advanced = Clone(bundle.Advanced);
        _pageProfiles.Network = Clone(bundle.Network);
        _pageProfiles.Customization = Clone(bundle.Customization);
        _pageProfiles.Profiles.ActiveProfileName = bundle.Name;
    }

    private static PlaybackPreferencesProfile Clone(PlaybackPreferencesProfile source)
    {
        return new PlaybackPreferencesProfile
        {
            ResumeWhereLeftOff = source.ResumeWhereLeftOff,
            AutoplayOnOpen = source.AutoplayOnOpen,
            EndOfFileAction = source.EndOfFileAction,
            DefaultPlaybackSpeedPercent = source.DefaultPlaybackSpeedPercent
        };
    }

    private static VideoPreferencesProfile Clone(VideoPreferencesProfile source)
    {
        return new VideoPreferencesProfile
        {
            OutputMode = source.OutputMode,
            HardwareDecode = source.HardwareDecode,
            InterpolationEnabled = source.InterpolationEnabled,
            DeinterlaceEnabled = source.DeinterlaceEnabled
        };
    }

    private static AudioPreferencesProfile Clone(AudioPreferencesProfile source)
    {
        return new AudioPreferencesProfile
        {
            OutputDevice = source.OutputDevice,
            DynamicRangeCompression = source.DynamicRangeCompression,
            ReplayGainNormalization = source.ReplayGainNormalization,
            ChannelLayout = source.ChannelLayout
        };
    }

    private static SubtitlePreferencesProfile Clone(SubtitlePreferencesProfile source)
    {
        return new SubtitlePreferencesProfile
        {
            AutoLoadMatchingSubtitles = source.AutoLoadMatchingSubtitles,
            PreferredLanguagesCsv = source.PreferredLanguagesCsv,
            FontSize = source.FontSize,
            BorderAndShadow = source.BorderAndShadow
        };
    }

    private static LibraryPreferencesProfile Clone(LibraryPreferencesProfile source)
    {
        return new LibraryPreferencesProfile
        {
            WatchFoldersEnabled = source.WatchFoldersEnabled,
            RememberRecentFiles = source.RememberRecentFiles
        };
    }

    private static AdvancedPreferencesProfile Clone(AdvancedPreferencesProfile source)
    {
        return new AdvancedPreferencesProfile
        {
            LogLevel = source.LogLevel,
            ExtraMpvOptionsText = source.ExtraMpvOptionsText
        };
    }

    private static NetworkPreferencesProfile Clone(NetworkPreferencesProfile source)
    {
        return new NetworkPreferencesProfile
        {
            PreferIpv6 = source.PreferIpv6,
            AllowMeteredConnections = source.AllowMeteredConnections,
            NetworkTimeoutSeconds = source.NetworkTimeoutSeconds,
            ProxyMode = source.ProxyMode,
            ProxyUrl = source.ProxyUrl
        };
    }

    private static CustomizationPreferencesProfile Clone(CustomizationPreferencesProfile source)
    {
        return new CustomizationPreferencesProfile
        {
            AccentHex = source.AccentHex,
            UseSystemTheme = source.UseSystemTheme,
            DenseLayout = source.DenseLayout,
            ShowPlaybackHud = source.ShowPlaybackHud
        };
    }

    private void InitializeInputBindings()
    {
        _inputBindings.Clear();
        _inputBindings.AddRange(InputBindingSettingsStore.Load(InputBindingCatalog.CreateDefaults()));
        _inputBindingsDirty = false;
        InputBindingStatusText.Text = "Bindings loaded.";
        RefreshInputBindingCategoriesAndList();
    }

    public void SetInputBindings(IEnumerable<InputBindingSetting> bindings)
    {
        _inputBindings.Clear();
        _inputBindings.AddRange(bindings.Select(binding => new InputBindingSetting(
            binding.Category,
            binding.Gesture,
            binding.Command,
            binding.Description,
            binding.Source)));
        _inputBindingsDirty = false;
        if (InputBindingStatusText is not null)
            InputBindingStatusText.Text = "Bindings synced from runtime.";
        RefreshInputBindingCategoriesAndList();
    }

    private void RefreshInputBindingCategoriesAndList()
    {
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
        MarkBindingsDirty("Added new custom binding.");

        if (!InputCategoryFilter.Items.Contains("Custom"))
            InputCategoryFilter.Items.Add("Custom");

        InputCategoryFilter.SelectedItem = "Custom";
        ApplyInputBindingFilters();
    }

    private void ResetInputBindingsButton_Click(object sender, RoutedEventArgs e)
    {
        InitializeInputBindings();
        PersistInputBindings();
    }

    private void SaveInputBindingsButton_Click(object sender, RoutedEventArgs e)
    {
        PersistInputBindings();
    }

    private void ExportInputConfButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PersistInputBindings();
            var exportedPath = InputBindingSettingsStore.ExportToInputConf(_inputBindings);
            InputBindingStatusText.Text = $"Exported input.conf to {exportedPath}";
        }
        catch (Exception ex)
        {
            InputBindingStatusText.Text = $"Export failed: {ex.Message}";
        }
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
        UpdateInputConflictStatus();
    }

    private void UpdateInputConflictStatus()
    {
        var duplicateGroups = _inputBindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture))
            .GroupBy(binding => binding.Gesture.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();
        if (duplicateGroups.Count == 0)
        {
            InputConflictStatusText.Text = "No gesture conflicts detected.";
            return;
        }

        InputConflictStatusText.Text = $"Conflicts: {duplicateGroups.Count} duplicated gesture(s).";
    }

    private void PersistInputBindings()
    {
        if (!_inputBindingsDirty)
        {
            InputBindingStatusText.Text = "Bindings already up to date.";
            InputBindingsChanged?.Invoke(this, CloneBindings(_inputBindings));
            return;
        }

        InputBindingSettingsStore.Save(_inputBindings);
        _inputBindingsDirty = false;
        InputBindingStatusText.Text = "Bindings saved.";
        InputBindingsChanged?.Invoke(this, CloneBindings(_inputBindings));
    }

    private static List<InputBindingSetting> CloneBindings(IEnumerable<InputBindingSetting> bindings)
    {
        return bindings
            .Select(binding => new InputBindingSetting(
                binding.Category,
                binding.Gesture,
                binding.Command,
                binding.Description,
                binding.Source))
            .ToList();
    }

    private void MarkBindingsDirty(string statusText)
    {
        _inputBindingsDirty = true;
        if (InputBindingStatusText is not null)
            InputBindingStatusText.Text = statusText;
        UpdateInputConflictStatus();
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

    private void InputBindingField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized)
            return;

        MarkBindingsDirty("Binding changes pending save.");
        ApplyInputBindingFilters();
    }

    private void ClearBindingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InputBindingSetting binding)
            return;

        binding.Gesture = string.Empty;
        binding.Command = string.Empty;
        binding.Description = string.Empty;
        binding.Source = "Custom";
        MarkBindingsDirty("Binding cleared. Save to apply.");
        ApplyInputBindingFilters();
    }

    private void InputGestureTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not InputBindingSetting binding)
            return;

        if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu)
            return;

        var gestureText = BuildKeyboardGestureText(e.Key);
        if (string.IsNullOrWhiteSpace(gestureText))
            return;

        binding.Gesture = gestureText;
        textBox.Text = gestureText;
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        ApplyInputBindingFilters();
        e.Handled = true;
    }

    private void InputGestureTextBox_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not InputBindingSetting binding)
            return;

        var point = e.GetCurrentPoint(textBox);
        var gestureText = point.Properties switch
        {
            { IsLeftButtonPressed: true } => "MBTN_LEFT",
            { IsRightButtonPressed: true } => "MBTN_RIGHT",
            { IsMiddleButtonPressed: true } => "MBTN_MID",
            { IsXButton1Pressed: true } => "MBTN_BACK",
            { IsXButton2Pressed: true } => "MBTN_FORWARD",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(gestureText))
            return;

        if (IsModifierPressed(VirtualKey.Control))
            gestureText = $"CTRL+{gestureText}";
        if (IsModifierPressed(VirtualKey.Shift))
            gestureText = $"SHIFT+{gestureText}";
        if (IsModifierPressed(VirtualKey.Menu))
            gestureText = $"ALT+{gestureText}";

        binding.Gesture = gestureText;
        textBox.Text = gestureText;
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        ApplyInputBindingFilters();
        e.Handled = true;
    }

    private void InputGestureTextBox_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not InputBindingSetting binding)
            return;

        var delta = e.GetCurrentPoint(textBox).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        var gestureText = delta > 0 ? "WHEEL_UP" : "WHEEL_DOWN";
        binding.Gesture = gestureText;
        textBox.Text = gestureText;
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        ApplyInputBindingFilters();
        e.Handled = true;
    }

    private static string BuildKeyboardGestureText(VirtualKey key)
    {
        var parts = new List<string>(4);
        if (IsModifierPressed(VirtualKey.Control))
            parts.Add("CTRL");
        if (IsModifierPressed(VirtualKey.Shift))
            parts.Add("SHIFT");
        if (IsModifierPressed(VirtualKey.Menu))
            parts.Add("ALT");

        var primary = key switch
        {
            VirtualKey.Escape => "ESC",
            VirtualKey.PageDown => "PGDWN",
            VirtualKey.PageUp => "PGUP",
            VirtualKey.Back => "BS",
            _ => key.ToString().ToUpperInvariant()
        };
        parts.Add(primary);
        return string.Join('+', parts);
    }

    private static bool IsModifierPressed(VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private void PlaybackSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        PlaybackStatusText.Text = "Playback settings saved.";
    }

    private void PlaybackResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Playback = PlaybackPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        PlaybackStatusText.Text = "Playback settings reset to defaults.";
    }

    private void VideoSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        VideoStatusText.Text = "Video settings saved.";
    }

    private void VideoResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Video = VideoPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        VideoStatusText.Text = "Video settings reset to defaults.";
    }

    private void AudioSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        AudioStatusText.Text = "Audio settings saved.";
    }

    private void AudioResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Audio = AudioPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        AudioStatusText.Text = "Audio settings reset to defaults.";
    }

    private void SubtitlesSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        SubtitlesStatusText.Text = "Subtitle settings saved.";
    }

    private void SubtitlesResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Subtitles = SubtitlePreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        SubtitlesStatusText.Text = "Subtitle settings reset to defaults.";
    }

    private void LibrarySaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        LibraryStatusText.Text = "Library settings saved.";
    }

    private void LibraryResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Library = LibraryPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        LibraryStatusText.Text = "Library settings reset to defaults.";
    }

    private void NetworkSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        NetworkStatusText.Text = "Network settings saved.";
    }

    private void NetworkResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Network = NetworkPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        NetworkStatusText.Text = "Network settings reset to defaults.";
    }

    private void ProfilesSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        ReadPageProfilesFromControls();
        var activeName = _pageProfiles.Profiles.ActiveProfileName;
        var existing = _pageProfiles.Profiles.Bundles.FirstOrDefault(bundle => string.Equals(bundle.Name, activeName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _pageProfiles.Profiles.Bundles.Add(new NamedPreferencesProfileBundle
            {
                Name = activeName,
                Playback = Clone(_pageProfiles.Playback),
                Video = Clone(_pageProfiles.Video),
                Audio = Clone(_pageProfiles.Audio),
                Subtitles = Clone(_pageProfiles.Subtitles),
                Library = Clone(_pageProfiles.Library),
                Advanced = Clone(_pageProfiles.Advanced),
                Network = Clone(_pageProfiles.Network),
                Customization = Clone(_pageProfiles.Customization)
            });
        }
        else
        {
            existing.Playback = Clone(_pageProfiles.Playback);
            existing.Video = Clone(_pageProfiles.Video);
            existing.Audio = Clone(_pageProfiles.Audio);
            existing.Subtitles = Clone(_pageProfiles.Subtitles);
            existing.Library = Clone(_pageProfiles.Library);
            existing.Advanced = Clone(_pageProfiles.Advanced);
            existing.Network = Clone(_pageProfiles.Network);
            existing.Customization = Clone(_pageProfiles.Customization);
        }

        PopulateProfilesCombo();
        SelectActiveProfileInCombo();
        SavePageProfiles();
        ProfilesStatusText.Text = $"Active profile '{_pageProfiles.Profiles.ActiveProfileName}' saved.";
    }

    private void ProfilesResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Profiles = ProfilesPreferencesProfile.CreateDefault();
        EnsureDefaultProfileBundle();
        PopulateProfilesCombo();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        ProfilesStatusText.Text = "Profile settings reset to defaults.";
    }

    private void ProfilesCreateButton_Click(object sender, RoutedEventArgs e)
    {
        var requestedName = (ProfilesActiveProfileTextBox.Text ?? string.Empty).Trim();
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "Profile" : requestedName;
        var name = baseName;
        var suffix = 2;
        while (_pageProfiles.Profiles.Bundles.Any(bundle => string.Equals(bundle.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {suffix}";
            suffix++;
        }

        _pageProfiles.Profiles.Bundles.Add(new NamedPreferencesProfileBundle
        {
            Name = name,
            Playback = Clone(_pageProfiles.Playback),
            Video = Clone(_pageProfiles.Video),
            Audio = Clone(_pageProfiles.Audio),
            Subtitles = Clone(_pageProfiles.Subtitles),
            Library = Clone(_pageProfiles.Library),
            Advanced = Clone(_pageProfiles.Advanced),
            Network = Clone(_pageProfiles.Network),
            Customization = Clone(_pageProfiles.Customization)
        });
        _pageProfiles.Profiles.ActiveProfileName = name;
        PopulateProfilesCombo();
        SelectActiveProfileInCombo();
        ProfilesActiveProfileTextBox.Text = name;
        SavePageProfiles();
        ProfilesStatusText.Text = $"Created profile '{name}'.";
    }

    private void ProfilesApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedName = (ProfilesActiveProfileCombo.SelectedItem as string ?? ProfilesActiveProfileTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            ProfilesStatusText.Text = "Select a profile first.";
            return;
        }

        var bundle = _pageProfiles.Profiles.Bundles.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (bundle is null)
        {
            ProfilesStatusText.Text = $"Profile '{selectedName}' was not found.";
            return;
        }

        ApplyBundle(bundle);
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        ProfilesStatusText.Text = $"Applied profile '{bundle.Name}'.";
    }

    private void ProfilesDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedName = (ProfilesActiveProfileCombo.SelectedItem as string ?? ProfilesActiveProfileTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            ProfilesStatusText.Text = "Select a profile to delete.";
            return;
        }

        var index = _pageProfiles.Profiles.Bundles.FindIndex(bundle => string.Equals(bundle.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            ProfilesStatusText.Text = $"Profile '{selectedName}' was not found.";
            return;
        }

        if (_pageProfiles.Profiles.Bundles.Count == 1)
        {
            ProfilesStatusText.Text = "At least one profile must remain.";
            return;
        }

        _pageProfiles.Profiles.Bundles.RemoveAt(index);
        _pageProfiles.Profiles.ActiveProfileName = _pageProfiles.Profiles.Bundles[0].Name;
        PopulateProfilesCombo();
        SelectActiveProfileInCombo();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        ProfilesStatusText.Text = $"Deleted profile '{selectedName}'.";
    }

    private void ProfilesExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadPageProfilesFromControls();
            var path = (ProfilesExchangePathBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                ProfilesImportExportStatusText.Text = "Enter a valid export path first.";
                return;
            }

            PreferencesProfileBundleExchange.ExportToPath(path, _pageProfiles.Profiles);
            ProfilesImportExportStatusText.Text = $"Exported {_pageProfiles.Profiles.Bundles.Count} bundles to {path}.";
        }
        catch (Exception ex)
        {
            ProfilesImportExportStatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private void ProfilesImportButton_Click(object sender, RoutedEventArgs e)
    {
        var path = (ProfilesExchangePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ProfilesImportExportStatusText.Text = "Enter a valid import path first.";
            return;
        }

        if (!PreferencesProfileBundleExchange.TryImportFromPath(path, out var importedProfiles, out var error))
        {
            ProfilesImportExportStatusText.Text = $"Import failed: {error}";
            return;
        }

        _pageProfiles.Profiles = importedProfiles;
        EnsureDefaultProfileBundle();
        var bundle = _pageProfiles.Profiles.Bundles.FirstOrDefault(item =>
            string.Equals(item.Name, _pageProfiles.Profiles.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
            ?? _pageProfiles.Profiles.Bundles[0];
        ApplyBundle(bundle);
        PopulateProfilesCombo();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        ProfilesImportExportStatusText.Text = $"Imported {_pageProfiles.Profiles.Bundles.Count} bundles from {path}.";
    }

    private void ProfilesActiveProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        if (ProfilesActiveProfileCombo.SelectedItem is string name)
            ProfilesActiveProfileTextBox.Text = name;
    }

    private void CustomizationSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        CustomizationStatusText.Text = "Customization settings saved.";
    }

    private void CustomizationResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Customization = CustomizationPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        CustomizationStatusText.Text = "Customization settings reset to defaults.";
    }

    private void AdvancedSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingPageControls)
            return;

        SavePageProfiles();
        AdvancedStatusText.Text = "Advanced settings saved.";
    }

    private void AdvancedResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Advanced = AdvancedPreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        AdvancedStatusText.Text = "Advanced settings reset to defaults.";
    }

    private void VideoQualityPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var preset = VideoQualityPresetCombo.SelectedIndex switch
        {
            1 => VideoQualityPreset.Cinema,
            2 => VideoQualityPreset.Anime,
            3 => VideoQualityPreset.LowResBoost,
            4 => VideoQualityPreset.NativeClean,
            _ => VideoQualityPreset.Reference
        };

        _playbackOptions.ApplyVideoQualityPreset(preset);
    }

    private void ShaderPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || ShaderStatusText is null)
            return;

        var preset = ShaderPresetCombo.SelectedIndex switch
        {
            1 => ShaderChainPreset.Fsrcnnx,
            2 => ShaderChainPreset.Anime4k,
            3 => ShaderChainPreset.SsimFsrcnnx,
            _ => ShaderChainPreset.None
        };
        _playbackOptions.ApplyShaderPreset(preset);
        ShaderStatusText.Text = $"Applied shader preset: {preset}.";
    }

    private void ApplyCustomShaderChainButton_Click(object sender, RoutedEventArgs e)
    {
        _playbackOptions.ApplyCustomShaderChain(CustomShaderChainBox.Text ?? string.Empty);
        ShaderStatusText.Text = "Applied custom shader chain.";
    }

    private void ImportPortableConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var path = (PortableConfigPathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            PortableImportStatusText.Text = "Enter a valid portable_config directory path.";
            return;
        }

        try
        {
            var imported = MpvPortableConfigImporter.Import(path);
            _importedConfig = imported;
            MpvRuntimeBootstrapSettings.Instance.ApplyImportedConfig(imported);
            ScriptExtensionSettingsStore.PortableConfigPath = path;

            _inputBindings.Clear();
            _inputBindings.AddRange(imported.InputBindings);
            _inputBindingsDirty = true;
            RefreshInputBindingCategoriesAndList();
            PersistInputBindings();

            ImportedShaderCountText.Text = $"{imported.ShaderFiles.Count} shader files detected";
            ImportedScriptCountText.Text = $"{imported.ScriptFiles.Count} script files detected";
            PortableImportStatusText.Text = $"Imported {imported.InputBindings.Count} bindings and {imported.MpvOptions.Count} mpv options.";
        }
        catch (Exception ex)
        {
            PortableImportStatusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private void InitializeExtensionControls()
    {
        PortableConfigPathBox.Text = ScriptExtensionSettingsStore.PortableConfigPath;
        ScriptsEnabledToggle.IsOn = ScriptExtensionSettingsStore.ScriptsEnabled;
        ScriptsFolderBox.Text = ScriptExtensionSettingsStore.ScriptsFolder;
        ImportedShaderCountText.Text = "0 shader files detected";
        ImportedScriptCountText.Text = "0 script files detected";
    }

    private void PersistExtensionControls()
    {
        ScriptExtensionSettingsStore.PortableConfigPath = PortableConfigPathBox.Text ?? string.Empty;
        ScriptExtensionSettingsStore.ScriptsEnabled = ScriptsEnabledToggle.IsOn;
        ScriptExtensionSettingsStore.ScriptsFolder = ScriptsFolderBox.Text ?? string.Empty;
    }

    private void ScriptsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        ScriptExtensionSettingsStore.ScriptsEnabled = ScriptsEnabledToggle.IsOn;
        ScriptStatusText.Text = ScriptsEnabledToggle.IsOn
            ? "Lua script loading will be enabled on next playback backend init."
            : "Lua script loading is disabled.";
    }

    private void ScriptsFolderApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ScriptExtensionSettingsStore.ScriptsFolder = ScriptsFolderBox.Text ?? string.Empty;
        ScriptStatusText.Text = "Saved scripts folder path.";
    }
}
