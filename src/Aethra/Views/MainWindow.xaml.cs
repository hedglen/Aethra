using Aethra.Commands;
using Aethra.Configuration;
using Aethra.Input;
using Aethra.Services;
using Aethra.Models;
using Aethra.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
// Aliased rather than `using Microsoft.UI.Xaml.Shapes;` because that namespace
// also contains a `Path` type which collides with `System.IO.Path` used here.
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;
using Windows.Storage.Pickers;
using Windows.System;

namespace Aethra
{
    public sealed partial class MainWindow : Window
    {
        private NativeMpvSoftwarePlayer? _softwarePlayer;
        private NativeMpvOpenGlPlayer? _gpuPlayer;
        private readonly List<INativeMpvPlayerBackend> _activeBackends = new();
        private D3D11SwapChainPanelHost? _gpuSurfaceSmokeHost;
        private readonly AethraCommandDispatcher _commandDispatcher;
        private readonly SUBCLASSPROC _windowSubclassProc;
        private readonly SUBCLASSPROC _childCursorSubclassProc;
        private readonly HashSet<IntPtr> _hookedCursorChildHwnds = new();
        private readonly Dictionary<IntPtr, IntPtr> _originalClassCursors = new();
        private readonly VideoAdjustmentBatcher _videoAdjustmentBatcher;
        private readonly DispatcherTimer _cursorHideEnforcementTimer;
        private readonly DispatcherTimer _volumeOsdHideTimer;
        private readonly DispatcherTimer _autoplayReassertTimer;
        private static readonly TimeSpan VolumeOsdLingerDuration = TimeSpan.FromMilliseconds(1500);
        private bool _useGpuVideoSurface = true;
        private static bool RunGpuSurfaceSmoke =>
            string.Equals(Environment.GetEnvironmentVariable("AETHRA_GPU_SURFACE_SMOKE"), "1", StringComparison.Ordinal);
        private IntPtr _mainHwnd;
        private bool _suppressSliderValueChanged;
        private IReadOnlyList<MpvChapter> _chapters = Array.Empty<MpvChapter>();
        // Last-known seek bar duration used to size chapter markers (the slider runs 0-100,
        // so we need the actual seconds to format chapter timestamps in the tooltip).
        private double _lastKnownDurationSeconds;
        // Index of the chapter whose tooltip is currently shown. -1 when hidden.
        private int _hoveredChapterIndex = -1;
        private const double ChapterMarkerHoverThresholdPx = 6.0;
        private bool _visiblePlayerInitialized;
        private bool _isVisiblePlayerInitializationQueued;
        private bool _isFullscreen;
        private bool _wasMaximizedBeforeFullscreen;
        private bool _isCommandRailExpanded;
        private readonly PlaybackActivityController _playbackActivity;
        private readonly PlaybackOptionsService _playbackOptions;
        private readonly InputRuntimeService _inputRuntimeService = new();
        private readonly List<InputBindingSetting> _currentInputBindings = new();
        private readonly PlaybackPersistenceSnapshot _playbackPersistence;
        private bool _persistedPreferencesAppliedToRuntime;
        private string? _pendingMediaPath;
        // True means playback is paused. Visual surfaces route through
        // PlayPauseVisualFor so the transport button and context menu stay aligned.
        private bool _isPlaybackPaused = true;
        private bool _isPointerOverTransportBar;
        private bool _isVideoContextFlyoutOpen;
        private bool _suppressVolumeSliderValueChanged;
        private double _currentVolume = 100;
        private bool _isMuted;
        // A/B loop point state. null means the point is not set; the value is
        // the timestamp in seconds we wrote to mpv's ab-loop-{a,b} property.
        private double? _loopPointA;
        private double? _loopPointB;
        private double _currentPlaybackPosition;
        // Gradient assigned to NativeProgressBar.Foreground when A is set, so the
        // slider's own value-fill draws gray from 0..A and accent from A..thumb.
        // The gradient is mapped onto the value-fill rectangle (which spans 0..thumb),
        // so the [A] cutoff fraction is A / current — re-applied each progress tick.
        private LinearGradientBrush? _loopAccentGradient;
        private bool _loopAccentSubscribed;
        private bool _isNativeCursorHidden;
        private bool _isInitializing = true;
        private Windows.Foundation.Point _lastRootPointerPosition;
        private bool _hasLastRootPointerPosition;
        private Windows.Foundation.Point _videoPointerPressedAt;
        private NativePoint _videoWindowDragStartCursorPosition;
        private Windows.Graphics.PointInt32 _videoWindowDragStartWindowPosition;
        private uint _videoPointerId;
        private bool _isVideoPointerPressPending;
        private bool _isVideoPointerDraggingWindow;
        private string? _lastLoadedMediaPath;
        private bool _startupMediaLoaded;
        private const string PreferredStartupMediaPath = @"C:\Users\rjh\Videos\test.mp4";
        private const double CommandRailCollapsedWidth = 64;
        private const double CommandRailExpandedWidth = 252;
        private const double TransportBarHeight = 70;
        private const double TopChromeHeight = 32;
        private const double WindowDragThreshold = 6;

        public MainWindow()
        {
            _playbackPersistence = PlaybackPersistenceStore.Load();
            _currentVolume = _playbackPersistence.LastVolume;
            _lastLoadedMediaPath = _playbackPersistence.LastMediaPath;
            _playbackActivity = new PlaybackActivityController(TimeSpan.FromSeconds(1), CanLetPlaybackChromeIdle);
            _playbackActivity.ModeChanged += PlaybackActivity_ModeChanged;
            _playbackOptions = PlaybackOptionsService.Instance;
            _playbackOptions.PropertyApplyRequested += PlaybackOptions_PropertyApplyRequested;

            _videoAdjustmentBatcher = new VideoAdjustmentBatcher(
                TimeSpan.FromMilliseconds(33),
                (property, value) => _playbackOptions.ApplyNumericProperty(property, value));
            _cursorHideEnforcementTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _cursorHideEnforcementTimer.Tick += CursorHideEnforcementTimer_Tick;
            _volumeOsdHideTimer = new DispatcherTimer
            {
                Interval = VolumeOsdLingerDuration
            };
            _volumeOsdHideTimer.Tick += VolumeOsdHideTimer_Tick;
            _autoplayReassertTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _autoplayReassertTimer.Tick += AutoplayReassertTimer_Tick;

            InitializeComponent();
            InitializeInputRuntime();
            FullSettings.SetInputBindings(_currentInputBindings);
            FullSettings.InputBindingsChanged += FullSettings_InputBindingsChanged;
            ApplyPlayPauseVisualState();
            InitializeVolumeUi();
            CommandRail.Loaded += CommandRail_Loaded;
            SetCommandRailExpanded(false);
            _commandDispatcher = new AethraCommandDispatcher(new AethraCommandContext(
                PausePlayback,
                MinimizeWindow,
                ToggleSettingsPanel,
                ToggleFullscreen,
                TogglePlayback,
                () => SeekRelative(-10),
                () => SeekRelative(30),
                () => AddVolume(5),
                () => AddVolume(-5),
                ToggleMute,
                HandleEscapeCommand,
                ToggleLoopPointA,
                ToggleLoopPointB,
                ResetLoopPoints,
                OpenFileFromCommand,
                OpenFolderFromCommand,
                OpenRecentFromCommand,
                ShowPlaylistFromCommand,
                ShowToolsFromCommand,
                ShowHelpFromCommand,
                ShowFavoritesFromCommand,
                ToggleAdjustmentsFromCommand,
                ToggleCommandRailFromCommand));
            _windowSubclassProc = WindowSubclassProc;
            _childCursorSubclassProc = ChildCursorSubclassProc;
            this.Activated += MainWindow_Activated;
            this.Activated += MainWindow_CursorActivationChanged;
            this.Closed += MainWindow_Closed;
            _isInitializing = false;
            ApplyPlaybackActivityState();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _videoAdjustmentBatcher.Dispose();
            _cursorHideEnforcementTimer.Stop();
            _volumeOsdHideTimer.Stop();
            _autoplayReassertTimer.Stop();
            GpuVideoSurface.SizeChanged -= GpuVideoSurface_PreInitSizeChanged;
            FullSettings.InputBindingsChanged -= FullSettings_InputBindingsChanged;
            PlaybackPersistenceStore.SaveVolume(_currentVolume);
            if (ShouldRememberRecentFiles())
                PlaybackPersistenceStore.SaveLastMedia(_lastLoadedMediaPath, _currentPlaybackPosition);
            else
                PlaybackPersistenceStore.ClearLastMedia();
            try
            {
                PlaybackPersistenceStore.SaveWindow(
                    AppWindow.Position.X,
                    AppWindow.Position.Y,
                    AppWindow.Size.Width,
                    AppWindow.Size.Height);
            }
            catch (COMException ex)
            {
                Debug.WriteLine($"Failed to persist window geometry during close. {ex}");
            }
            _playbackOptions.PropertyApplyRequested -= PlaybackOptions_PropertyApplyRequested;
            if (_loopAccentSubscribed)
            {
                AccentColorService.AccentColorChanged -= OnAccentColorChangedForLoopGradient;
                _loopAccentSubscribed = false;
            }
            _playbackActivity.Stop();
            RootGrid.SetCursorVisible(true);
            VideoContainer.SetCursorVisible(true);
            ShowNativeCursorIfHidden();
            _gpuSurfaceSmokeHost?.Dispose();
            UnregisterPlayerBackend(_gpuPlayer);
            _gpuPlayer?.Dispose();
            UnregisterPlayerBackend(_softwarePlayer);
            _softwarePlayer?.Dispose();

            if (_mainHwnd != IntPtr.Zero)
            {
                RemoveWindowSubclass(_mainHwnd, _windowSubclassProc, WINDOW_SUBCLASS_ID);
            }

            foreach (var childHwnd in _hookedCursorChildHwnds)
                RemoveWindowSubclass(childHwnd, _childCursorSubclassProc, CHILD_CURSOR_SUBCLASS_ID);
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            try
            {
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(null);
            }
            catch (COMException ex)
            {
                Debug.WriteLine($"Failed to configure title bar extension during activation. {ex}");
            }

            // Make sure the main element can receive focus and keys
            if (this.Content is not null)
            {
                try
                {
                    this.Content.IsTabStop = true;
                    this.Content.Focus(FocusState.Programmatic);
                }
                catch (COMException ex)
                {
                    Debug.WriteLine($"Failed to focus window content during activation. {ex}");
                }
            }

            if (this.Content is not null)
            {
                this.Content.KeyDown += (s, e) =>
                {
                    if (TryExecuteRuntimeInput(e.Key))
                    {
                        MarkPlaybackActivity();
                        e.Handled = true;
                        return;
                    }

                    if (HandleLegacyKeyDown(e.Key))
                    {
                        MarkPlaybackActivity();
                        e.Handled = true;
                    }
                };
            }

            try
            {
                this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                ApplyTitleBarInsets();
            }
            catch (COMException ex)
            {
                Debug.WriteLine($"Failed to configure AppWindow title bar during activation. {ex}");
            }
            ApplyPersistedWindowState();
            try
            {
                EnsureWindowMessageHook();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to install window message hook during activation. {ex}");
            }

            try
            {
                this.AppWindow.Changed += (s, e) =>
                {
                    ApplyTitleBarInsets();

                };
            }
            catch (COMException ex)
            {
                Debug.WriteLine($"Failed to subscribe AppWindow changed event during activation. {ex}");
            }

            // Wait for the UI element to finish loading before initializing mpv
            VideoContainer.Loaded += VideoContainer_Loaded;
            GpuVideoSurface.Loaded += GpuVideoSurface_Loaded;
            GpuVideoSurface.SizeChanged += GpuVideoSurface_PreInitSizeChanged;

            // Loaded events can fire before these handlers are attached; always queue
            // one fallback initialization attempt to keep startup deterministic.
            EnsureVisiblePlayerInitialization();
        }

