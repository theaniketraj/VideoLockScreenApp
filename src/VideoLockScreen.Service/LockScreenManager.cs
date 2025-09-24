using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Utilities;
using WpfApplication = System.Windows.Application;
using MonitorConfig = VideoLockScreen.Core.Models.MonitorConfiguration;

namespace VideoLockScreen.Service
{
    /// <summary>
    /// Manages the video lock screen overlay windows with enhanced system integration
    /// </summary>
    public class LockScreenManager
    {
        private readonly ILogger<LockScreenManager> _logger;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly IVideoSynchronizationService _videoSynchronizationService;
        private readonly ISystemIntegrationService _systemIntegrationService;
        private readonly SystemHelper _systemHelper;
        private readonly MonitorManager _monitorManager;
        private readonly List<LockScreenWindow> _overlayWindows = new();
        private bool _isLockScreenVisible = false;
        private int _currentLoopCount = 0;
        private int _maxLoopCount = -1;

        public LockScreenManager(
            ILogger<LockScreenManager> logger,
            IVideoPlayerService videoPlayerService,
            IVideoSynchronizationService videoSynchronizationService,
            ISystemIntegrationService systemIntegrationService,
            SystemHelper systemHelper,
            MonitorManager monitorManager)
        {
            _logger = logger;
            _videoPlayerService = videoPlayerService;
            _videoSynchronizationService = videoSynchronizationService;
            _systemIntegrationService = systemIntegrationService;
            _systemHelper = systemHelper;
            _monitorManager = monitorManager;

            // Subscribe to video player events
            _videoPlayerService.PlaybackEnded += OnVideoPlaybackEnded;
            _videoPlayerService.PlaybackError += OnVideoPlaybackError;
            
            // Subscribe to synchronization events
            _videoSynchronizationService.SynchronizationStatusChanged += OnSynchronizationStatusChanged;
            
            // Subscribe to system security events
            _systemIntegrationService.SecurityStateChanged += OnSecurityStateChanged;
        }

        /// <summary>
        /// Shows the video lock screen on all or primary monitor(s)
        /// </summary>
        /// <param name="settings">Lock screen settings</param>
        public async Task ShowLockScreenAsync(VideoLockScreenSettings settings)
        {
            try
            {
                if (_isLockScreenVisible)
                {
                    _logger.LogDebug("Lock screen already visible");
                    return;
                }

                _logger.LogInformation("Showing video lock screen");

                await WpfApplication.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // Get monitor information
                    var monitors = _systemHelper.GetMonitorInfo();
                    var monitorsToUse = settings.ShowOnAllMonitors ? monitors : monitors.Where(m => m.IsPrimary).ToList();

                    if (!monitorsToUse.Any())
                    {
                        _logger.LogWarning("No monitors found to display lock screen");
                        return;
                    }

                    // Load the video
                    if (!await _videoPlayerService.LoadVideoAsync(settings.VideoFilePath))
                    {
                        _logger.LogError("Failed to load video for lock screen: {VideoFilePath}", settings.VideoFilePath);
                        return;
                    }

                    // Create overlay windows for each monitor
                    foreach (var monitor in monitorsToUse)
                    {
                        var overlayWindow = new LockScreenWindow(_logger, _videoPlayerService, monitor, settings);
                        _overlayWindows.Add(overlayWindow);
                        overlayWindow.Show();
                    }

                    // Configure loop settings
                    _maxLoopCount = settings.LoopCount;
                    _currentLoopCount = 0;

                    // Start video playback
                    _videoPlayerService.Play();
                    _isLockScreenVisible = true;

                    _logger.LogInformation("Video lock screen displayed on {MonitorCount} monitor(s)", monitorsToUse.Count());
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing video lock screen");
                await HideLockScreenAsync(); // Cleanup on error
            }
        }

