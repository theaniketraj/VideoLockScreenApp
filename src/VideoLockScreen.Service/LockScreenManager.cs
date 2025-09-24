using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Utilities;
using WpfApplication = System.Windows.Application;

namespace VideoLockScreen.Service
{
    /// <summary>
    /// Manages the video lock screen overlay windows
    /// </summary>
    public class LockScreenManager
    {
        private readonly ILogger<LockScreenManager> _logger;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly SystemHelper _systemHelper;
        private readonly List<LockScreenWindow> _overlayWindows = new();
        private bool _isLockScreenVisible = false;
        private int _currentLoopCount = 0;
        private int _maxLoopCount = -1;

        public LockScreenManager(
            ILogger<LockScreenManager> logger,
            IVideoPlayerService videoPlayerService,
            SystemHelper systemHelper)
        {
            _logger = logger;
            _videoPlayerService = videoPlayerService;
            _systemHelper = systemHelper;

            // Subscribe to video player events
            _videoPlayerService.PlaybackEnded += OnVideoPlaybackEnded;
            _videoPlayerService.PlaybackError += OnVideoPlaybackError;
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
        /// Gets whether the lock screen is currently visible
        /// </summary>
        public bool IsVisible => _isLockScreenVisible;
    }

    /// <summary>
    /// Full-screen overlay window for displaying video lock screen
    /// </summary>
    public class LockScreenWindow : Window
    {
        private readonly ILogger _logger;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly MonitorInfo _monitor;
        private readonly VideoLockScreenSettings _settings;
        private MediaElement? _mediaElement;

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

            // Prevent interaction
            IsHitTestVisible = false;

            // Set title for debugging
            Title = $"VideoLockScreen_{_monitor.DeviceName}";

            _logger.LogDebug("Lock screen window initialized for monitor {MonitorName} at {X},{Y} {Width}x{Height}",
                _monitor.DeviceName, Left, Top, Width, Height);
        }

        /// <summary>
        /// Creates the video content for the window
        /// </summary>
        private void CreateVideoContent()
        {
            try
            {
                // Create media element
                _mediaElement = _videoPlayerService.CreateMediaElement();
                _videoPlayerService.ConfigureMediaElement(_mediaElement, _settings);

                // Create a grid container
                var grid = new Grid();
                grid.Children.Add(_mediaElement);

                Content = grid;

                _logger.LogDebug("Video content created for lock screen window");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating video content for lock screen window");
            }
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