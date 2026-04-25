using Aethra.Commands;
using Aethra.Native;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace Aethra
{
    public sealed partial class MainWindow : Window
    {
        private NativeMpvSoftwarePlayer? _softwarePlayer;
        private NativeMpvOpenGlPlayer? _gpuPlayer;
        private D3D11SwapChainPanelHost? _gpuSurfaceSmokeHost;
        private readonly AethraCommandDispatcher _commandDispatcher;
        private readonly SUBCLASSPROC _windowSubclassProc;
        private bool _useGpuVideoSurface = true;
        private static bool RunGpuSurfaceSmoke =>
            string.Equals(Environment.GetEnvironmentVariable("AETHRA_GPU_SURFACE_SMOKE"), "1", StringComparison.Ordinal);
        private IntPtr _mainHwnd;
        private bool _suppressSliderValueChanged;
        private bool _visiblePlayerInitialized;
        private bool _isFullscreen;
        private bool _wasMaximizedBeforeFullscreen;
        private bool _isCommandRailExpanded;
        private readonly DispatcherTimer _fullscreenControlsIdleTimer;
        private Windows.Foundation.Point _lastFullscreenPointerPosition;
        private bool _hasLastFullscreenPointerPosition;
        private DateTime _fullscreenControlsHiddenAtUtc = DateTime.MinValue;
        private bool _isPlaybackPaused = true;
        private const double CommandRailCollapsedWidth = 64;
        private const double CommandRailExpandedWidth = 252;

        public MainWindow()
        {
            InitializeComponent();
            ApplyPlayPauseVisualState();
            CommandRail.Loaded += CommandRail_Loaded;
            SetCommandRailExpanded(false);
            _fullscreenControlsIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fullscreenControlsIdleTimer.Tick += FullscreenControlsIdleTimer_Tick;
            _commandDispatcher = new AethraCommandDispatcher(new AethraCommandContext(PausePlayback, MinimizeWindow));
            _windowSubclassProc = WindowSubclassProc;
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            UpdateCursorVisibility();
            _gpuSurfaceSmokeHost?.Dispose();
            _gpuPlayer?.Dispose();
            _softwarePlayer?.Dispose();

            if (_mainHwnd != IntPtr.Zero)
            {
                RemoveWindowSubclass(_mainHwnd, _windowSubclassProc, WINDOW_SUBCLASS_ID);
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            // Make sure the main element can receive focus and keys
            this.Content.IsTabStop = true;
            this.Content.Focus(FocusState.Programmatic);

            this.Content.KeyDown += (s, e) =>
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.Space:
                        _commandDispatcher.Execute(AethraCommandIds.BossKey);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Right:
                        SeekRelative(10);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Left:
                        SeekRelative(-10);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Up:
                        AddVolume(5);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Down:
                        AddVolume(-5);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.F:
                        ToggleFullscreen();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.S:
                        ToggleSettingsPanel();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Escape:
                        if (FullSettingsHost.Visibility == Visibility.Visible)
                        {
                            FullSettingsHost.Visibility = Visibility.Collapsed;
                            UpdateCursorVisibility();
                        }
                        else if (SettingsHost.Visibility == Visibility.Visible)
                        {
                            SettingsHost.Visibility = Visibility.Collapsed;
                            UpdateCursorVisibility();
                        }
                        else if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                        {
                            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                            UpdateCursorVisibility();
                        }
                        else if (_isFullscreen)
                        {
                            ExitFullscreen();
                        }
                        e.Handled = true;
                        break;
                }
            };

            this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            ApplyTitleBarInsets();
            EnsureWindowMessageHook();

            this.AppWindow.Changed += (s, e) =>
            {
                ApplyTitleBarInsets();

            };

            // Wait for the UI element to finish loading before initializing mpv
            VideoContainer.Loaded += VideoContainer_Loaded;
            GpuVideoSurface.Loaded += GpuVideoSurface_Loaded;

            // Keep keyboard focus on the main window.
            this.Activate();
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

            _ = DispatcherQueue.TryEnqueue(TryInitializeVisiblePlayer);
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
                    {
                        _ = DispatcherQueue.TryEnqueue(TryInitializeVisiblePlayer);
                        return;
                    }

                    InitializeNativeGpuPlayer();
                    _visiblePlayerInitialized = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GPU renderer startup failed. Falling back to software rendering. {ex}");
                _gpuSurfaceSmokeHost?.Dispose();
                _gpuSurfaceSmokeHost = null;
                _gpuPlayer?.Dispose();
                _gpuPlayer = null;
            }

            _useGpuVideoSurface = false;
            ApplyVideoSurfaceMode();
            InitializeNativeSoftwarePlayer();
            _visiblePlayerInitialized = true;
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
            var shouldShow = SettingsHost.Visibility != Visibility.Visible;
            SettingsHost.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;

            if (shouldShow)
                EmbeddedPanelHost.Visibility = Visibility.Collapsed;

            UpdateCursorVisibility();
        }

        private void CommandRail_Loaded(object sender, RoutedEventArgs e)
        {
            CommandRail.Loaded -= CommandRail_Loaded;
            SetCommandRailExpanded(false);
        }

        private void RailToggleButton_Click(object sender, RoutedEventArgs e)
        {
            SetCommandRailExpanded(!_isCommandRailExpanded);
        }

        private void SetCommandRailExpanded(bool expanded)
        {
            _isCommandRailExpanded = expanded;
            CommandRail.Width = expanded ? CommandRailExpandedWidth : CommandRailCollapsedWidth;
            RailToggleIcon.Glyph = expanded ? "\uE70E" : "\uE700";
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
            SettingsHost.Visibility = Visibility.Collapsed;
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            FullSettingsHost.Visibility = Visibility.Visible;
            UpdateCursorVisibility();
        }

        private void RailConverterButton_Click(object sender, RoutedEventArgs e)
        {
            ShowEmbeddedPanel("Tools", "Media converter", "Conversion tools.");
        }

        private void CloseEmbeddedPanelButton_Click(object sender, RoutedEventArgs e)
        {
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            UpdateCursorVisibility();
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
            UpdateCursorVisibility();
        }

        private void UpdateEmbeddedPanelOffset()
        {
            EmbeddedPanelHost.Margin = new Thickness(CommandRail.Width, 40, 0, 156);
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
        }

        private IntPtr WindowSubclassProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            IntPtr dwRefData)
        {
            if (msg == WM_KEYDOWN)
            {
                var vk = (int)wParam;
                if (vk == 0x46) // F
                {
                    DispatcherQueue.TryEnqueue(ToggleFullscreen);
                    return IntPtr.Zero;
                }
            }

            if (msg == WM_KEYDOWN && wParam == (IntPtr)VK_S)
            {
                DispatcherQueue.TryEnqueue(ToggleSettingsPanel);
                return IntPtr.Zero;
            }

            if (msg == WM_KEYDOWN && wParam == (IntPtr)VK_ESCAPE)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (FullSettingsHost.Visibility == Visibility.Visible)
                    {
                        FullSettingsHost.Visibility = Visibility.Collapsed;
                        UpdateCursorVisibility();
                    }
                    else if (SettingsHost.Visibility == Visibility.Visible)
                    {
                        SettingsHost.Visibility = Visibility.Collapsed;
                        UpdateCursorVisibility();
                    }
                    else if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                    {
                        EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                        UpdateCursorVisibility();
                    }
                    else if (_isFullscreen)
                    {
                        ExitFullscreen();
                    }
                });
                return IntPtr.Zero;
            }

            if (msg == WM_RBUTTONUP)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (FullSettingsHost.Visibility == Visibility.Visible)
                        return;
                    ToggleSettingsPanel();
                });
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void Settings_CloseRequested(object? sender, EventArgs e)
        {
            SettingsHost.Visibility = Visibility.Collapsed;
            UpdateCursorVisibility();
        }

        private void Settings_OpenAllSettingsRequested(object? sender, EventArgs e)
        {
            SettingsHost.Visibility = Visibility.Collapsed;
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            FullSettingsHost.Visibility = Visibility.Visible;
            UpdateCursorVisibility();
        }

        private void FullSettings_CloseRequested(object? sender, EventArgs e)
        {
            FullSettingsHost.Visibility = Visibility.Collapsed;
            UpdateCursorVisibility();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsPanel();
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsPanel();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            else if (SettingsHost.Visibility == Visibility.Visible)
                SettingsHost.Visibility = Visibility.Collapsed;

            UpdateCursorVisibility();
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
            _hasLastFullscreenPointerPosition = false;
            TopChrome.Visibility = Visibility.Collapsed;
            CommandRail.Visibility = Visibility.Collapsed;
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            SettingsHost.Visibility = Visibility.Collapsed;
            FullSettingsHost.Visibility = Visibility.Collapsed;
            ShowFullscreenControls();
            this.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        private void ExitFullscreen()
        {
            _fullscreenControlsIdleTimer.Stop();
            _isFullscreen = false;
            this.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (_wasMaximizedBeforeFullscreen
                && this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            TopChrome.Visibility = Visibility.Visible;
            CommandRail.Visibility = Visibility.Visible;
            TransportBar.Visibility = Visibility.Visible;
            UpdateEmbeddedPanelOffset();
            ApplyTitleBarInsets();
            UpdateCursorVisibility();
        }

        private void InitializeNativeSoftwarePlayer()
        {
            _softwarePlayer = new NativeMpvSoftwarePlayer(
                DispatcherQueue,
                bitmap => VideoFrame.Source = bitmap);
            _softwarePlayer.ProgressChanged += Player_ProgressChanged;
        }

        private void InitializeNativeGpuPlayer()
        {
            _gpuPlayer = new NativeMpvOpenGlPlayer(DispatcherQueue, GpuVideoSurface, GpuPlayer_Failed);
            _gpuPlayer.ProgressChanged += Player_ProgressChanged;
            GpuVideoSurface.SizeChanged += GpuVideoSurface_SizeChanged;
        }

        private void GpuPlayer_Failed(Exception ex)
        {
            Debug.WriteLine($"GPU renderer task failed. Falling back to software rendering. {ex}");

            GpuVideoSurface.SizeChanged -= GpuVideoSurface_SizeChanged;
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
                return;

            TogglePlayback();
            e.Handled = true;
        }

        private void Player_ProgressChanged(object? sender, NativeMpvPlaybackProgress progress)
        {
            UpdateProgress(progress.Position, progress.Duration);
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
            _gpuPlayer?.TogglePause();
            _softwarePlayer?.TogglePause();
            ApplyPlayPauseVisualState();
        }

        private void PausePlayback()
        {
            _isPlaybackPaused = true;
            _gpuPlayer?.Pause();
            _softwarePlayer?.Pause();
            ApplyPlayPauseVisualState();
        }

        private void SeekRelative(double seconds)
        {
            _gpuPlayer?.Seek(seconds);
            _softwarePlayer?.Seek(seconds);
        }

        private void SeekToPercent(double percent)
        {
            _gpuPlayer?.SeekToPercent(percent);
            _softwarePlayer?.SeekToPercent(percent);
        }

        private void AddVolume(int amount)
        {
            _gpuPlayer?.AddVolume(amount);
            _softwarePlayer?.AddVolume(amount);
        }

        private void MinimizeWindow()
        {
            if (_isFullscreen)
                ExitFullscreen();

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Minimize();
        }

        private void LoadMedia(string path)
        {
            MediaTitleText.Text = GetDisplayMediaName(path);
            _gpuPlayer?.LoadFile(path);
            _softwarePlayer?.LoadFile(path);
            _isPlaybackPaused = false;
            ApplyPlayPauseVisualState();
        }

        private void ApplyPlayPauseVisualState()
        {
            if (_isPlaybackPaused)
            {
                PlayPauseIcon.Glyph = "\uE769";
                PlayPauseButton.BorderBrush = (Brush)Application.Current.Resources["AethraAccentBrush"];
            }
            else
            {
                PlayPauseIcon.Glyph = "\uE768";
                PlayPauseButton.BorderBrush = (Brush)Application.Current.Resources["AethraVideoBrush"];
            }
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
            TopChrome.Padding = new Thickness(0, 0, this.AppWindow.TitleBar.RightInset, 0);
        }

        private void RootGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(RootGrid);
            if (point.Properties.IsRightButtonPressed)
            {
                if (FullSettingsHost.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    return;
                }

                ToggleSettingsPanel();
                e.Handled = true;
            }
        }

        private void RootGrid_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_isFullscreen)
                return;

            var position = e.GetCurrentPoint(RootGrid).Position;
            var justHidden = DateTime.UtcNow - _fullscreenControlsHiddenAtUtc < TimeSpan.FromMilliseconds(250);

            if (!_hasLastFullscreenPointerPosition)
            {
                _lastFullscreenPointerPosition = position;
                _hasLastFullscreenPointerPosition = true;

                if (justHidden)
                    return;
            }
            else
            {
                var xDelta = Math.Abs(position.X - _lastFullscreenPointerPosition.X);
                var yDelta = Math.Abs(position.Y - _lastFullscreenPointerPosition.Y);

                if (xDelta < 2 && yDelta < 2)
                    return;

                _lastFullscreenPointerPosition = position;
            }

            ShowFullscreenControls();
        }

        private void FullscreenControlsIdleTimer_Tick(object? sender, object e)
        {
            _fullscreenControlsIdleTimer.Stop();

            if (_isFullscreen)
            {
                _fullscreenControlsHiddenAtUtc = DateTime.UtcNow;
                TransportBar.Visibility = Visibility.Collapsed;
                UpdateCursorVisibility();
            }
        }

        private void ShowFullscreenControls()
        {
            if (!_isFullscreen)
                return;

            TransportBar.Visibility = Visibility.Visible;
            UpdateCursorVisibility();
            _fullscreenControlsIdleTimer.Stop();
            _fullscreenControlsIdleTimer.Start();
        }

        // Single source of truth for cursor visibility. Called whenever any state
        // that affects whether the cursor should be visible changes (fullscreen
        // toggle, transport bar visibility, settings overlay visibility, pointer
        // activity). The cursor hides only when fullscreen, idle (transport bar
        // collapsed), and no overlay is visible.
        private bool ShouldForceHideMouseCursor()
        {
            return _isFullscreen
                && TransportBar.Visibility == Visibility.Collapsed
                && EmbeddedPanelHost.Visibility != Visibility.Visible
                && SettingsHost.Visibility != Visibility.Visible
                && FullSettingsHost.Visibility != Visibility.Visible;
        }

        private void UpdateCursorVisibility()
        {
            var shouldHide = ShouldForceHideMouseCursor();
            RootGrid.SetCursorVisible(!shouldHide);
            VideoContainer.SetCursorVisible(!shouldHide);
        }

        private delegate IntPtr SUBCLASSPROC(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            IntPtr dwRefData);

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

        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_S = 0x53;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_RBUTTONUP = 0x0205;
        private static readonly UIntPtr WINDOW_SUBCLASS_ID = new(1);

        private void UpdateProgress(double position, double duration)
        {
            if (duration <= 0)
                return;

            var percent = Math.Clamp(position / duration * 100, 0, 100);
            _suppressSliderValueChanged = true;
            try
            {
                NativeProgressBar.Value = percent;
                CurrentTimeText.Text = FormatTime(position);
                DurationText.Text = FormatTime(duration);
            }
            finally
            {
                _suppressSliderValueChanged = false;
            }
        }

        private void NativeProgressBar_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSliderValueChanged)
                return;

            SeekToPercent(e.NewValue);
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return "0:00";

            var time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
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

            if (!string.IsNullOrEmpty(pathToLoad))
            {
                LoadMedia(pathToLoad);
            }
        }
    }

}