        /// <summary>
        /// Hides the video lock screen
        /// </summary>
        public async Task HideLockScreenAsync()
        {
            try
            {
                if (!_isLockScreenVisible)
                {
                    return;
                }

                _logger.LogInformation("Hiding video lock screen");

                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Stop video playback
                    _videoPlayerService.Stop();

                    // Close all overlay windows
                    foreach (var window in _overlayWindows)
                    {
                        try
                        {
                            window.Close();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error closing overlay window");
                        }
                    }

                    _overlayWindows.Clear();
                    _isLockScreenVisible = false;
                    _currentLoopCount = 0;

                    _logger.LogInformation("Video lock screen hidden");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding video lock screen");
            }
        }

        /// <summary>
        /// Handles video playback ended event
        /// </summary>
        private async void OnVideoPlaybackEnded(object? sender, EventArgs e)
        {
            try
            {
                _currentLoopCount++;
                _logger.LogDebug("Video playback ended, loop count: {CurrentLoop}/{MaxLoop}", 
                    _currentLoopCount, _maxLoopCount == -1 ? "∞" : _maxLoopCount.ToString());

                // Check if we should continue looping
                if (_maxLoopCount == -1 || _currentLoopCount < _maxLoopCount)
                {
                    // Restart video
                    _videoPlayerService.Seek(TimeSpan.Zero);
                    _videoPlayerService.Play();
                }
                else
                {
                    // Max loops reached, hide lock screen
                    _logger.LogInformation("Maximum loop count reached, hiding lock screen");
                    await HideLockScreenAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video playback ended");
            }
        }

        /// <summary>
        /// Handles video playback error event
        /// </summary>
        private async void OnVideoPlaybackError(object? sender, VideoErrorEventArgs e)
        {
            _logger.LogError("Video playback error: {Message}", e.Message);
            
            try
            {
                // Hide lock screen on video error
                await HideLockScreenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video playback error");
            }
        }

        /// <summary>
        /// Handles video synchronization status changes
        /// </summary>
        private void OnSynchronizationStatusChanged(object? sender, SynchronizationEventArgs e)
        {
            _logger.LogInformation("Video synchronization status changed: {Status} on {MonitorCount} monitors", 
                e.Status, e.MonitorCount);

            switch (e.Status)
            {
                case SynchronizationStatus.Lost:
                    _logger.LogWarning("Video synchronization lost - attempting recovery");
                    break;
                case SynchronizationStatus.Started:
                    _logger.LogInformation("Video synchronization started successfully");
                    break;
                case SynchronizationStatus.Stopped:
                    _logger.LogInformation("Video synchronization stopped");
                    break;
            }
        }

        /// <summary>
        /// Handles system security state changes
        /// </summary>
        private void OnSecurityStateChanged(object? sender, SystemSecurityEventArgs e)
        {
            _logger.LogInformation("System security level changed: {SecurityLevel} (Enabled: {IsEnabled})", 
                e.SecurityLevel, e.IsEnabled);

            if (!string.IsNullOrEmpty(e.Features))
            {
                _logger.LogDebug("Security features: {Features}", e.Features);
            }
        }

        /// <summary>
        /// Shows enhanced lock screen with system integration
        /// </summary>
        public async Task ShowEnhancedLockScreenAsync(VideoLockScreenSettings settings, bool enableKioskMode = false)
        {
            try
            {
                _logger.LogInformation("Showing enhanced lock screen (Kiosk Mode: {KioskMode})", enableKioskMode);

                // Enable system integration if kiosk mode is requested
                if (enableKioskMode)
                {
                    await _systemIntegrationService.EnableKioskModeAsync();
                }

                // Get optimized monitor configurations
                var monitors = _systemHelper.GetMonitorInfo();
                var configurations = new List<MonitorConfig>();

                foreach (var monitor in monitors)
                {
                    var config = _monitorManager.CreateOptimizedConfiguration(monitor, settings);
                    if (_monitorManager.ValidateConfiguration(config))
                    {
                        configurations.Add(config);
                    }
                }

                if (configurations.Count == 0)
                {
                    _logger.LogError("No valid monitor configurations found");
                    return;
                }

                // Start synchronized video playback
                var syncResult = await _videoSynchronizationService.StartSynchronizedPlaybackAsync(configurations);
                if (!syncResult.Success)
                {
                    _logger.LogError("Failed to start synchronized playback: {Errors}", 
                        string.Join(", ", syncResult.Errors));
                    return;
                }

                // Show the standard lock screen windows
                await ShowLockScreenAsync(settings);

                _logger.LogInformation("Enhanced lock screen shown successfully with {Count} synchronized displays", 
                    syncResult.SynchronizedContexts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show enhanced lock screen");
                throw;
            }
        }

        /// <summary>
        /// Hides enhanced lock screen and restores system
        /// </summary>
        public async Task HideEnhancedLockScreenAsync()
        {
            try
            {
                _logger.LogInformation("Hiding enhanced lock screen");

                // Stop synchronized playback
                await _videoSynchronizationService.StopSynchronizedPlaybackAsync();

                // Disable kiosk mode if it was enabled
                await _systemIntegrationService.DisableKioskModeAsync();

                // Hide standard lock screen
                await HideLockScreenAsync();

                _logger.LogInformation("Enhanced lock screen hidden successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide enhanced lock screen");
                throw;
            }
        }

        /// <summary>
        /// Gets whether the lock screen is currently visible
        /// </summary>
        public bool IsVisible => _isLockScreenVisible;
    }

    /// <summary>
    /// Full-screen overlay window for displaying video lock screen
    /// </summary>
    public class LockScreenWindow : Window
    {
        #region Windows API Declarations
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_SHOWNOACTIVATE = 4;
        
        #endregion

        private readonly ILogger _logger;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly MonitorInfo _monitor;
        private readonly VideoLockScreenSettings _settings;
        private MediaElement? _mediaElement;
        private HwndSource? _hwndSource;

        public LockScreenWindow(
            ILogger logger,
            IVideoPlayerService videoPlayerService,
            MonitorInfo monitor,
            VideoLockScreenSettings settings)
        {
            _logger = logger;
            _videoPlayerService = videoPlayerService;
            _monitor = monitor;
            _settings = settings;

            InitializeWindow();
            CreateVideoContent();
        }

        /// <summary>
        /// Initializes the window properties
        /// </summary>
        private void InitializeWindow()
        {
            // Window configuration for lock screen overlay
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            AllowsTransparency = false;
            Background = System.Windows.Media.Brushes.Black;

            // Position and size for specific monitor
            Left = _monitor.Bounds.X;
            Top = _monitor.Bounds.Y;
            Width = _monitor.Bounds.Width;
            Height = _monitor.Bounds.Height;

            // Prevent window from stealing focus
            ShowActivated = false;

            // Set title for debugging
            Title = $"VideoLockScreen_{_monitor.DeviceName}";

            // Handle key events to prevent system shortcuts
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            
            // Handle window loaded event for additional configuration
            Loaded += OnWindowLoaded;
            
            // Handle window closed event for cleanup
            Closed += OnWindowClosed;

            _logger.LogDebug("Lock screen window initialized for monitor {MonitorName} at {X},{Y} {Width}x{Height}",
                _monitor.DeviceName, Left, Top, Width, Height);
        }

        /// <summary>
        /// Handles window loaded event for Win32 API configuration
        /// </summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get window handle
                _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                var hwnd = new WindowInteropHelper(this).Handle;

                // Set extended window style to prevent activation and hide from Alt+Tab
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

                // Ensure window stays on top
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                _logger.LogDebug("Lock screen window Win32 configuration applied for {MonitorName}", _monitor.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply Win32 configuration to lock screen window for {MonitorName}", _monitor.DeviceName);
            }
        }

        /// <summary>
        /// Handles key down events to prevent system shortcuts
        /// </summary>
        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Block common system key combinations during lock screen
            switch (e.Key)
            {
                case Key.LWin:
                case Key.RWin:
                case Key.Tab when (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt:
                case Key.Escape when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                case Key.Delete when (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt):
                    e.Handled = true;
                    _logger.LogDebug("Blocked system key combination: {Key} with modifiers {Modifiers}", e.Key, Keyboard.Modifiers);
                    break;
            }
        }

        /// <summary>
        /// Handles key up events
        /// </summary>
        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Additional key handling if needed
            e.Handled = true;
        }

