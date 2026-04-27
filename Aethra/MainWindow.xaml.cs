using Aethra.Commands;
using Aethra.Native;
using System;
using System.Collections.Generic;
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
        private readonly SUBCLASSPROC _childCursorSubclassProc;
        private readonly HashSet<IntPtr> _hookedCursorChildHwnds = new();
        private readonly Dictionary<IntPtr, IntPtr> _originalClassCursors = new();
        private readonly Dictionary<string, double> _pendingVideoAdjustments = new(StringComparer.Ordinal);
        private readonly DispatcherTimer _videoAdjustmentFlushTimer;
        private readonly DispatcherTimer _cursorHideEnforcementTimer;
        private bool _useGpuVideoSurface = true;
        private static bool RunGpuSurfaceSmoke =>
            string.Equals(Environment.GetEnvironmentVariable("AETHRA_GPU_SURFACE_SMOKE"), "1", StringComparison.Ordinal);
        private IntPtr _mainHwnd;
        private bool _suppressSliderValueChanged;
        private bool _visiblePlayerInitialized;
        private bool _isFullscreen;
        private bool _wasMaximizedBeforeFullscreen;
        private bool _isCommandRailExpanded;
        private readonly PlaybackActivityController _playbackActivity;
        // True means playback is paused. Visual surfaces route through
        // PlayPauseVisualFor so the transport button and context menu stay aligned.
        private bool _isPlaybackPaused = true;
        private bool _isPointerOverTransportBar;
        private bool _isVideoContextFlyoutOpen;
        private bool _isNativeCursorHidden;
        private Windows.Foundation.Point _lastRootPointerPosition;
        private bool _hasLastRootPointerPosition;
        private Windows.Foundation.Point _videoPointerPressedAt;
        private NativePoint _videoWindowDragStartCursorPosition;
        private Windows.Graphics.PointInt32 _videoWindowDragStartWindowPosition;
        private uint _videoPointerId;
        private bool _isVideoPointerPressPending;
        private bool _isVideoPointerDraggingWindow;
        private const double CommandRailCollapsedWidth = 64;
        private const double CommandRailExpandedWidth = 252;
        private const double TransportBarHeight = 70;
        private const double WindowDragThreshold = 6;

        public MainWindow()
        {
            InitializeComponent();
            ApplyPlayPauseVisualState();
            CommandRail.Loaded += CommandRail_Loaded;
            SetCommandRailExpanded(false);
            _playbackActivity = new PlaybackActivityController(TimeSpan.FromSeconds(1), CanLetPlaybackChromeIdle);
            _playbackActivity.ModeChanged += PlaybackActivity_ModeChanged;
            _videoAdjustmentFlushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _videoAdjustmentFlushTimer.Tick += VideoAdjustmentFlushTimer_Tick;
            _cursorHideEnforcementTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _cursorHideEnforcementTimer.Tick += CursorHideEnforcementTimer_Tick;
            _commandDispatcher = new AethraCommandDispatcher(new AethraCommandContext(PausePlayback, MinimizeWindow));
            _windowSubclassProc = WindowSubclassProc;
            _childCursorSubclassProc = ChildCursorSubclassProc;
            this.Activated += MainWindow_Activated;
            this.Activated += MainWindow_CursorActivationChanged;
            this.Closed += MainWindow_Closed;
            ApplyPlaybackActivityState();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _videoAdjustmentFlushTimer.Stop();
            _cursorHideEnforcementTimer.Stop();
            _playbackActivity.Stop();
            RootGrid.SetCursorVisible(true);
            VideoContainer.SetCursorVisible(true);
            ShowNativeCursorIfHidden();
            _gpuSurfaceSmokeHost?.Dispose();
            _gpuPlayer?.Dispose();
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
                        MarkPlaybackActivity();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Right:
                        SeekRelative(10);
                        MarkPlaybackActivity();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Left:
                        SeekRelative(-10);
                        MarkPlaybackActivity();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Up:
                        AddVolume(5);
                        MarkPlaybackActivity();
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.Down:
                        AddVolume(-5);
                        MarkPlaybackActivity();
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
                            RefreshPlaybackActivityState();
                        }
                        else if (RightDrawerHost.Visibility == Visibility.Visible)
                        {
                            CloseRightDrawer();
                        }
                        else if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                        {
                            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                            RefreshPlaybackActivityState();
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
            var shouldShow = FullSettingsHost.Visibility != Visibility.Visible;
            FullSettingsHost.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;

            if (shouldShow)
            {
                EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                CloseRightDrawer(updateCursor: false);
            }

            RefreshPlaybackActivityState();
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
            EmbeddedPanelHost.Visibility = Visibility.Collapsed;
            CloseRightDrawer(updateCursor: false);
            FullSettingsHost.Visibility = Visibility.Visible;
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
            EmbeddedPanelHost.Margin = new Thickness(CommandRail.Width, 40, 0, TransportBarHeight);
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
                        RefreshPlaybackActivityState();
                    }
                    else if (RightDrawerHost.Visibility == Visibility.Visible)
                    {
                        CloseRightDrawer();
                    }
                    else if (EmbeddedPanelHost.Visibility == Visibility.Visible)
                    {
                        EmbeddedPanelHost.Visibility = Visibility.Collapsed;
                        RefreshPlaybackActivityState();
                    }
                    else if (_isFullscreen)
                    {
                        ExitFullscreen();
                    }
                });
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void FullSettings_CloseRequested(object? sender, EventArgs e)
        {
            FullSettingsHost.Visibility = Visibility.Collapsed;
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
            _pendingVideoAdjustments[e.MpvProperty] = e.Value;

            if (!_videoAdjustmentFlushTimer.IsEnabled)
                _videoAdjustmentFlushTimer.Start();
        }

        private void VideoAdjustmentFlushTimer_Tick(object? sender, object e)
        {
            if (_pendingVideoAdjustments.Count == 0)
            {
                _videoAdjustmentFlushTimer.Stop();
                return;
            }

            var adjustments = _pendingVideoAdjustments.ToArray();
            _pendingVideoAdjustments.Clear();

            foreach (var adjustment in adjustments)
                ApplyVideoAdjustment(adjustment.Key, adjustment.Value);
        }

        private void ApplyVideoAdjustment(string mpvProperty, double value)
        {
            _gpuPlayer?.SetProperty(mpvProperty, value);
            _softwarePlayer?.SetProperty(mpvProperty, value);
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
            CommandRail.Visibility = Visibility.Visible;
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
        }

        private void InitializeNativeGpuPlayer()
        {
            _gpuPlayer = new NativeMpvOpenGlPlayer(DispatcherQueue, GpuVideoSurface, GpuPlayer_Failed);
            _gpuPlayer.ProgressChanged += Player_ProgressChanged;
            _gpuPlayer.PlaybackPausedChanged += Player_PlaybackPausedChanged;
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

        private void ContextPlayPauseItem_Click(object sender, RoutedEventArgs e) => TogglePlayback();

        private void ContextSeekBackItem_Click(object sender, RoutedEventArgs e) => SeekRelative(-10);

        private void ContextSeekForwardItem_Click(object sender, RoutedEventArgs e) => SeekRelative(30);

        private void ContextFullscreenItem_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        private void ContextOpenFileItem_Click(object sender, RoutedEventArgs e) => RailOpenFileButton_Click(sender, e);

        private void ContextOpenFolderItem_Click(object sender, RoutedEventArgs e) => RailOpenFolderButton_Click(sender, e);

        private void ContextRecentItem_Click(object sender, RoutedEventArgs e) => RailRecentButton_Click(sender, e);

        private void ContextSettingsItem_Click(object sender, RoutedEventArgs e) => ToggleSettingsPanel();

        private void Player_ProgressChanged(object? sender, NativeMpvPlaybackProgress progress)
        {
            UpdateProgress(progress.Position, progress.Duration);
        }

        private void Player_PlaybackPausedChanged(object? sender, bool isPaused)
        {
            _isPlaybackPaused = isPaused;
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
            _gpuPlayer?.TogglePause();
            _softwarePlayer?.TogglePause();
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
        }

        private void PausePlayback()
        {
            _isPlaybackPaused = true;
            _gpuPlayer?.Pause();
            _softwarePlayer?.Pause();
            ApplyPlayPauseVisualState();
            RefreshPlaybackActivityState();
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
            RefreshPlaybackActivityState();
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
            TopChrome.Padding = new Thickness(0, 0, this.AppWindow.TitleBar.RightInset, 0);
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