        private void MainWindow_CursorActivationChanged(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                RootGrid.SetCursorVisible(true);
                VideoContainer.SetCursorVisible(true);
                ShowNativeCursorIfHidden();
                return;
            }

            RefreshPlaybackActivityState();
        }

        private void VideoContainer_Loaded(object sender, RoutedEventArgs e)
        {
            VideoContainer.Loaded -= VideoContainer_Loaded;
            ApplyVideoSurfaceMode();

            if (!_useGpuVideoSurface)
                TryInitializeVisiblePlayer();
        }

        private void GpuVideoSurface_Loaded(object sender, RoutedEventArgs e)
        {
            GpuVideoSurface.Loaded -= GpuVideoSurface_Loaded;

            if (!_useGpuVideoSurface)
                return;

            EnsureVisiblePlayerInitialization();
        }

        private void GpuVideoSurface_PreInitSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_visiblePlayerInitialized || !_useGpuVideoSurface)
                return;

            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
                return;

            EnsureVisiblePlayerInitialization();
        }

        private void EnsureVisiblePlayerInitialization()
        {
            if (_visiblePlayerInitialized || _isVisiblePlayerInitializationQueued)
                return;

            _isVisiblePlayerInitializationQueued = true;
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                _isVisiblePlayerInitializationQueued = false;
                TryInitializeVisiblePlayer();
            });
        }

        private void TryInitializeVisiblePlayer()
        {
            if (_visiblePlayerInitialized)
                return;

            ApplyVideoSurfaceMode();

            try
            {
                SmokeAttachGpuSurface();

                if (_useGpuVideoSurface)
                {
                    if (GpuVideoSurface.ActualWidth <= 0 || GpuVideoSurface.ActualHeight <= 0)
                        return;

                    InitializeNativeGpuPlayer();
                    _visiblePlayerInitialized = true;
                    GpuVideoSurface.SizeChanged -= GpuVideoSurface_PreInitSizeChanged;
                    TryLoadPendingMedia();
                    TryLoadStartupMedia();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GPU renderer startup failed. Falling back to software rendering. {ex}");
                _gpuSurfaceSmokeHost?.Dispose();
                _gpuSurfaceSmokeHost = null;
                UnregisterPlayerBackend(_gpuPlayer);
                _gpuPlayer?.Dispose();
                _gpuPlayer = null;
            }

            _useGpuVideoSurface = false;
            ApplyVideoSurfaceMode();
            GpuVideoSurface.SizeChanged -= GpuVideoSurface_PreInitSizeChanged;
            InitializeNativeSoftwarePlayer();
            _visiblePlayerInitialized = true;
            TryLoadPendingMedia();
            TryLoadStartupMedia();
        }

        private void ApplyVideoSurfaceMode()
        {
            GpuVideoSurface.Visibility = _useGpuVideoSurface ? Visibility.Visible : Visibility.Collapsed;
            VideoFrame.Visibility = _useGpuVideoSurface ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SmokeAttachGpuSurface()
        {
            if (!RunGpuSurfaceSmoke)
                return;

            _gpuSurfaceSmokeHost = new D3D11SwapChainPanelHost();
            _gpuSurfaceSmokeHost.Attach(GpuVideoSurface, width: 16, height: 16);
            _gpuSurfaceSmokeHost.Present();
        }

        private void ToggleSettingsPanel()
        {
            var shouldShow = FullSettingsHost.Visibility != Visibility.Visible;
            if (shouldShow)
                OpenFullSettingsPanel();
            else
                CloseFullSettingsPanel();

            RefreshPlaybackActivityState();
        }

        private void OpenFullSettingsPanel()
        {
            if (_isFullscreen)
                ExitFullscreen();

            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            CloseRightDrawer(updateCursor: false);
            FullSettingsHost.Visibility = Visibility.Visible;
        }

        private void CloseFullSettingsPanel()
        {
            FullSettingsHost.Visibility = Visibility.Collapsed;
        }

        private void CommandRail_Loaded(object sender, RoutedEventArgs e)
        {
            CommandRail.Loaded -= CommandRail_Loaded;
            SetCommandRailExpanded(false);
        }

        private void TopLeftMenuButton_Click(object sender, RoutedEventArgs e)
        {
            var topChromeHeight = TopChrome.ActualHeight > 0 ? TopChrome.ActualHeight : TopChrome.Height;
            VideoContextFlyout.ShowAt(RootGrid, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Position = new Windows.Foundation.Point(0, topChromeHeight)
            });
        }

        private void SetCommandRailExpanded(bool expanded)
        {
            _isCommandRailExpanded = expanded;
            CommandRail.Width = expanded ? CommandRailExpandedWidth : CommandRailCollapsedWidth;
            SetTaggedVisibility(CommandRail, "RailExpanded", expanded ? Visibility.Visible : Visibility.Collapsed);

            if (!expanded)
                HideRailSubMenus();

            UpdateEmbeddedPanelOffset();
        }

        private void ToggleRailSubMenu(StackPanel subMenu)
        {
            if (!_isCommandRailExpanded)
                SetCommandRailExpanded(true);

            var shouldOpen = subMenu.Visibility != Visibility.Visible;
            HideRailSubMenus();
            subMenu.Visibility = shouldOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideRailSubMenus()
        {
            MediaRailSubMenu.Visibility = Visibility.Collapsed;
            PlaylistRailSubMenu.Visibility = Visibility.Collapsed;
            ToolsRailSubMenu.Visibility = Visibility.Collapsed;
        }

        private void RailMediaButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRailSubMenu(MediaRailSubMenu);
            ShowEmbeddedPanel("Media", "Media", "Open files and browse recent media.");
        }

        private void RailPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRailSubMenu(PlaylistRailSubMenu);
            ShowEmbeddedPanel("Playlist", "Playlist", "Queue, import, and export media lists.");
        }

        private void RailEqualizerButton_Click(object sender, RoutedEventArgs e)
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Equalizer", "Equalizer", "Shape playback sound without leaving the video.");
        }

        private void RailToolsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRailSubMenu(ToolsRailSubMenu);
            ShowEmbeddedPanel("Tools", "Tools", "Effects, preferences, and utility actions.");
        }

        private void RailHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Help", "Help", "Shortcuts and support actions.");
        }

        private void OpenFileFromCommand()
        {
            RailOpenFileButton_Click(this, new RoutedEventArgs());
        }

        private void OpenFolderFromCommand()
        {
            RailOpenFolderButton_Click(this, new RoutedEventArgs());
        }

        private void OpenRecentFromCommand()
        {
            RailRecentButton_Click(this, new RoutedEventArgs());
        }

        private void ShowPlaylistFromCommand()
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Playlist", "Playlist", "Queue, import, and export media lists.");
        }

        private void ShowToolsFromCommand()
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Tools", "Tools", "Effects, preferences, and utility actions.");
        }

        private void ShowHelpFromCommand()
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Help", "Help", "Shortcuts and support actions.");
        }

        private void ShowFavoritesFromCommand()
        {
            HideRailSubMenus();
            ShowEmbeddedPanel("Favorites", "Favorites", "Favorite media shortcuts and quick actions.");
        }

        private void ToggleAdjustmentsFromCommand()
        {
            ToggleRightDrawer(VideoAdjustments);
        }

        private void ToggleCommandRailFromCommand()
        {
            SetCommandRailExpanded(!_isCommandRailExpanded);
            MarkPlaybackActivity();
        }

        private async void RailOpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Media", "Media", "Open files and browse recent media.");

            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, GetWindowHandle());
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            LoadMedia(file.Path);
        }

        private async void RailOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Media", "Media", "Opening the first playable file in the folder.");

            var picker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, GetWindowHandle());
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;

            var files = await folder.GetFilesAsync();
            var mediaFile = files.FirstOrDefault(file => IsSupportedMediaPath(file.Path));
            if (mediaFile is null)
            {
                EmbeddedPanelSubtitle.Text = "No supported media files were found in that folder.";
                return;
            }

            LoadMedia(mediaFile.Path);
        }

        private void RailRecentButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Media", "Recent files", "Recently opened media will appear here.");
        }

        private void RailPlaylistQueueButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Playlist", "Playlist", "Queue, import, and export media lists.");
        }

        private void RailPlaylistImportButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Playlist", "Import playlist", "No playlist file selected.");
        }

        private void RailPlaylistExportButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Playlist", "Export playlist", "No playlist is loaded.");
        }

        private void RailEffectsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Tools", "Effects and filters", "Video and audio effect controls.");
        }

        private void RailPreferencesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFullSettingsPanel();
            RefreshPlaybackActivityState();
        }

        private void RailConverterButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Tools", "Media converter", "Conversion tools.");
        }

        private void CloseEmbeddedPanelButton_Click(object sender, RoutedEventArgs e)
        {
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            RefreshPlaybackActivityState();
        }

        private void ShowEmbeddedPanel(string panel, string title, string subtitle)
        {
            EmbeddedPanelTitle.Text = title;
            EmbeddedPanelSubtitle.Text = subtitle;

            MediaEmbeddedPanel.Visibility = panel == "Media" ? Visibility.Visible : Visibility.Collapsed;
            PlaylistEmbeddedPanel.Visibility = panel == "Playlist" ? Visibility.Visible : Visibility.Collapsed;
            EqualizerEmbeddedPanel.Visibility = panel == "Equalizer" ? Visibility.Visible : Visibility.Collapsed;
            ToolsEmbeddedPanel.Visibility = panel == "Tools" ? Visibility.Visible : Visibility.Collapsed;
            HelpEmbeddedPanel.Visibility = panel == "Help" ? Visibility.Visible : Visibility.Collapsed;

            EmbeddedPanelHost.Visibility = Visibility.Visible;
            UpdateEmbeddedPanelOffset();
            RefreshPlaybackActivityState();
        }

        private void UpdateEmbeddedPanelOffset()
        {
            var commandRailWidth = CommandRail.Visibility == Visibility.Visible ? CommandRail.Width : 0;
            EmbeddedPanelHost.Margin = new Thickness(commandRailWidth, TopChromeHeight, 0, TransportBarHeight);
        }

        private IntPtr GetWindowHandle()
        {
            if (_mainHwnd == IntPtr.Zero)
                _mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            return _mainHwnd;
        }

        private static bool IsSupportedMediaPath(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" or ".m4v" or ".mp3" or ".flac" or ".wav" or ".m4a";
        }

        private static void SetTaggedVisibility(DependencyObject root, string tag, Visibility visibility)
        {
            if (root is FrameworkElement element
                && string.Equals(element.Tag as string, tag, StringComparison.Ordinal))
            {
                element.Visibility = visibility;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
                SetTaggedVisibility(VisualTreeHelper.GetChild(root, i), tag, visibility);
        }

        private void EnsureWindowMessageHook()
        {
            _mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetWindowSubclass(_mainHwnd, _windowSubclassProc, WINDOW_SUBCLASS_ID, IntPtr.Zero);
            EnsureChildCursorHooks();
        }

        private IntPtr WindowSubclassProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            IntPtr dwRefData)
        {
            if (msg == WM_SETCURSOR && ShouldForceHideMouseCursor())
            {
                SetCursor(IntPtr.Zero);
                return new IntPtr(1);
            }

            if ((msg == WM_MOUSEMOVE || msg == WM_NCMOUSEMOVE) && _isNativeCursorHidden)
            {
                DispatcherQueue.TryEnqueue(MarkPlaybackActivity);
            }

            if (msg == WM_KEYDOWN)
            {
                var vk = (int)wParam;
                if (vk == 0x46) // F
                {
                    DispatcherQueue.TryEnqueue(() => _commandDispatcher.Execute(AethraCommandIds.ToggleFullscreen));
                    return new IntPtr(1);
                }
            }

            if (msg == WM_KEYDOWN && wParam == (IntPtr)VK_S)
            {
                DispatcherQueue.TryEnqueue(() => _commandDispatcher.Execute(AethraCommandIds.ToggleSettings));
                return new IntPtr(1);
            }

            if (msg == WM_KEYDOWN && wParam == (IntPtr)VK_ESCAPE)
            {
                DispatcherQueue.TryEnqueue(() => _commandDispatcher.Execute(AethraCommandIds.ExitOverlayOrFullscreen));
                return new IntPtr(1);
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void FullSettings_CloseRequested(object? sender, EventArgs e)
        {
            CloseFullSettingsPanel();
            RefreshPlaybackActivityState();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsPanel();
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightDrawer(VideoAdjustments);
        }

        private void ToggleRightDrawer(UIElement panel)
        {
            var shouldShow = RightDrawerHost.Visibility != Visibility.Visible
                || panel.Visibility != Visibility.Visible;

            foreach (var child in RightDrawerHost.Children.OfType<UIElement>())
                child.Visibility = Visibility.Collapsed;

            if (shouldShow)
            {
                FullSettingsHost.Visibility = Visibility.Collapsed;
                panel.Visibility = Visibility.Visible;
                RightDrawerHost.Visibility = Visibility.Visible;
            }
            else
            {
                RightDrawerHost.Visibility = Visibility.Collapsed;
            }

            RefreshPlaybackActivityState();
        }

        private void CloseRightDrawer(bool updateCursor = true)
        {
            RightDrawerHost.Visibility = Visibility.Collapsed;

            if (updateCursor)
                RefreshPlaybackActivityState();
        }

        private void VideoAdjustments_CloseRequested(object? sender, EventArgs e)
        {
            CloseRightDrawer();
        }

        private void VideoAdjustments_AdjustmentChanged(object? sender, VideoAdjustmentChangedEventArgs e)
        {
            _videoAdjustmentBatcher.Queue(e.MpvProperty, e.Value);
        }

        private void PlaybackOptions_PropertyApplyRequested(object? sender, PlaybackPropertyApplyEventArgs e)
        {
            ForEachPlayerBackend(player => player.SetProperty(e.PropertyName, e.PropertyValue));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (RightDrawerHost.Visibility == Visibility.Visible)
                CloseRightDrawer(updateCursor: false);
            else if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                EmbeddedPanelHost.Visibility = Visibility.Collapsed;

            RefreshPlaybackActivityState();
        }

        private void SeekBackButton_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(-10);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback();
        }

        private void SeekForwardButton_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(30);
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            if (_isFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        private void EnterFullscreen()
        {
            _wasMaximizedBeforeFullscreen =
                this.AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.State == OverlappedPresenterState.Maximized;

            _isFullscreen = true;
            TopChrome.Visibility = Visibility.Collapsed;
            CommandRail.Visibility = Visibility.Collapsed;
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            RightDrawerHost.Visibility = Visibility.Collapsed;
            FullSettingsHost.Visibility = Visibility.Collapsed;
            RefreshPlaybackActivityState();
            this.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        private void ExitFullscreen()
        {
            _isFullscreen = false;
            this.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (_wasMaximizedBeforeFullscreen
                && this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            TopChrome.Visibility = Visibility.Visible;
            UpdateEmbeddedPanelOffset();
            ApplyTitleBarInsets();
            RefreshPlaybackActivityState();
        }

        private void InitializeNativeSoftwarePlayer()
        {
            _softwarePlayer = new NativeMpvSoftwarePlayer(
                DispatcherQueue,
                bitmap => VideoFrame.Source = bitmap);
            _softwarePlayer.ProgressChanged += Player_ProgressChanged;
            _softwarePlayer.PlaybackPausedChanged += Player_PlaybackPausedChanged;
            _softwarePlayer.ChaptersChanged += Player_ChaptersChanged;
            RegisterPlayerBackend(_softwarePlayer);
        }

        private void InitializeNativeGpuPlayer()
        {
            _gpuPlayer = new NativeMpvOpenGlPlayer(DispatcherQueue, GpuVideoSurface, GpuPlayer_Failed);
            _gpuPlayer.ProgressChanged += Player_ProgressChanged;
            _gpuPlayer.PlaybackPausedChanged += Player_PlaybackPausedChanged;
            _gpuPlayer.ChaptersChanged += Player_ChaptersChanged;
            GpuVideoSurface.SizeChanged += GpuVideoSurface_SizeChanged;
            RegisterPlayerBackend(_gpuPlayer);
        }

        private void GpuPlayer_Failed(Exception ex)
        {
            Debug.WriteLine($"GPU renderer task failed. Falling back to software rendering. {ex}");

            GpuVideoSurface.SizeChanged -= GpuVideoSurface_SizeChanged;
            UnregisterPlayerBackend(_gpuPlayer);
            _gpuPlayer?.Dispose();
            _gpuPlayer = null;

            if (_softwarePlayer is not null)
                return;

            _useGpuVideoSurface = false;
            ApplyVideoSurfaceMode();
            InitializeNativeSoftwarePlayer();
        }

        private void VideoContainer_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(VideoContainer);
            if (!point.Properties.IsLeftButtonPressed)
            {
                if (TryExecuteRuntimePointerPress(point))
                {
                    MarkPlaybackActivity();
                    e.Handled = true;
                }

                return;
            }

            MarkPlaybackActivity();
            _videoPointerPressedAt = point.Position;
            _videoWindowDragStartWindowPosition = AppWindow.Position;
            GetCursorPos(out _videoWindowDragStartCursorPosition);
            _videoPointerId = e.Pointer.PointerId;
            _isVideoPointerPressPending = true;
            _isVideoPointerDraggingWindow = false;
            VideoContainer.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private bool TryExecuteRuntimePointerPress(Microsoft.UI.Input.PointerPoint point)
        {
            var properties = point.Properties;

            if (properties.IsRightButtonPressed)
                return ExecuteRuntimePointerCommand("MBTN_RIGHT");
            if (properties.IsMiddleButtonPressed)
                return ExecuteRuntimePointerCommand("MBTN_MID");
            if (properties.IsXButton1Pressed)
                return ExecuteRuntimePointerCommand("MBTN_BACK");
            if (properties.IsXButton2Pressed)
                return ExecuteRuntimePointerCommand("MBTN_FORWARD");

            return false;
        }

        private void VideoContainer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _videoPointerId
                || (!_isVideoPointerPressPending && !_isVideoPointerDraggingWindow))
            {
                return;
            }

            var point = e.GetCurrentPoint(VideoContainer);
            if (!point.Properties.IsLeftButtonPressed)
            {
                ResetVideoPointerPress(e);
                return;
            }

            if (_isVideoPointerDraggingWindow)
            {
                MoveWindowForVideoDrag();
                e.Handled = true;
                return;
            }

            var xDelta = Math.Abs(point.Position.X - _videoPointerPressedAt.X);
            var yDelta = Math.Abs(point.Position.Y - _videoPointerPressedAt.Y);

            if (xDelta < WindowDragThreshold && yDelta < WindowDragThreshold)
                return;

            _isVideoPointerDraggingWindow = true;
            _isVideoPointerPressPending = false;
            MarkPlaybackActivity();

            if (!_isFullscreen)
                BeginWindowDrag();

            e.Handled = true;
        }

        private void VideoContainer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _videoPointerId)
                return;

            var shouldTogglePlayback = _isVideoPointerPressPending && !_isVideoPointerDraggingWindow;
            ResetVideoPointerPress(e);

            if (shouldTogglePlayback)
            {
                MarkPlaybackActivity();
                TogglePlayback();
            }

            e.Handled = true;
        }

        private void VideoContainer_PointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == _videoPointerId)
                ResetVideoPointerPress(e);
        }

        private void ResetVideoPointerPress(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isVideoPointerPressPending || _isVideoPointerDraggingWindow)
                VideoContainer.ReleasePointerCapture(e.Pointer);

            _isVideoPointerPressPending = false;
            _isVideoPointerDraggingWindow = false;
            _videoPointerId = 0;
        }

        private void BeginWindowDrag()
        {
            ShowNativeCursorIfHidden();
            MoveWindowForVideoDrag();
        }

        private void MoveWindowForVideoDrag()
        {
            if (!GetCursorPos(out var cursorPosition))
                return;

            var xDelta = cursorPosition.X - _videoWindowDragStartCursorPosition.X;
            var yDelta = cursorPosition.Y - _videoWindowDragStartCursorPosition.Y;
            AppWindow.Move(new Windows.Graphics.PointInt32(
                _videoWindowDragStartWindowPosition.X + xDelta,
                _videoWindowDragStartWindowPosition.Y + yDelta));
        }

        private void VideoContextFlyout_Opening(object? sender, object e)
        {
            if (FullSettingsHost.Visibility == Visibility.Visible
                || RightDrawerHost.Visibility == Visibility.Visible)
            {
                _isVideoContextFlyoutOpen = false;
                VideoContextFlyout.Hide();
                return;
            }

            _isVideoContextFlyoutOpen = true;
            MarkPlaybackActivity();
            var (label, glyph) = PlayPauseVisualFor(_isPlaybackPaused);
            ContextPlayPauseItem.Text = label;
            ContextPlayPauseIcon.Glyph = glyph;
            ContextFullscreenItem.Text = _isFullscreen ? "Exit fullscreen" : "Enter fullscreen";
            ContextFullscreenIcon.Glyph = _isFullscreen ? "\uE73F" : "\uE740";
        }

        private void VideoContextFlyout_Closed(object? sender, object e)
        {
            _isVideoContextFlyoutOpen = false;
            RefreshPlaybackActivityState();
        }

        private void ContextPlayPauseItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.TogglePlayPause);

        private void ContextSeekBackItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.SeekBack10);

        private void ContextSeekForwardItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.SeekForward30);

        private void ContextFullscreenItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.ToggleFullscreen);

        private void ContextOpenFileItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.OpenFile);

        private void ContextOpenFolderItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.OpenFolder);

        private void ContextRecentItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.OpenRecent);

        private void ContextSettingsItem_Click(object sender, RoutedEventArgs e) => _commandDispatcher.Execute(AethraCommandIds.ToggleSettings);

        private void Player_ProgressChanged(object? sender, NativeMpvPlaybackProgress progress)
        {
            UpdateProgress(progress.Position, progress.Duration);
        }

        private void Player_PlaybackPausedChanged(object? sender, bool isPaused)
        {
            _isPlaybackPaused = isPaused;
            ClosePlayerShellCommandSurfacesForActivePlayback();
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
        }

        private void GpuVideoSurface_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_gpuPlayer is null)
                return;

            var width = (int)Math.Ceiling(e.NewSize.Width);
            var height = (int)Math.Ceiling(e.NewSize.Height);

            if (width <= 0 || height <= 0)
                return;

            _gpuPlayer.RequestResize(width, height);
        }

        private void TogglePlayback()
        {
            _isPlaybackPaused = !_isPlaybackPaused;
            ClosePlayerShellCommandSurfacesForActivePlayback();
            ForEachPlayerBackend(player => player.TogglePause());
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
        }

        private void PausePlayback()
        {
            _isPlaybackPaused = true;
            ForEachPlayerBackend(player => player.Pause());
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
        }

        private void SeekRelative(double seconds)
        {
            ForEachPlayerBackend(player => player.Seek(seconds));
        }

        private void SeekToPercent(double percent)
        {
            ForEachPlayerBackend(player => player.SeekToPercent(percent));
        }

        private void AddVolume(int amount)
        {
            _currentVolume = Math.Clamp(_currentVolume + amount, 0, 100);
            SetVolume(_currentVolume);
            UpdateVolumeUi();
        }

        private void SetVolume(double value)
        {
            var clamped = Math.Clamp(value, 0.0, 100.0);
            _currentVolume = clamped;
            ForEachPlayerBackend(player => player.SetVolume(clamped));
        }

        private void ToggleMute()
        {
            _isMuted = !_isMuted;
            _playbackOptions.ApplyStringProperty("mute", _isMuted ? "yes" : "no");
            MarkPlaybackActivity();
        }

        private void UpdateVolumeUi()
        {
            if (VolumeValueText is not null)
                VolumeValueText.Text = $"{Math.Round(_currentVolume):0}%";

            if (VolumeSlider is null)
                return;

            _suppressVolumeSliderValueChanged = true;
            try
            {
                VolumeSlider.Value = _currentVolume;
            }
            finally
            {
                _suppressVolumeSliderValueChanged = false;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || _suppressVolumeSliderValueChanged)
                return;

            SetVolume(e.NewValue);
            UpdateVolumeUi();
            MarkPlaybackActivity();
        }

        private void FullSettings_InputBindingsChanged(object? sender, IReadOnlyList<InputBindingSetting> bindings)
        {
            _currentInputBindings.Clear();
            _currentInputBindings.AddRange(bindings);
            _inputRuntimeService.LoadBindings(_currentInputBindings);
        }

        private void VideoContainer_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HandleVolumeWheel(e);
        }

        private void VolumeButton_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HandleVolumeWheel(e);
        }

        private void HandleVolumeWheel(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if (delta > 0 && ExecuteRuntimePointerCommand("WHEEL_UP"))
            {
                MarkPlaybackActivity();
                e.Handled = true;
                return;
            }

            if (delta < 0 && ExecuteRuntimePointerCommand("WHEEL_DOWN"))
            {
                MarkPlaybackActivity();
                e.Handled = true;
                return;
            }

            // Standard Windows wheel notch is a delta of 120; treat each notch as a
            // 5-unit volume change so a single scroll feels like one perceptible step.
            const int VolumeStepPerNotch = 5;
            const double WheelNotch = 120.0;

            if (delta == 0)
                return;

            var notches = (int)Math.Round(delta / WheelNotch);
            if (notches == 0)
                notches = delta > 0 ? 1 : -1;

            AddVolume(notches * VolumeStepPerNotch);
            ShowVolumeOsd();
            MarkPlaybackActivity();
            e.Handled = true;
        }

        private bool ExecuteRuntimePointerCommand(string token)
        {
            var gesture = new InputGesture(
                InputGesture.NormalizePrimaryToken(token),
                IsModifierPressed(VirtualKey.Control),
                IsModifierPressed(VirtualKey.Shift),
                IsModifierPressed(VirtualKey.Menu));

            if (!_inputRuntimeService.TryGetCommand(gesture, out var command))
                return false;

            return ExecuteInputCommand(command);
        }

        private void LoopAButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLoopPointA();
            MarkPlaybackActivity();
        }

        private void LoopBButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLoopPointB();
            MarkPlaybackActivity();
        }

        private void ToggleLoopPointA()
        {
            if (_loopPointA.HasValue)
            {
                _loopPointA = null;
                // mpv accepts the literal "no" to clear an ab-loop point.
                _playbackOptions.ApplyStringProperty("ab-loop-a", "no");
            }
            else
            {
                _loopPointA = _currentPlaybackPosition;
                _playbackOptions.ApplyNumericProperty("ab-loop-a", _currentPlaybackPosition);
            }

            UpdateLoopButtonVisuals();
            RefreshSeekBarFill();
            UpdateLoopMarkers();
        }

        private void ToggleLoopPointB()
        {
            if (_loopPointB.HasValue)
            {
                _loopPointB = null;
                _playbackOptions.ApplyStringProperty("ab-loop-b", "no");
            }
            else
            {
                _loopPointB = _currentPlaybackPosition;
                _playbackOptions.ApplyNumericProperty("ab-loop-b", _currentPlaybackPosition);

                // Setting B activates the loop, so jump back to A immediately for
                // instant feedback. If A wasn't set, the loop start is the file
                // beginning — jump to 0.
                var seekTarget = _loopPointA ?? 0.0;
                ForEachPlayerBackend(player => player.SeekToTime(seekTarget));
            }

            UpdateLoopButtonVisuals();
            RefreshSeekBarFill();
            UpdateLoopMarkers();
        }

        private void UpdateLoopMarkers()
        {
            // For each arrow marker, we want the *apex* to land on the marked
            // position (rather than the polygon's bounding-box center, which would
            // visually look offset since the visual mass of a triangle isn't at the
            // box center). A's apex is on the right edge of the polygon; B's apex
            // is on the left edge. Both arrows then point INTO the loop region.
            UpdateLoopMarker(LoopAMarker, LoopAMarkerTransform, _loopPointA, apexAtRight: true);
            UpdateLoopMarker(LoopBMarker, LoopBMarkerTransform, _loopPointB, apexAtRight: false);
        }

        private void UpdateLoopMarker(
            Microsoft.UI.Xaml.Shapes.Polygon? marker,
            TranslateTransform? transform,
            double? point,
            bool apexAtRight)
        {
            if (marker is null || transform is null)
                return;

            if (!point.HasValue
                || _lastKnownDurationSeconds <= 0
                || NativeProgressBar.ActualWidth <= 0)
            {
                marker.Visibility = Visibility.Collapsed;
                return;
            }

            var percent = Math.Clamp(point.Value / _lastKnownDurationSeconds * 100.0, 0.0, 100.0);
            var pointX = percent / 100.0 * NativeProgressBar.ActualWidth;
            // Align the polygon's apex with the marked position.
            // - apexAtRight (A): right edge of the polygon at pointX -> shift left by Width.
            // - !apexAtRight (B): left edge of the polygon at pointX -> shift not needed.
            transform.X = apexAtRight ? pointX - marker.Width : pointX;
            marker.Visibility = Visibility.Visible;
        }

        private void RefreshSeekBarFill()
        {
            if (NativeProgressBar is null)
                return;

            // No A set: solid accent value-fill (the standard look).
            if (!_loopPointA.HasValue)
            {
                NativeProgressBar.Foreground = (Brush)Application.Current.Resources["AethraAccentBrush"];
                return;
            }

            // A set but the playhead hasn't passed it (or duration unknown):
            // the value-fill rectangle is shorter than A's pixel offset, so the
            // [A, current] portion is empty. Use solid accent and let the loop
            // bounce the playhead forward; this state is normally short-lived.
            if (_currentPlaybackPosition <= _loopPointA.Value || _lastKnownDurationSeconds <= 0)
            {
                NativeProgressBar.Foreground = (Brush)Application.Current.Resources["AethraAccentBrush"];
                return;
            }

            EnsureLoopAccentGradient();
            if (_loopAccentGradient is null)
                return;

            // The slider's value-fill rectangle spans [0, currentX] in slider coords,
            // and a relative LinearGradientBrush is mapped onto that rectangle's
            // bounds. So the cutoff fraction within the brush is A / current.
            var aFraction = Math.Clamp(_loopPointA.Value / _currentPlaybackPosition, 0.0, 1.0);
            _loopAccentGradient.GradientStops[1].Offset = aFraction;
            _loopAccentGradient.GradientStops[2].Offset = aFraction;
            NativeProgressBar.Foreground = _loopAccentGradient;
        }

        private void EnsureLoopAccentGradient()
        {
            if (_loopAccentGradient is not null)
                return;

            // Track color is local to TransportBar.Grid.Resources; pull the brush
            // from there so it stays in sync if the resource value is ever changed.
            // Falls back to the accent brush color so a missing resource won't crash.
            var trackColor = TransportBar?.Resources?["SliderTrackFill"] is SolidColorBrush trackBrush
                ? trackBrush.Color
                : Microsoft.UI.Colors.Gray;

            var accentBrush = (SolidColorBrush)Application.Current.Resources["AethraAccentBrush"];
            var accentColor = accentBrush.Color;

            _loopAccentGradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0.5),
                EndPoint = new Windows.Foundation.Point(1, 0.5),
            };
            // Two stops at the same offset = hard color transition at A.
            _loopAccentGradient.GradientStops.Add(new GradientStop { Offset = 0.0, Color = trackColor });
            _loopAccentGradient.GradientStops.Add(new GradientStop { Offset = 0.0, Color = trackColor });
            _loopAccentGradient.GradientStops.Add(new GradientStop { Offset = 0.0, Color = accentColor });
            _loopAccentGradient.GradientStops.Add(new GradientStop { Offset = 1.0, Color = accentColor });

            if (!_loopAccentSubscribed)
            {
                AccentColorService.AccentColorChanged += OnAccentColorChangedForLoopGradient;
                _loopAccentSubscribed = true;
            }
        }

        private void OnAccentColorChangedForLoopGradient(object? sender, AccentColorChangedEventArgs e)
        {
            if (_loopAccentGradient is null)
                return;

            // Stops 2 and 3 are the accent half of the gradient; refresh them so
            // the loop fill follows accent changes from Preferences.
            _loopAccentGradient.GradientStops[2].Color = e.Color;
            _loopAccentGradient.GradientStops[3].Color = e.Color;
        }

        private void UpdateLoopButtonVisuals()
        {
            // Reach the brushes through Application.Resources rather than capturing
            // them once: the AethraAccentBrush instance is the same one the rest of
            // the app uses, so changing accent in Preferences updates these letters
            // automatically without us having to subscribe to AccentColorChanged.
            var resources = Application.Current.Resources;
            var accentBrush = (Brush)resources["AethraAccentBrush"];
            var mutedBrush = (Brush)resources["AethraMutedTextBrush"];

            if (LoopAText is not null)
                LoopAText.Foreground = _loopPointA.HasValue ? accentBrush : mutedBrush;

            if (LoopBText is not null)
                LoopBText.Foreground = _loopPointB.HasValue ? accentBrush : mutedBrush;

            if (LoopAButton is not null)
                ToolTipService.SetToolTip(LoopAButton,
                    _loopPointA.HasValue
                        ? $"Clear A (set at {PlaybackMetadataFormatter.FormatPlaybackTime(_loopPointA.Value)})"
                        : "Set A loop point");

            if (LoopBButton is not null)
                ToolTipService.SetToolTip(LoopBButton,
                    _loopPointB.HasValue
                        ? $"Clear B (set at {PlaybackMetadataFormatter.FormatPlaybackTime(_loopPointB.Value)})"
                        : "Set B loop point");
        }

        private void ShowVolumeOsd()
        {
            if (VolumeOsd is null || VolumeOsdText is null)
                return;

            VolumeOsdText.Text = $"{Math.Round(_currentVolume):0}%";
            VolumeOsd.Visibility = Visibility.Visible;

            // Restart the linger timer on every call so a continuous scroll keeps the
            // readout visible the whole time and only fades after the user stops.
            _volumeOsdHideTimer.Stop();
            _volumeOsdHideTimer.Start();
        }

        private void VolumeOsdHideTimer_Tick(object? sender, object e)
        {
            _volumeOsdHideTimer.Stop();

            if (VolumeOsd is not null)
                VolumeOsd.Visibility = Visibility.Collapsed;
        }

        private void AutoplayReassertTimer_Tick(object? sender, object e)
        {
            _autoplayReassertTimer.Stop();
            ForEachPlayerBackend(player => player.SetProperty("pause", "no"));
        }

        private void MinimizeWindow()
        {
            if (_isFullscreen)
                ExitFullscreen();

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Minimize();
        }

        private void InitializeVolumeUi()
        {
            UpdateVolumeUi();
        }

        private void LoadMedia(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (ShouldQueueMediaLoad(_activeBackends.Count))
            {
                _pendingMediaPath = path;
                // A user/requested load takes precedence over startup autoload.
                _startupMediaLoaded = true;
                EnsureVisiblePlayerInitialization();
                return;
            }

            MediaTitleText.Text = GetDisplayMediaName(path);
            _lastLoadedMediaPath = path;
            ForEachPlayerBackend(player => player.LoadFile(path));
            if (ShouldAutoplayOnOpen())
            {
                ForEachPlayerBackend(player => player.SetProperty("pause", "no"));
                // Startup/watch-later can reapply pause post-load; reassert shortly after.
                _autoplayReassertTimer.Stop();
                _autoplayReassertTimer.Start();
                _isPlaybackPaused = false;
            }
            else
            {
                _autoplayReassertTimer.Stop();
                ForEachPlayerBackend(player => player.Pause());
                _isPlaybackPaused = true;
            }
            // mpv resets ab-loop across files; clear our cached state too so the
            // A/B button colors don't claim a point is set against the new file.
            _loopPointA = null;
            _loopPointB = null;
            UpdateLoopButtonVisuals();
            RefreshSeekBarFill();
            UpdateLoopMarkers();
            ClosePlayerShellCommandSurfacesForActivePlayback();
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
        }

        internal static bool ShouldQueueMediaLoad(int activeBackendCount)
        {
            return activeBackendCount <= 0;
        }

        private void TryLoadPendingMedia()
        {
            if (string.IsNullOrWhiteSpace(_pendingMediaPath))
                return;

            var path = _pendingMediaPath;
            _pendingMediaPath = null;
            LoadMedia(path);
        }

        // Play/pause surfaces show the action available to the user.
        private static (string Label, string Glyph) PlayPauseVisualFor(bool isPaused) =>
            isPaused
                ? ("Play", "\uE768")
                : ("Pause", "\uE769");

        private void ApplyPlayPauseVisualState()
        {
            var (_, glyph) = PlayPauseVisualFor(_isPlaybackPaused);
            PlayPauseIcon.Glyph = glyph;
            PlayPauseButton.Background = (Brush)Application.Current.Resources["AethraVideoBrush"];
            PlayPauseButton.BorderBrush = _isPlaybackPaused
                ? (Brush)Application.Current.Resources["AethraAccentBrush"]
                : (Brush)Application.Current.Resources["AethraVideoBrush"];
        }

        private static string GetDisplayMediaName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Ready";

            try
            {
                var fileName = Path.GetFileName(path);
                return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
            }
            catch
            {
                return path;
            }
        }

        private void ApplyTitleBarInsets()
        {
            try
            {
                TopChrome.Padding = new Thickness(0, 0, this.AppWindow.TitleBar.RightInset, 0);
            }
            catch (COMException ex)
            {
                Debug.WriteLine($"Failed to apply title bar insets. {ex}");
            }
        }

        private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(RootGrid).Position;
            _lastRootPointerPosition = position;
            _hasLastRootPointerPosition = true;
            _playbackActivity.NotifyPointerMoved(position);
        }

        private void TransportBar_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isPointerOverTransportBar = true;
            RefreshPlaybackActivityState();
        }

        private void TransportBar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isPointerOverTransportBar = false;
            RefreshPlaybackActivityState();
        }

        private void PlaybackActivity_ModeChanged(object? sender, EventArgs e)
        {
            ApplyPlaybackActivityState();
        }

        private void MarkPlaybackActivity()
        {
            if (!_isPlaybackPaused)
            {
                if (!_playbackActivity.IsEnabled)
                    _playbackActivity.Start();

                _playbackActivity.MarkActive();
            }

            ApplyPlaybackActivityState();
        }

        private void RefreshPlaybackActivityState()
        {
            if (_isPlaybackPaused)
            {
                _playbackActivity.Stop();
            }
            else if (!_playbackActivity.IsEnabled)
            {
                _playbackActivity.Start();
            }
            else if (!CanLetPlaybackChromeIdle())
            {
                _playbackActivity.MarkActive();
            }

            ApplyPlaybackActivityState();
        }

        private void ApplyPlaybackActivityState()
        {
            ApplyPlayerShellCommandRailVisibility();

            var shouldShowTransport = !_isFullscreen
                || _playbackActivity.Mode == PlaybackActivityMode.Active
                || !CanLetPlaybackChromeIdle();

            TransportBar.Visibility = shouldShowTransport ? Visibility.Visible : Visibility.Collapsed;
            UpdateCursorVisibility();
        }

        private bool CanLetPlaybackChromeIdle()
        {
            if (_isPlaybackPaused
                || _isPointerOverTransportBar
                || _isVideoContextFlyoutOpen
                || EmbeddedPanelHost.Visibility == Visibility.Visible
                || RightDrawerHost.Visibility == Visibility.Visible
                || FullSettingsHost.Visibility == Visibility.Visible)
            {
                return false;
            }

            if (!_hasLastRootPointerPosition)
                return _isFullscreen;

            return IsPointerOverPlaybackSurface(_lastRootPointerPosition);
        }

        // Single source of truth for cursor visibility. Called whenever any state
        // that affects whether the cursor should be visible changes. The cursor
        // hides when playing media has entered idle mode over the playback area.
        private bool ShouldForceHideMouseCursor()
        {
            return _playbackActivity.Mode == PlaybackActivityMode.Idle
                && CanLetPlaybackChromeIdle();
        }

        private void UpdateCursorVisibility()
        {
            var shouldHide = ShouldForceHideMouseCursor();
            RootGrid.SetCursorVisible(!shouldHide);
            VideoContainer.SetCursorVisible(!shouldHide);
            SetNativeCursorVisible(!shouldHide);
        }

        private bool IsPointerOverPlaybackSurface(Windows.Foundation.Point position)
        {
            if (!IsPointInsideElement(VideoContainer, position))
                return false;

            return !IsPointInsideElement(TopChrome, position)
                && !IsPointInsideElement(CommandRail, position)
                && !IsPointInsideElement(TransportBar, position)
                && !IsPointInsideElement(EmbeddedPanelHost, position)
                && !IsPointInsideElement(RightDrawerHost, position)
                && !IsPointInsideElement(FullSettingsHost, position);
        }

        private bool IsPointInsideElement(FrameworkElement element, Windows.Foundation.Point position)
        {
            if (element.Visibility != Visibility.Visible
                || element.ActualWidth <= 0
                || element.ActualHeight <= 0)
            {
                return false;
            }

            var bounds = element.TransformToVisual(RootGrid)
                .TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

            return bounds.Contains(position);
        }

        private bool ShouldShowPlayerShellCommandRail()
        {
            return !_isFullscreen && _isPlaybackPaused;
        }

        private void ApplyPlayerShellCommandRailVisibility()
        {
            var shouldShowRail = ShouldShowPlayerShellCommandRail();

            if (!shouldShowRail)
            {
                if (_isCommandRailExpanded)
                    SetCommandRailExpanded(false);
                else
                    HideRailSubMenus();
            }

            CommandRail.Visibility = shouldShowRail ? Visibility.Visible : Visibility.Collapsed;
            UpdateEmbeddedPanelOffset();
        }

        private void ClosePlayerShellCommandSurfacesForActivePlayback()
        {
            if (_isPlaybackPaused)
                return;

            if (_isCommandRailExpanded)
                SetCommandRailExpanded(false);
            else
                HideRailSubMenus();

            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
        }

        private void SetNativeCursorVisible(bool isVisible)
        {
            if (isVisible)
                ShowNativeCursorIfHidden();
            else
                HideNativeCursor();
        }

        private void HideNativeCursor()
        {
            EnsureChildCursorHooks();
            ClobberClassCursors(IntPtr.Zero);

            if (!_isNativeCursorHidden)
            {
                _isNativeCursorHidden = true;
                HideCursorDisplayCounter();
            }

            SetCursor(IntPtr.Zero);

            if (!_cursorHideEnforcementTimer.IsEnabled)
                _cursorHideEnforcementTimer.Start();
        }

        private void ShowNativeCursorIfHidden()
        {
            _cursorHideEnforcementTimer.Stop();

            if (!_isNativeCursorHidden)
                return;

            _isNativeCursorHidden = false;
            var arrowCursor = LoadCursor(IntPtr.Zero, IDC_ARROW);
            ClobberClassCursors(arrowCursor);
            ShowCursorDisplayCounter();
            SetCursor(arrowCursor);
        }

        private void CursorHideEnforcementTimer_Tick(object? sender, object e)
        {
            if (!ShouldForceHideMouseCursor())
            {
                UpdateCursorVisibility();
                return;
            }

            EnsureChildCursorHooks();
            ClobberClassCursors(IntPtr.Zero);
            SetCursor(IntPtr.Zero);
        }

        private void EnsureChildCursorHooks()
        {
            var mainHwnd = GetWindowHandle();
            EnumChildWindows(mainHwnd, EnumCursorChildHwnd, IntPtr.Zero);
        }

        private bool EnumCursorChildHwnd(IntPtr hWnd, IntPtr lParam)
        {
            if (_hookedCursorChildHwnds.Add(hWnd))
                SetWindowSubclass(hWnd, _childCursorSubclassProc, CHILD_CURSOR_SUBCLASS_ID, IntPtr.Zero);

            return true;
        }

        private void ClobberClassCursors(IntPtr cursor)
        {
            ApplyClassCursor(GetWindowHandle(), cursor);
            EnumChildWindows(GetWindowHandle(), (hWnd, _) =>
            {
                ApplyClassCursor(hWnd, cursor);
                return true;
            }, IntPtr.Zero);
        }

        private void ApplyClassCursor(IntPtr hWnd, IntPtr cursor)
        {
            if (cursor == IntPtr.Zero)
            {
                if (!_originalClassCursors.ContainsKey(hWnd))
                    _originalClassCursors[hWnd] = GetClassLongPtr(hWnd, GCLP_HCURSOR);

                SetClassLongPtr(hWnd, GCLP_HCURSOR, IntPtr.Zero);
                return;
            }

            var cursorToRestore = _originalClassCursors.TryGetValue(hWnd, out var originalCursor)
                ? originalCursor
                : cursor;

            if (cursorToRestore == IntPtr.Zero)
                cursorToRestore = cursor;

            SetClassLongPtr(hWnd, GCLP_HCURSOR, cursorToRestore);
        }

        private static void HideCursorDisplayCounter()
        {
            for (var i = 0; i < CursorCounterSafetyLimit; i++)
            {
                if (ShowCursor(false) < 0)
                    return;
            }
        }

        private static void ShowCursorDisplayCounter()
        {
            for (var i = 0; i < CursorCounterSafetyLimit; i++)
            {
                if (ShowCursor(true) >= 0)
                    return;
            }
        }

        private IntPtr ChildCursorSubclassProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            IntPtr dwRefData)
        {
            if (msg == WM_SETCURSOR && ShouldForceHideMouseCursor())
            {
                SetCursor(IntPtr.Zero);
                return new IntPtr(1);
            }

            if ((msg == WM_MOUSEMOVE || msg == WM_NCMOUSEMOVE) && _isNativeCursorHidden)
            {
                DispatcherQueue.TryEnqueue(MarkPlaybackActivity);
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private delegate IntPtr SUBCLASSPROC(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            IntPtr dwRefData);

        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(
            IntPtr hWnd,
            SUBCLASSPROC pfnSubclass,
            UIntPtr uIdSubclass,
            IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool RemoveWindowSubclass(
            IntPtr hWnd,
            SUBCLASSPROC pfnSubclass,
            UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_S = 0x53;
        private const int GCLP_HCURSOR = -12;
        private const int CursorCounterSafetyLimit = 64;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_SETCURSOR = 0x0020;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_NCMOUSEMOVE = 0x00A0;
        private static readonly UIntPtr WINDOW_SUBCLASS_ID = new(1);
        private static readonly UIntPtr CHILD_CURSOR_SUBCLASS_ID = new(2);
        private static readonly IntPtr IDC_ARROW = new(32512);

        private void UpdateProgress(double position, double duration)
        {
            if (duration <= 0)
                return;

            // Cached so the A/B loop click handlers can capture the live timestamp
            // without having to plumb a separate position observer.
            _currentPlaybackPosition = position;

            var percent = Math.Clamp(position / duration * 100, 0, 100);
            _suppressSliderValueChanged = true;
            try
            {
                NativeProgressBar.Value = percent;
                CurrentTimeText.Text = PlaybackMetadataFormatter.FormatPlaybackTime(position);
                DurationText.Text = PlaybackMetadataFormatter.FormatPlaybackTime(duration);
            }
            finally
            {
                _suppressSliderValueChanged = false;
            }

            // Marker positions are percent-of-duration but the tooltip shows absolute
            // timestamps, so we need the duration to format times. Re-render markers
            // when the duration first becomes known so chapters reported before
            // duration was observed still get drawn.
            if (Math.Abs(_lastKnownDurationSeconds - duration) > 0.001)
            {
                _lastKnownDurationSeconds = duration;
                RebuildChapterMarkers();
                // A's percent-of-duration depends on duration, so both the loop
                // fill and the A/B markers need to refresh once duration is known.
                RefreshSeekBarFill();
                UpdateLoopMarkers();
            }

            // The gradient cutoff is A / current, so the value-fill needs a refresh
            // every time the playhead moves while A is set. UpdateProgress is the
            // hot path; the early-out for the no-A case keeps it cheap.
            if (_loopPointA.HasValue)
                RefreshSeekBarFill();
        }

        private void NativeProgressBar_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSliderValueChanged)
                return;

            // With B set, any seek past B is illegal in loop terms — mpv's ab-loop
            // only fires when playback *reaches* B during forward play, not when
            // the user clicks beyond it. Trap that case here and snap the playhead
            // back to A (or to file start when A isn't set), matching the rule
            // "clicking past B returns you to A".
            if (_loopPointB.HasValue && _lastKnownDurationSeconds > 0)
            {
                var bPercent = _loopPointB.Value / _lastKnownDurationSeconds * 100.0;
                if (e.NewValue > bPercent)
                {
                    var aTarget = _loopPointA ?? 0.0;
                    ForEachPlayerBackend(player => player.SeekToTime(aTarget));

                    // Snap the slider Value visually to A immediately so the thumb
                    // doesn't briefly display past B before mpv's progress event
                    // catches up. Suppressed so this assignment doesn't recurse.
                    var aPercent = Math.Clamp(aTarget / _lastKnownDurationSeconds * 100.0, 0.0, 100.0);
                    _suppressSliderValueChanged = true;
                    try
                    {
                        NativeProgressBar.Value = aPercent;
                    }
                    finally
                    {
                        _suppressSliderValueChanged = false;
                    }
                    return;
                }
            }

            SeekToPercent(e.NewValue);
        }

        private void Player_ChaptersChanged(object? sender, IReadOnlyList<MpvChapter> chapters)
        {
            _chapters = chapters ?? Array.Empty<MpvChapter>();
            RebuildChapterMarkers();
        }

        private void NativeProgressBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Chapter marker positions and the A/B icon positions are absolute
            // pixels relative to the slider, so any resize requires re-laying them
            // out. (The loop gradient is fraction-based and doesn't need a resize
            // refresh — it's already mapped onto the value-fill rectangle.)
            RebuildChapterMarkers();
            UpdateLoopMarkers();
        }

        private void NativeProgressBar_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_chapters.Count == 0 || _lastKnownDurationSeconds <= 0)
            {
                HideChapterTooltip();
                return;
            }

            var width = NativeProgressBar.ActualWidth;
            if (width <= 0)
                return;

            var pointerX = e.GetCurrentPoint(NativeProgressBar).Position.X;

            // Find the chapter whose marker is closest to the pointer, but only
            // commit to showing a tooltip when we are within a small px threshold so
            // the tooltip doesn't follow the cursor across the whole bar.
            var nearestIndex = -1;
            var nearestDistance = double.PositiveInfinity;
            for (var i = 0; i < _chapters.Count; i++)
            {
                var chapterX = ChapterPercent(_chapters[i]) / 100.0 * width;
                var distance = Math.Abs(pointerX - chapterX);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0 || nearestDistance > ChapterMarkerHoverThresholdPx)
            {
                HideChapterTooltip();
                return;
            }

            ShowChapterTooltip(nearestIndex, width);
        }

        private void NativeProgressBar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HideChapterTooltip();
        }

        private void RebuildChapterMarkers()
        {
            if (ChapterMarkerCanvas is null)
                return;

            ChapterMarkerCanvas.Children.Clear();
            HideChapterTooltip();

            if (_chapters.Count == 0)
                return;

            var width = NativeProgressBar.ActualWidth;
            if (width <= 0)
                return;

            // Subtle dark notch that contrasts against both the unfilled track
            // (mid-gray) and the filled value track (near-white / accent). Keep the
            // markers off the very edges so the slider thumb at value=0/100 isn't
            // visually fighting them.
            var markerBrush = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.55 };
            const double markerWidth = 2.0;
            const double markerHeight = 14.0;
            var canvasHeight = ChapterMarkerCanvas.ActualHeight > 0 ? ChapterMarkerCanvas.ActualHeight : 22.0;
            var top = (canvasHeight - markerHeight) / 2.0;

            foreach (var chapter in _chapters)
            {
                var percent = ChapterPercent(chapter);
                if (percent <= 0.001 || percent >= 99.999)
                    continue; // markers at the very ends are noise.

                var x = percent / 100.0 * width - markerWidth / 2.0;

                var tick = new Rectangle
                {
                    Width = markerWidth,
                    Height = markerHeight,
                    Fill = markerBrush,
                    RadiusX = 1,
                    RadiusY = 1,
                };
                Canvas.SetLeft(tick, x);
                Canvas.SetTop(tick, top);
                ChapterMarkerCanvas.Children.Add(tick);
            }
        }

        private double ChapterPercent(MpvChapter chapter)
        {
            if (_lastKnownDurationSeconds <= 0)
                return -1;

            return Math.Clamp(chapter.Time / _lastKnownDurationSeconds * 100.0, 0.0, 100.0);
        }

        private void ShowChapterTooltip(int chapterIndex, double sliderWidth)
        {
            if (chapterIndex < 0 || chapterIndex >= _chapters.Count)
                return;

            var chapter = _chapters[chapterIndex];
            var percent = ChapterPercent(chapter);
            if (percent < 0)
                return;

            // Only re-bind text content when the hovered chapter actually changes;
            // otherwise PointerMoved fires constantly and keeping the assignments
            // idempotent avoids unnecessary layout invalidation.
            if (_hoveredChapterIndex != chapterIndex)
            {
                _hoveredChapterIndex = chapterIndex;
                ChapterTooltipTitle.Text = PlaybackMetadataFormatter.GetChapterTitle(chapter, chapterIndex);
                ChapterTooltipTime.Text = PlaybackMetadataFormatter.FormatPlaybackTime(chapter.Time);
            }

            ChapterTooltip.Visibility = Visibility.Visible;

            // Position the tooltip horizontally over the marker, then nudge so it
            // doesn't clip past the slider's left/right edges.
            ChapterTooltip.UpdateLayout();
            var tooltipWidth = ChapterTooltip.ActualWidth;
            var markerX = percent / 100.0 * sliderWidth;
            var desiredLeft = markerX - tooltipWidth / 2.0;
            var maxLeft = Math.Max(0, sliderWidth - tooltipWidth);
            var clampedLeft = Math.Clamp(desiredLeft, 0, maxLeft);
            ChapterTooltipTransform.X = clampedLeft;
        }

        private void HideChapterTooltip()
        {
            if (_hoveredChapterIndex == -1 && ChapterTooltip.Visibility == Visibility.Collapsed)
                return;

            _hoveredChapterIndex = -1;
            ChapterTooltip.Visibility = Visibility.Collapsed;
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems) ||
                e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            string? pathToLoad = null;

            // First check for files
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    // For simplicity, just grab the first dropped file
                    pathToLoad = items[0].Path;
                }
            }
            // Fall back to text/URL
            else if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                pathToLoad = await e.DataView.GetTextAsync();
            }

            var normalizedPath = NormalizeMediaTarget(pathToLoad);
            if (!string.IsNullOrEmpty(normalizedPath))
                LoadMedia(normalizedPath);
        }

        private void InitializeInputRuntime()
        {
            var bindings = InputBindingSettingsStore.Load(InputBindingCatalog.CreateDefaults()).ToList();
            var portablePath = ScriptExtensionSettingsStore.PortableConfigPath;
            if (!string.IsNullOrWhiteSpace(portablePath) && Directory.Exists(portablePath))
            {
                try
                {
                    var imported = MpvPortableConfigImporter.Import(portablePath);
                    MpvRuntimeBootstrapSettings.Instance.ApplyImportedConfig(imported);
                    if (imported.InputBindings.Count > 0)
                        bindings = imported.InputBindings.ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to import portable config from '{portablePath}'. {ex}");
                }
            }

            _currentInputBindings.Clear();
            _currentInputBindings.AddRange(bindings);
            _inputRuntimeService.LoadBindings(_currentInputBindings);
        }

        private bool TryExecuteRuntimeInput(VirtualKey key)
        {
            var gesture = InputGesture.FromVirtualKey(
                key,
                IsModifierPressed(VirtualKey.Control),
                IsModifierPressed(VirtualKey.Shift),
                IsModifierPressed(VirtualKey.Menu));
            if (!_inputRuntimeService.TryGetCommand(gesture, out var command))
                return false;

            return ExecuteInputCommand(command);
        }

        private static bool IsModifierPressed(VirtualKey key)
        {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        }

        private bool ExecuteInputCommand(string command)
        {
            if (!MpvCommandLineParser.TryParseCommandChain(command, out var commandChain))
                return false;

            var handledAny = false;
            foreach (var argv in commandChain)
            {
                if (argv.Length == 0)
                    continue;

                if (TryExecuteNativeInputAlias(argv))
                {
                    handledAny = true;
                    continue;
                }

                var verb = argv[0];
                if (AethraCommandIds.IsAethraCommand(verb))
                {
                    handledAny = _commandDispatcher.Execute(verb) || handledAny;
                    continue;
                }

                if (InputCommandSupport.IsDeniedCommandVerb(verb))
                    return false;

                if (_activeBackends.Count == 0)
                    continue;

                ForEachPlayerBackend(player => player.ExecuteCommand(argv));
                handledAny = true;
            }

            return handledAny;
        }

        private bool TryExecuteNativeInputAlias(IReadOnlyList<string> argv)
        {
            if (!InputCommandSupport.TryGetNativeAlias(argv, out var alias))
                return false;

            switch (alias)
            {
                case InputCommandSupport.NativeAlias.TogglePlayPause:
                    return _commandDispatcher.Execute(AethraCommandIds.TogglePlayPause);

                case InputCommandSupport.NativeAlias.ToggleMute:
                    return _commandDispatcher.Execute(AethraCommandIds.ToggleMute);

                case InputCommandSupport.NativeAlias.ToggleFullscreen:
                    return _commandDispatcher.Execute(AethraCommandIds.ToggleFullscreen);

                case InputCommandSupport.NativeAlias.ExitFullscreen:
                    if (_isFullscreen)
                        ExitFullscreen();
                    return true;

                case InputCommandSupport.NativeAlias.ShowPlaylist:
                    return _commandDispatcher.Execute(AethraCommandIds.ShowPlaylist);

                case InputCommandSupport.NativeAlias.ToggleSettings:
                    return _commandDispatcher.Execute(AethraCommandIds.ToggleSettings);

                case InputCommandSupport.NativeAlias.Quit:
                    Close();
                    return true;

                default:
                    return false;
            }
        }

        private void HandleEscapeCommand()
        {
            if (FullSettingsHost.Visibility == Visibility.Visible)
            {
                CloseFullSettingsPanel();
                RefreshPlaybackActivityState();
                return;
            }

            if (RightDrawerHost.Visibility == Visibility.Visible)
            {
                CloseRightDrawer();
                return;
            }

            if (EmbeddedPanelHost.Visibility == Visibility.Visible)
            {
                EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                RefreshPlaybackActivityState();
                return;
            }

            if (_isFullscreen)
                ExitFullscreen();
        }

        private void ResetLoopPoints()
        {
            _loopPointA = null;
            _loopPointB = null;
            _playbackOptions.ApplyStringProperty("ab-loop-a", "no");
            _playbackOptions.ApplyStringProperty("ab-loop-b", "no");
            UpdateLoopButtonVisuals();
            RefreshSeekBarFill();
            UpdateLoopMarkers();
        }

        private void ApplyPersistedWindowState()
        {
            if (_playbackPersistence.WindowX is null
                || _playbackPersistence.WindowY is null
                || _playbackPersistence.WindowWidth is null
                || _playbackPersistence.WindowHeight is null)
            {
                return;
            }

            try
            {
                AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                    _playbackPersistence.WindowX.Value,
                    _playbackPersistence.WindowY.Value,
                    _playbackPersistence.WindowWidth.Value,
                    _playbackPersistence.WindowHeight.Value));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to restore window geometry. {ex}");
            }
        }

        private void TryLoadStartupMedia()
        {
            if (_startupMediaLoaded)
                return;

            var startupPath = ResolveStartupMediaCandidate(PreferredStartupMediaPath, _lastLoadedMediaPath, out var shouldResumePersistedPosition);
            if (string.IsNullOrWhiteSpace(startupPath))
                return;

            _startupMediaLoaded = true;
            LoadMedia(startupPath);
            if (shouldResumePersistedPosition && _playbackPersistence.LastPositionSeconds > 0)
            {
                ForEachPlayerBackend(player => player.SeekToTime(_playbackPersistence.LastPositionSeconds));
            }
        }

        internal static string? ResolveStartupMediaCandidate(string preferredPath, string? persistedPath, out bool shouldResumePersistedPosition)
        {
            shouldResumePersistedPosition = false;
            var normalizedPreferredPath = NormalizeMediaTarget(preferredPath);
            var normalizedPersistedPath = NormalizeMediaTarget(persistedPath);

            if (IsPlayableMediaTarget(normalizedPreferredPath))
                return normalizedPreferredPath;

            if (IsPlayableMediaTarget(normalizedPersistedPath))
            {
                shouldResumePersistedPosition = true;
                return normalizedPersistedPath;
            }

            return null;
        }

        internal static string? NormalizeMediaTarget(string? rawTarget)
        {
            if (string.IsNullOrWhiteSpace(rawTarget))
                return null;

            return rawTarget.Trim();
        }

        internal static bool IsPlayableMediaTarget(string? pathOrUri)
        {
            if (string.IsNullOrWhiteSpace(pathOrUri))
                return false;

            if (File.Exists(pathOrUri))
                return true;

            if (!Uri.TryCreate(pathOrUri, UriKind.Absolute, out var uri))
                return false;

            if (uri.IsFile)
                return File.Exists(uri.LocalPath);

            return uri.Scheme is "http" or "https" or "rtsp";
        }

        private bool HandleLegacyKeyDown(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Space:
                    return _commandDispatcher.Execute(AethraCommandIds.BossKey);
                case VirtualKey.Right:
                    SeekRelative(10);
                    return true;
                case VirtualKey.Left:
                    SeekRelative(-10);
                    return true;
                case VirtualKey.Up:
                    AddVolume(5);
                    return true;
                case VirtualKey.Down:
                    AddVolume(-5);
                    return true;
                case VirtualKey.F:
                    ToggleFullscreen();
                    return true;
                case VirtualKey.S:
                    ToggleSettingsPanel();
                    return true;
                case VirtualKey.Escape:
                    HandleEscapeCommand();
                    return true;
                default:
                    return false;
            }
        }

        private void RegisterPlayerBackend(INativeMpvPlayerBackend player)
        {
            if (_activeBackends.Contains(player))
                return;

            _activeBackends.Add(player);
            ApplyPersistedPreferencesToRuntime();
            if (ShouldAutoplayOnOpen() && !string.IsNullOrWhiteSpace(_lastLoadedMediaPath))
            {
                _autoplayReassertTimer.Stop();
                _autoplayReassertTimer.Start();
            }
        }

        private void UnregisterPlayerBackend(INativeMpvPlayerBackend? player)
        {
            if (player is null)
                return;

            _activeBackends.Remove(player);
        }

        private void ForEachPlayerBackend(Action<INativeMpvPlayerBackend> action)
        {
            foreach (var player in _activeBackends)
                action(player);
        }

        private void ApplyPersistedPreferencesToRuntime()
        {
            if (_persistedPreferencesAppliedToRuntime || _activeBackends.Count == 0)
                return;

            var profiles = PreferencesProfilesStore.Load();
            _playbackOptions.ApplyPlaybackPreferences(profiles.Playback);
            _playbackOptions.ApplyVideoPreferences(profiles.Video);
            _playbackOptions.ApplyVideoEnhancementPreferences(profiles.Video);
            _playbackOptions.ApplyAudioPreferences(profiles.Audio);
            _playbackOptions.ApplySubtitlePreferences(profiles.Subtitles);
            _playbackOptions.ApplyAdvancedPreferences(profiles.Advanced);
            _playbackOptions.ApplyNetworkPreferences(profiles.Network);
            _playbackOptions.ApplyCustomizationPreferences(profiles.Customization);
            _persistedPreferencesAppliedToRuntime = true;
        }

        private static bool ShouldRememberRecentFiles()
        {
            try
            {
                return PreferencesProfilesStore.Load().Library.RememberRecentFiles;
            }
            catch
            {
                return true;
            }
        }

        private static bool ShouldAutoplayOnOpen()
        {
            try
            {
                return PreferencesProfilesStore.Load().Playback.AutoplayOnOpen;
            }
            catch
            {
                return true;
            }
        }
    }
}