        /// <summary>
        /// Handles window closed event for cleanup
        /// </summary>
        private void OnWindowClosed(object sender, EventArgs e)
        {
            _hwndSource?.Dispose();
            _hwndSource = null;
            
            _logger.LogDebug("Lock screen window closed and cleaned up for {MonitorName}", _monitor.DeviceName);
        }

        /// <summary>
        /// Creates the video content for the window
        /// </summary>
        private void CreateVideoContent()
        {
            try
            {
                // Create media element with optimized settings for lock screen
                _mediaElement = _videoPlayerService.CreateMediaElement();
                _videoPlayerService.ConfigureMediaElement(_mediaElement, _settings);

                // Configure video scaling and rendering
                if (_mediaElement != null)
                {
                    // Optimize for full-screen rendering
                    _mediaElement.Stretch = GetOptimalStretch(_settings.ScalingMode);
                    _mediaElement.StretchDirection = StretchDirection.Both;
                    
                    // Handle video events for better control
                    _mediaElement.MediaOpened += OnMediaOpened;
                    _mediaElement.MediaFailed += OnMediaFailed;
                    _mediaElement.MediaEnded += OnMediaEnded;
                }

                // Create container with proper layout
                var container = new Grid
                {
                    Background = System.Windows.Media.Brushes.Black,
                    ClipToBounds = true
                };

                // Add video element
                if (_mediaElement != null)
                {
                    container.Children.Add(_mediaElement);
                }

                // Set as window content
                Content = container;

                _logger.LogDebug("Enhanced video content created for lock screen window on monitor {MonitorName}", _monitor.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating video content for lock screen window on monitor {MonitorName}", _monitor.DeviceName);
                
                // Create fallback content
                CreateFallbackContent();
            }
        }

        /// <summary>
        /// Gets the optimal stretch mode based on settings
        /// </summary>
        private Stretch GetOptimalStretch(VideoScalingMode scalingMode)
        {
            return scalingMode switch
            {
                VideoScalingMode.Stretch => Stretch.Fill,
                VideoScalingMode.Uniform => Stretch.Uniform,
                VideoScalingMode.UniformToFill => Stretch.UniformToFill,
                VideoScalingMode.None => Stretch.None,
                _ => Stretch.UniformToFill
            };
        }

        /// <summary>
        /// Creates fallback content when video fails to load
        /// </summary>
        private void CreateFallbackContent()
        {
            try
            {
                var fallbackGrid = new Grid
                {
                    Background = System.Windows.Media.Brushes.Black
                };

                var textBlock = new TextBlock
                {
                    Text = "Video Lock Screen",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 48,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Opacity = 0.3
                };

                fallbackGrid.Children.Add(textBlock);
                Content = fallbackGrid;

                _logger.LogInformation("Fallback content created for lock screen window on monitor {MonitorName}", _monitor.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create fallback content for lock screen window on monitor {MonitorName}", _monitor.DeviceName);
            }
        }

        /// <summary>
        /// Handles media opened event
        /// </summary>
        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            _logger.LogDebug("Video media opened successfully for lock screen on monitor {MonitorName}", _monitor.DeviceName);
        }

        /// <summary>
        /// Handles media failed event
        /// </summary>
        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _logger.LogError(e.ErrorException, "Video media failed to play for lock screen on monitor {MonitorName}", _monitor.DeviceName);
            CreateFallbackContent();
        }

        /// <summary>
        /// Handles media ended event
        /// </summary>
        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _logger.LogDebug("Video media ended for lock screen on monitor {MonitorName}", _monitor.DeviceName);
        }

        /// <summary>
        /// Override to prevent closing by user
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _logger.LogDebug("Lock screen window closing for monitor {MonitorName}", _monitor.DeviceName);
            base.OnClosing(e);
        }

        /// <summary>
        /// Override to handle loaded event
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            try
            {
                // Make sure window stays on top
                var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
                var hwnd = windowInteropHelper.Handle;
                
                // This would require SystemHelper or direct P/Invoke
                // _systemHelper.MakeWindowTopmost(hwnd);
                
                _logger.LogDebug("Lock screen window source initialized for monitor {MonitorName}", _monitor.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in OnSourceInitialized for lock screen window");
            }
        }
    }
}