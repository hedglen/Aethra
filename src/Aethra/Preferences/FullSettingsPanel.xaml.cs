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
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace Aethra.Preferences;

public sealed partial class FullSettingsPanel : UserControl
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SectionFilterKeywords =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Playback"] = new[] { "playback", "startup", "speed", "autoplay", "resume", "loop" },
            ["Video"] = new[] { "video", "decode", "interpolation", "deinterlace", "quality", "renderer" },
            ["Audio"] = new[] { "audio", "volume", "device", "drc", "replaygain", "channels" },
            ["Subtitles"] = new[] { "subtitles", "captions", "font", "languages", "timing" },
            ["Input"] = new[] { "input", "shortcuts", "bindings", "keys", "mouse", "wheel" },
            ["Library"] = new[] { "library", "recent", "history", "folders" },
            ["Network"] = new[] { "network", "stream", "proxy", "timeout", "ipv6" },
            ["Shaders"] = new[] { "shaders", "upscaling", "post-processing", "fsrcnnx", "anime4k" },
            ["Profiles"] = new[] { "profiles", "bundles", "import", "export", "presets" },
            ["Customization"] = new[] { "customization", "theme", "accent", "layout", "hud" },
            ["Advanced"] = new[] { "advanced", "expert", "raw", "logging", "mpv options", "scripts" }
        };

    private readonly List<InputBindingSetting> _inputBindings = new();
    private readonly ObservableCollection<InputBindingSetting> _visibleInputBindings = new();
    private readonly ObservableCollection<AccentFavoriteColor> _favoriteAccentColors = new();
    private readonly PlaybackOptionsService _playbackOptions = PlaybackOptionsService.Instance;
    private PreferencesPageProfiles _pageProfiles = PreferencesPageProfiles.CreateDefault();
    private MpvImportedConfig? _importedConfig;
    private bool _isInitialized;
    private bool _syncingAccentText;
    private bool _inputBindingsDirty;
    private bool _isHydratingPageControls;
    private bool _isProgrammaticInputBindingUpdate;
    private bool _isRefreshingInputBindingUi;
    private readonly HashSet<string> _conflictingGestures = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? CloseRequested;
    public event EventHandler<IReadOnlyList<InputBindingSetting>>? InputBindingsChanged;

    public FullSettingsPanel()
    {
        InitializeComponent();
        InitializePreferencesSectionFilter();
        InitializeAccentControls();
        InitializeFavoriteAccentColors();
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
        SetActiveSection(tag);
    }

    private void PreferencesSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyPreferencesSectionFilter();
    }

    private void InitializePreferencesSectionFilter()
    {
        if (PreferencesFilterStatusText is not null)
            PreferencesFilterStatusText.Text = $"Showing {NavView.MenuItems.OfType<NavigationViewItem>().Count()} sections.";
        ApplyPreferencesSectionFilter();
    }

    private void SetActiveSection(string tag)
    {
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

    private void ApplyPreferencesSectionFilter()
    {
        if (NavView is null || PreferencesSearchBox is null || PreferencesFilterStatusText is null)
            return;

        var query = (PreferencesSearchBox.Text ?? string.Empty).Trim();
        var totalSections = 0;
        var visibleSections = new List<NavigationViewItem>();

        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            totalSections++;
            var isVisible = MatchesPreferencesFilter(item, query);
            item.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (isVisible)
                visibleSections.Add(item);
        }

        ApplyNavigationGroupHeaderVisibility(visibleSections);

        if (visibleSections.Count == 0)
        {
            SetActiveSection(string.Empty);
            PreferencesFilterStatusText.Text = string.IsNullOrWhiteSpace(query)
                ? "No sections available."
                : $"No sections match \"{query}\".";
            return;
        }

        if (NavView.SelectedItem is not NavigationViewItem selectedItem
            || selectedItem.Visibility != Visibility.Visible)
        {
            NavView.SelectedItem = visibleSections[0];
            SetActiveSection(visibleSections[0].Tag?.ToString() ?? string.Empty);
        }

        PreferencesFilterStatusText.Text = string.IsNullOrWhiteSpace(query)
            ? $"Showing {visibleSections.Count} sections."
            : $"Showing {visibleSections.Count} of {totalSections} sections.";
    }

    private void ApplyNavigationGroupHeaderVisibility(IReadOnlyCollection<NavigationViewItem> visibleSections)
    {
        CoreNavHeader.Visibility = HasVisibleSection(visibleSections, "Playback", "Input", "Library")
            ? Visibility.Visible
            : Visibility.Collapsed;
        MediaNavHeader.Visibility = HasVisibleSection(visibleSections, "Video", "Audio", "Subtitles", "Shaders")
            ? Visibility.Visible
            : Visibility.Collapsed;
        SystemNavHeader.Visibility = HasVisibleSection(visibleSections, "Network", "Profiles", "Customization", "Advanced")
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool HasVisibleSection(IEnumerable<NavigationViewItem> visibleSections, params string[] tags)
    {
        var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        return visibleSections.Any(section => tagSet.Contains(section.Tag?.ToString() ?? string.Empty));
    }

    private static bool MatchesPreferencesFilter(NavigationViewItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var normalizedQuery = query.Trim();
        var tag = item.Tag?.ToString() ?? string.Empty;
        var content = item.Content?.ToString() ?? string.Empty;

        if (Contains(tag, normalizedQuery) || Contains(content, normalizedQuery))
            return true;

        if (!SectionFilterKeywords.TryGetValue(tag, out var keywords))
            return false;

        return keywords.Any(keyword => keyword.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    private void InitializeAccentControls()
    {
        SyncAccentText(AccentColorService.CurrentHex);
        FullAccentStatusText.Text = $"Using {AccentColorService.CurrentHex}";
    }

    private void InitializeFavoriteAccentColors()
    {
        FavoriteAccentColorsGridView.ItemsSource = _favoriteAccentColors;
        RefreshFavoriteAccentColors();
    }

    private void FullAccentHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingAccentText)
            return;

        var text = FullAccentHexBox.Text;
        if (AccentColorService.TryParseHexColor(text, out var color, out var normalizedHex))
        {
            SyncAccentPickerColor(color);
            FullAccentStatusText.Text = $"Ready to apply {normalizedHex}";
            return;
        }

        FullAccentStatusText.Text = "Enter a hex color like #7B2FFF or #A0F.";
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_syncingAccentText)
            return;

        var normalizedHex = ToHex(args.NewColor);
        SyncAccentText(normalizedHex);
        FullAccentStatusText.Text = $"Ready to apply {normalizedHex}";
    }

    private void ColorSpectrumShapeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (ColorSpectrumShapeRadioButtons.SelectedItem)
        {
            case "Box":
                AccentColorPicker.ColorSpectrumShape = ColorSpectrumShape.Box;
                break;
            default:
                AccentColorPicker.ColorSpectrumShape = ColorSpectrumShape.Ring;
                break;
        }
    }

    private void FullApplyAccentButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccentFromText();
    }

    private void AddFavoriteAccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (AccentColorService.TryAddFavoriteHex(FullAccentHexBox.Text, out var normalizedHex))
        {
            SyncAccentText(normalizedHex);
            RefreshFavoriteAccentColors();
            FullAccentStatusText.Text = $"Saved {normalizedHex} to favorites.";
            return;
        }

        FullAccentStatusText.Text = "Pick a valid accent color before adding it to favorites.";
    }

    private void FavoriteAccentColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AccentFavoriteColor favorite })
            return;

        if (AccentColorService.TryApplyHex(favorite.Hex, out var normalizedHex))
        {
            SyncAccentText(normalizedHex);
            FullAccentStatusText.Text = $"Using favorite {normalizedHex}";
        }
    }

    private void RemoveFavoriteAccentColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AccentFavoriteColor favorite })
            return;

        if (AccentColorService.TryRemoveFavoriteHex(favorite.Hex, out var normalizedHex))
        {
            RefreshFavoriteAccentColors();
            FullAccentStatusText.Text = $"Removed {normalizedHex} from favorites.";
        }
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
        VideoQualityPresetCombo.SelectedIndex = _pageProfiles.Video.QualityPreset switch
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
        ShaderPresetCombo.SelectedIndex = _pageProfiles.Video.ShaderPreset switch
        {
            ShaderChainPreset.None => 0,
            ShaderChainPreset.Fsrcnnx => 1,
            ShaderChainPreset.Anime4k => 2,
            ShaderChainPreset.SsimFsrcnnx => 3,
            _ => 0
        };
        CustomShaderChainBox.Text = _pageProfiles.Video.CustomShaderChain;
    }

    private void SyncAccentText(string hex)
    {
        _syncingAccentText = true;
        try
        {
            FullAccentHexBox.Text = hex;
            if (AccentColorService.TryParseHexColor(hex, out var color, out _))
                AccentColorPicker.Color = color;
        }
        finally
        {
            _syncingAccentText = false;
        }
    }

    private void SyncAccentPickerColor(Color color)
    {
        _syncingAccentText = true;
        try
        {
            AccentColorPicker.Color = color;
        }
        finally
        {
            _syncingAccentText = false;
        }
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void RefreshFavoriteAccentColors()
    {
        _favoriteAccentColors.Clear();
        foreach (var hex in AccentColorService.LoadFavoriteHexColors())
            _favoriteAccentColors.Add(new AccentFavoriteColor(hex));

        FavoriteAccentEmptyText.Visibility = _favoriteAccentColors.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
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
            VideoQualityPresetCombo.SelectedIndex = _pageProfiles.Video.QualityPreset switch
            {
                VideoQualityPreset.Cinema => 1,
                VideoQualityPreset.Anime => 2,
                VideoQualityPreset.LowResBoost => 3,
                VideoQualityPreset.NativeClean => 4,
                _ => 0
            };
            ShaderPresetCombo.SelectedIndex = _pageProfiles.Video.ShaderPreset switch
            {
                ShaderChainPreset.Fsrcnnx => 1,
                ShaderChainPreset.Anime4k => 2,
                ShaderChainPreset.SsimFsrcnnx => 3,
                _ => 0
            };
            CustomShaderChainBox.Text = _pageProfiles.Video.CustomShaderChain;
            VideoStatusText.Text = "Video settings loaded.";

            AudioDeviceCombo.SelectedIndex = _pageProfiles.Audio.OutputDevice switch
            {
                "WASAPI Shared (auto)" => 1,
                "WASAPI Exclusive (example: wasapi/{GUID})" => 2,
                _ => 0
            };
            AudioDeviceTextBox.Text = _pageProfiles.Audio.OutputDevice;
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
            SubtitlesDelaySlider.Value = Math.Clamp(_pageProfiles.Subtitles.SubtitleDelaySeconds, -10, 10);
            SubtitlesStatusText.Text = "Subtitles settings loaded.";

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
        _pageProfiles.Video.QualityPreset = VideoQualityPresetCombo.SelectedIndex switch
        {
            1 => VideoQualityPreset.Cinema,
            2 => VideoQualityPreset.Anime,
            3 => VideoQualityPreset.LowResBoost,
            4 => VideoQualityPreset.NativeClean,
            _ => VideoQualityPreset.Reference
        };
        _pageProfiles.Video.ShaderPreset = ShaderPresetCombo.SelectedIndex switch
        {
            1 => ShaderChainPreset.Fsrcnnx,
            2 => ShaderChainPreset.Anime4k,
            3 => ShaderChainPreset.SsimFsrcnnx,
            _ => ShaderChainPreset.None
        };
        _pageProfiles.Video.CustomShaderChain = (CustomShaderChainBox.Text ?? string.Empty).Trim();

        var selectedAudioDevice = AudioDeviceCombo.SelectedItem is ComboBoxItem audioItem
            ? (audioItem.Content?.ToString() ?? "System default")
            : "System default";
        var audioDeviceOverride = (AudioDeviceTextBox.Text ?? string.Empty).Trim();
        _pageProfiles.Audio.OutputDevice = string.IsNullOrWhiteSpace(audioDeviceOverride) ? selectedAudioDevice : audioDeviceOverride;
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
        _pageProfiles.Subtitles.FontSize = Math.Clamp(SubtitlesFontSizeSlider.Value, 14, 28);
        _pageProfiles.Subtitles.BorderAndShadow = SubtitlesBorderShadowToggle.IsOn;
        _pageProfiles.Subtitles.SubtitleDelaySeconds = Math.Clamp(SubtitlesDelaySlider.Value, -10, 10);

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
        _playbackOptions.ApplyVideoEnhancementPreferences(_pageProfiles.Video);
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
            DeinterlaceEnabled = source.DeinterlaceEnabled,
            QualityPreset = source.QualityPreset,
            ShaderPreset = source.ShaderPreset,
            CustomShaderChain = source.CustomShaderChain
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
            BorderAndShadow = source.BorderAndShadow,
            SubtitleDelaySeconds = source.SubtitleDelaySeconds
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
        var loadResult = InputBindingSettingsStore.LoadWithMigration(
            InputBindingCatalog.CreateDefaults(),
            InputBindingCatalog.CreateLegacyDefaultsSnapshot());
        _inputBindings.Clear();
        _inputBindings.AddRange(loadResult.Bindings);
        _inputBindingsDirty = false;
        var warningSuffix = loadResult.Warnings.Count == 0
            ? string.Empty
            : $" {loadResult.Warnings.Count} warning(s) flagged for review.";
        InputBindingStatusText.Text = $"{loadResult.Summary}{warningSuffix}";
        RefreshInputBindingCategoriesAndList();
        SyncPrimaryControlsFromBindings();
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
        SyncPrimaryControlsFromBindings();
    }

    private void RefreshInputBindingCategoriesAndList()
    {
        if (InputCategoryFilter is null || InputBindingsList is null)
            return;

        var previousCategory = InputCategoryFilter.SelectedItem as string;
        _isRefreshingInputBindingUi = true;
        try
        {
            InputCategoryFilter.Items.Clear();
            InputCategoryFilter.Items.Add("All");

            foreach (var category in _inputBindings.Select(binding => binding.Category).Distinct().OrderBy(category => category))
                InputCategoryFilter.Items.Add(category);

            if (!string.IsNullOrWhiteSpace(previousCategory) && InputCategoryFilter.Items.Contains(previousCategory))
                InputCategoryFilter.SelectedItem = previousCategory;
            else
                InputCategoryFilter.SelectedItem = "All";

            if (!ReferenceEquals(InputBindingsList.ItemsSource, _visibleInputBindings))
                InputBindingsList.ItemsSource = _visibleInputBindings;
        }
        finally
        {
            _isRefreshingInputBindingUi = false;
        }

        ApplyInputBindingFilters();
    }

    private void SyncPrimaryControlsFromBindings()
    {
        if (PrimarySpaceCommandBox is null
            || PrimaryLeftClickCommandBox is null
            || PrimaryDoubleLeftClickCommandBox is null)
        {
            return;
        }

        PrimarySpaceCommandBox.Text = GetCommandForGesture("SPACE");
        PrimaryLeftClickCommandBox.Text = GetCommandForGesture("MBTN_LEFT");
        PrimaryDoubleLeftClickCommandBox.Text = GetCommandForGesture("MBTN_LEFT_DBL");
    }

    private string GetCommandForGesture(string gesture)
    {
        if (!InputRuntimeService.TryNormalizeGestureKey(gesture, out var targetKey))
            return string.Empty;

        for (var index = _inputBindings.Count - 1; index >= 0; index--)
        {
            var binding = _inputBindings[index];
            if (!InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out var key))
                continue;

            if (string.Equals(key, targetKey, StringComparison.Ordinal))
                return binding.Command;
        }

        return string.Empty;
    }

    private void InputSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _isRefreshingInputBindingUi)
            return;

        ApplyInputBindingFilters();
    }

    private void InputFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isRefreshingInputBindingUi)
            return;

        ApplyInputBindingFilters();
    }

    private void InputConflictsOnlyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _isRefreshingInputBindingUi)
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
        try
        {
            _inputBindings.Clear();
            _inputBindings.AddRange(CloneBindings(InputBindingCatalog.CreateDefaults()));
            _inputBindingsDirty = true;
            RefreshInputBindingCategoriesAndList();
            MarkBindingsDirty("Bindings reset to defaults. Save bindings to apply.");
            PrimaryControlsStatusText.Text = "Primary controls reset with list defaults.";
            SyncPrimaryControlsFromBindings();
        }
        catch (Exception ex)
        {
            InputBindingStatusText.Text = $"Reset failed: {ex.Message}";
        }
    }

    private void PrimaryControlsApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var updated = 0;
        updated += UpsertPrimaryBinding("General", "SPACE", PrimarySpaceCommandBox.Text, "Primary space action");
        updated += UpsertPrimaryBinding("Mouse", "MBTN_LEFT", PrimaryLeftClickCommandBox.Text, "Primary click action");
        updated += UpsertPrimaryBinding("Mouse", "MBTN_LEFT_DBL", PrimaryDoubleLeftClickCommandBox.Text, "Primary double-click action");

        if (updated == 0)
        {
            PrimaryControlsStatusText.Text = "No primary-control changes were applied.";
            return;
        }

        MarkBindingsDirty("Primary control changes pending save.");
        PrimaryControlsStatusText.Text = $"Updated {updated} primary binding(s). Save bindings to persist.";
        ApplyInputBindingFilters();
        SyncPrimaryControlsFromBindings();
    }

    private void PrimaryControlsResetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var defaultsByKey = BuildDefaultBindingsByNormalizedKey();

            var updated = 0;
            updated += ResetPrimaryBindingToDefault(defaultsByKey, "SPACE");
            updated += ResetPrimaryBindingToDefault(defaultsByKey, "MBTN_LEFT");
            updated += ResetPrimaryBindingToDefault(defaultsByKey, "MBTN_LEFT_DBL");

            if (updated == 0)
            {
                PrimaryControlsStatusText.Text = "Primary controls already match defaults.";
                return;
            }

            MarkBindingsDirty("Primary controls reset to defaults. Save to apply.");
            PrimaryControlsStatusText.Text = $"Reset {updated} primary binding(s) to defaults.";
            ApplyInputBindingFilters();
            SyncPrimaryControlsFromBindings();
        }
        catch (Exception ex)
        {
            PrimaryControlsStatusText.Text = $"Reset failed: {ex.Message}";
        }
    }

    private IReadOnlyDictionary<string, InputBindingSetting> BuildDefaultBindingsByNormalizedKey()
    {
        var byKey = new Dictionary<string, InputBindingSetting>(StringComparer.Ordinal);
        foreach (var binding in InputBindingCatalog.CreateDefaults())
        {
            if (!InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out var key))
                continue;

            if (!byKey.ContainsKey(key))
                byKey[key] = binding;
        }

        return byKey;
    }

    private int UpsertPrimaryBinding(string category, string gesture, string? command, string description)
    {
        var normalizedCommand = (command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return 0;

        if (!InputRuntimeService.TryNormalizeGestureKey(gesture, out var targetKey))
            return 0;

        var existing = _inputBindings
            .LastOrDefault(binding =>
                InputRuntimeService.TryNormalizeGestureKey(binding.Gesture, out var key)
                && string.Equals(key, targetKey, StringComparison.Ordinal));

        if (existing is null)
        {
            _inputBindings.Add(new InputBindingSetting(category, gesture, normalizedCommand, description, "Primary"));
            return 1;
        }

        if (string.Equals(existing.Command?.Trim(), normalizedCommand, StringComparison.Ordinal)
            && string.Equals(existing.Category, category, StringComparison.Ordinal))
        {
            return 0;
        }

        existing.Category = category;
        existing.Gesture = gesture;
        existing.Command = normalizedCommand;
        existing.Description = description;
        existing.Source = "Primary";
        return 1;
    }

    private int ResetPrimaryBindingToDefault(IReadOnlyDictionary<string, InputBindingSetting> defaultsByKey, string gesture)
    {
        if (!InputRuntimeService.TryNormalizeGestureKey(gesture, out var key))
            return 0;

        if (!defaultsByKey.TryGetValue(key, out var defaultBinding))
            return 0;

        return UpsertPrimaryBinding(defaultBinding.Category, defaultBinding.Gesture, defaultBinding.Command, defaultBinding.Description);
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

        if (InputConflictsOnlyToggle?.IsOn == true)
            bindings = bindings.Where(binding => IsConflictingGesture(binding.Gesture));

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

        UpdateInputBindingDiagnostics(syncPrimaryControls: false);
    }

    private void UpdateInputBindingDiagnostics(bool syncPrimaryControls)
    {
        if (InputBindingCountText is not null)
            InputBindingCountText.Text = $"{_visibleInputBindings.Count} shown / {_inputBindings.Count} total";

        UpdateInputConflictStatus();
        UpdateInputCommandValidationStatus();

        if (syncPrimaryControls)
            SyncPrimaryControlsFromBindings();
    }

    private void UpdateInputConflictStatus()
    {
        _conflictingGestures.Clear();
        var duplicateGroups = _inputBindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture))
            .GroupBy(binding => binding.Gesture.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();
        foreach (var group in duplicateGroups)
            _conflictingGestures.Add(group.Key);

        if (duplicateGroups.Count == 0)
        {
            InputConflictStatusText.Text = "No gesture conflicts detected.";
            return;
        }

        var sample = string.Join(", ", duplicateGroups.Take(3).Select(group => group.Key));
        InputConflictStatusText.Text = $"Conflicts: {duplicateGroups.Count} duplicated gesture(s). Runtime keeps the last row. {sample}";
    }

    private bool IsConflictingGesture(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return false;

        return _conflictingGestures.Contains(gesture.Trim());
    }

    private void UpdateInputCommandValidationStatus()
    {
        var unsupported = _inputBindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Command))
            .Select(binding =>
            {
                var isUnsupported = InputCommandSupport.TryGetUnsupportedReason(binding.Command, out var reason);
                return (isUnsupported, reason);
            })
            .Where(item => item.isUnsupported)
            .ToList();
        var blockedCount = unsupported.Count(item => item.reason.StartsWith("Blocked", StringComparison.OrdinalIgnoreCase));
        var incompleteCount = _inputBindings.Count(binding =>
            string.IsNullOrWhiteSpace(binding.Gesture) || string.IsNullOrWhiteSpace(binding.Command));

        if (unsupported.Count == 0 && incompleteCount == 0)
        {
            InputCommandValidationText.Text = "All commands look valid for current runtime support.";
            return;
        }

        InputCommandValidationText.Text = $"Review needed: {unsupported.Count} unsupported command(s) ({blockedCount} blocked by safety policy), {incompleteCount} incomplete row(s).";
    }

    private void PersistInputBindings()
    {
        var incompleteCount = _inputBindings.Count(binding =>
            string.IsNullOrWhiteSpace(binding.Gesture) || string.IsNullOrWhiteSpace(binding.Command));
        if (!_inputBindingsDirty)
        {
            InputBindingStatusText.Text = incompleteCount > 0
                ? $"Bindings already up to date. {incompleteCount} incomplete row(s) are not persisted."
                : "Bindings already up to date.";
            InputBindingsChanged?.Invoke(this, CloneBindings(_inputBindings));
            return;
        }

        InputBindingSettingsStore.Save(_inputBindings);
        _inputBindingsDirty = false;
        InputBindingStatusText.Text = incompleteCount > 0
            ? $"Bindings saved. {incompleteCount} incomplete row(s) were skipped."
            : "Bindings saved.";
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

        if (_isRefreshingInputBindingUi)
            return;

        if (_isProgrammaticInputBindingUpdate)
            return;

        if (sender is TextBox textBox && textBox.FocusState == FocusState.Unfocused)
            return;

        MarkBindingsDirty("Binding changes pending save.");
        UpdateInputBindingDiagnostics(syncPrimaryControls: true);
    }

    private void ClearBindingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InputBindingSetting binding)
            return;

        RunProgrammaticInputBindingUpdate(() =>
        {
            binding.Gesture = string.Empty;
            binding.Command = string.Empty;
            binding.Description = string.Empty;
            binding.Source = "Custom";
        });
        MarkBindingsDirty("Binding cleared. Save to apply.");
        ApplyInputBindingFilters();
    }

    private void DuplicateBindingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InputBindingSetting binding)
            return;

        var index = _inputBindings.IndexOf(binding);
        if (index < 0)
            return;

        _inputBindings.Insert(index + 1, new InputBindingSetting(
            binding.Category,
            binding.Gesture,
            binding.Command,
            binding.Description,
            "Custom"));
        MarkBindingsDirty("Binding duplicated. Save to apply.");
        ApplyInputBindingFilters();
    }

    private void MoveBindingUpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveBinding(sender, -1);
    }

    private void MoveBindingDownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveBinding(sender, 1);
    }

    private void MoveBinding(object sender, int direction)
    {
        if (sender is not Button button || button.DataContext is not InputBindingSetting binding)
            return;

        var index = _inputBindings.IndexOf(binding);
        if (index < 0)
            return;

        var target = index + direction;
        if (target < 0 || target >= _inputBindings.Count)
            return;

        _inputBindings.RemoveAt(index);
        _inputBindings.Insert(target, binding);
        MarkBindingsDirty("Binding order updated. Save to apply.");
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

        RunProgrammaticInputBindingUpdate(() =>
        {
            binding.Gesture = gestureText;
            textBox.Text = gestureText;
        });
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        UpdateInputBindingDiagnostics(syncPrimaryControls: true);
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

        RunProgrammaticInputBindingUpdate(() =>
        {
            binding.Gesture = gestureText;
            textBox.Text = gestureText;
        });
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        UpdateInputBindingDiagnostics(syncPrimaryControls: true);
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
        RunProgrammaticInputBindingUpdate(() =>
        {
            binding.Gesture = gestureText;
            textBox.Text = gestureText;
        });
        MarkBindingsDirty($"Captured {gestureText}. Save to apply.");
        UpdateInputBindingDiagnostics(syncPrimaryControls: true);
        e.Handled = true;
    }

    private void RunProgrammaticInputBindingUpdate(Action action)
    {
        _isProgrammaticInputBindingUpdate = true;
        try
        {
            action();
        }
        finally
        {
            _isProgrammaticInputBindingUpdate = false;
        }
    }

    private static string BuildKeyboardGestureText(VirtualKey key)
    {
        var parts = new List<string>(4);
        var ctrlPressed = IsModifierPressed(VirtualKey.Control);
        var shiftPressed = IsModifierPressed(VirtualKey.Shift);
        var altPressed = IsModifierPressed(VirtualKey.Menu);
        if (ctrlPressed)
            parts.Add("CTRL");
        if (shiftPressed)
            parts.Add("SHIFT");
        if (altPressed)
            parts.Add("ALT");

        var primary = key switch
        {
            VirtualKey.Escape => "ESC",
            VirtualKey.PageDown => "PGDWN",
            VirtualKey.PageUp => "PGUP",
            VirtualKey.Back => "BS",
            VirtualKey.NumberPad0 => "KP0",
            VirtualKey.NumberPad1 => "KP1",
            VirtualKey.NumberPad2 => "KP2",
            VirtualKey.NumberPad3 => "KP3",
            VirtualKey.NumberPad4 => "KP4",
            VirtualKey.NumberPad5 => "KP5",
            VirtualKey.NumberPad6 => "KP6",
            VirtualKey.NumberPad7 => "KP7",
            VirtualKey.NumberPad8 => "KP8",
            VirtualKey.NumberPad9 => "KP9",
            VirtualKey.Decimal => "KP_DEC",
            VirtualKey.Subtract => "KP_SUBTRACT",
            _ => BuildFallbackGestureToken(key)
        };
        parts.Add(primary);
        return string.Join('+', parts);
    }

    private static string BuildFallbackGestureToken(VirtualKey key)
    {
        if (key is >= VirtualKey.A and <= VirtualKey.Z)
            return key.ToString().ToLowerInvariant();

        if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
            return ((int)key - (int)VirtualKey.Number0).ToString();

        var keyCode = (int)key;
        return keyCode switch
        {
            188 => ",",
            190 => ".",
            219 => "[",
            221 => "]",
            _ => key.ToString().ToUpperInvariant()
        };
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
        SubtitlesStatusText.Text = "Subtitles settings saved.";
    }

    private void SubtitlesResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pageProfiles.Subtitles = SubtitlePreferencesProfile.CreateDefault();
        HydratePageControlsFromProfiles();
        SavePageProfiles();
        SubtitlesStatusText.Text = "Subtitles settings reset to defaults.";
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

        _pageProfiles.Video.QualityPreset = preset;
        _playbackOptions.ApplyVideoQualityPreset(preset);
        if (_isInitialized && !_isHydratingPageControls)
            VideoStatusText.Text = "Quality preset updated. Save video to persist.";
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
        _pageProfiles.Video.ShaderPreset = preset;
        if (preset != ShaderChainPreset.None)
            _pageProfiles.Video.CustomShaderChain = string.Empty;
        _playbackOptions.ApplyShaderPreset(preset);
        ShaderStatusText.Text = $"Applied shader preset: {preset}. Save video to persist.";
    }

    private void ApplyCustomShaderChainButton_Click(object sender, RoutedEventArgs e)
    {
        var chain = (CustomShaderChainBox.Text ?? string.Empty).Trim();
        _pageProfiles.Video.ShaderPreset = ShaderChainPreset.None;
        _pageProfiles.Video.CustomShaderChain = chain;
        _playbackOptions.ApplyCustomShaderChain(chain);
        ShaderStatusText.Text = "Applied custom shader chain. Save video to persist.";
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
            InputConfPathBox.Text = Path.Combine(path, "input.conf");

            var blockedCount = imported.InputBindings.Count(binding =>
                InputCommandSupport.TryGetUnsupportedReason(binding.Command, out _));
            var blockedSample = imported.InputBindings
                .Select(binding =>
                {
                    _ = InputCommandSupport.TryGetUnsupportedReason(binding.Command, out var reason);
                    return (binding, reason);
                })
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.reason));

            ImportedShaderCountText.Text = $"{imported.ShaderFiles.Count} shader files detected";
            ImportedScriptCountText.Text = $"{imported.ScriptFiles.Count} script files detected";
            var unsupportedInputRowsCount = imported.UnsupportedInputRows.Count;
            var includeDiagnosticsCount = imported.UnsupportedMpvRows.Count(row =>
                row.StartsWith("include-", StringComparison.OrdinalIgnoreCase));
            var profileDiagnosticsCount = imported.UnsupportedMpvRows.Count(row =>
                row.StartsWith("profile-", StringComparison.OrdinalIgnoreCase));
            var unsupportedOptionRowsCount = imported.UnsupportedMpvRows.Count - includeDiagnosticsCount - profileDiagnosticsCount;
            var includeCount = imported.IncludedMpvConfigFiles.Count;
            var mergedProfileCount = imported.ProfileMergedOptions.Count;
            var blockedSummary = blockedCount == 0
                ? "0 blocked command row(s)"
                : $"{blockedCount} blocked command row(s)";
            var blockedSampleText = blockedCount == 0
                ? string.Empty
                : $" {blockedSample.binding?.Gesture ?? "Command"}: {blockedSample.reason}.";
            PortableImportStatusText.Text =
                $"Imported {imported.InputBindings.Count} bindings, {imported.MpvOptions.Count} mpv options, {mergedProfileCount} merged profile(s), {includeCount} include file(s). " +
                $"Diagnostics: {blockedSummary}, {unsupportedInputRowsCount} unsupported input row(s), {unsupportedOptionRowsCount} unsupported mpv option row(s), {includeDiagnosticsCount} include diagnostic row(s), {profileDiagnosticsCount} profile diagnostic row(s).{blockedSampleText}";
        }
        catch (Exception ex)
        {
            PortableImportStatusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private void ImportInputConfButton_Click(object sender, RoutedEventArgs e)
    {
        var inputConfPath = (InputConfPathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(inputConfPath) || !File.Exists(inputConfPath))
        {
            PortableImportStatusText.Text = "Enter a valid input.conf file path.";
            return;
        }

        try
        {
            var imported = InputBindingSettingsStore.ImportFromInputConf(inputConfPath);
            if (imported.Count == 0)
            {
                PortableImportStatusText.Text = "No valid bindings found in input.conf.";
                return;
            }

            _inputBindings.Clear();
            _inputBindings.AddRange(imported);
            _inputBindingsDirty = true;
            RefreshInputBindingCategoriesAndList();
            PersistInputBindings();

            var blockedCount = imported.Count(binding =>
                InputCommandSupport.TryGetUnsupportedReason(binding.Command, out _));
            PortableImportStatusText.Text = blockedCount == 0
                ? $"Imported {imported.Count} binding(s) from input.conf."
                : $"Imported {imported.Count} binding(s) from input.conf. {blockedCount} command(s) need review.";
        }
        catch (Exception ex)
        {
            PortableImportStatusText.Text = $"input.conf import failed: {ex.Message}";
        }
    }

    private void InitializeExtensionControls()
    {
        PortableConfigPathBox.Text = ScriptExtensionSettingsStore.PortableConfigPath;
        InputConfPathBox.Text = string.IsNullOrWhiteSpace(ScriptExtensionSettingsStore.PortableConfigPath)
            ? string.Empty
            : Path.Combine(ScriptExtensionSettingsStore.PortableConfigPath, "input.conf");
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

public sealed class AccentFavoriteColor
{
    public AccentFavoriteColor(string hex)
    {
        Hex = hex;
        if (!AccentColorService.TryParseHexColor(hex, out var color, out _))
            color = Color.FromArgb(0, 0, 0, 0);

        Brush = new SolidColorBrush(color);
    }

    public string Hex { get; }

    public SolidColorBrush Brush { get; }
}
