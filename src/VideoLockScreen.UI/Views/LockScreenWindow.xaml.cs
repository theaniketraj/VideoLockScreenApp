using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;

namespace VideoLockScreen.UI.Views
{
    /// <summary>
    /// Full-screen lock screen window for video playback
    /// </summary>
    public partial class LockScreenWindow : Window
    {
        private readonly ILogger<LockScreenWindow> _logger;
        private readonly ISystemIntegrationService _systemIntegrationService;
        private readonly IVideoPlayerService _videoPlayerService;
        
        private DispatcherTimer? _exitTimer;
        private DispatcherTimer? _infoTimer;
        private DispatcherTimer? _positionTimer;
        private DateTime _emergencyKeyPressStart;
        private bool _emergencyKeysPressed;
        private bool _isExiting;
        private string? _currentVideoPath;
        private VideoLockScreenSettings? _settings;

        // Emergency exit key combination
        private readonly Key[] _emergencyKeys = { Key.LeftCtrl, Key.LeftAlt, Key.LeftShift, Key.Escape };
        private const int EmergencyExitDelayMs = 3000; // 3 seconds
        private const int InfoDisplayDelayMs = 10000; // 10 seconds

        public event EventHandler<LockScreenExitEventArgs>? ExitRequested;

        public LockScreenWindow(
            ILogger<LockScreenWindow> logger,
            ISystemIntegrationService systemIntegrationService,
            IVideoPlayerService videoPlayerService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _systemIntegrationService = systemIntegrationService ?? throw new ArgumentNullException(nameof(systemIntegrationService));
            _videoPlayerService = videoPlayerService ?? throw new ArgumentNullException(nameof(videoPlayerService));

            InitializeComponent();
            InitializeTimers();
            
            _logger.LogInformation("Lock screen window initialized");
        }

        private void InitializeTimers()
        {
            // Timer for emergency exit detection
            _exitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _exitTimer.Tick += ExitTimer_Tick;

            // Timer for showing video info
            _infoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(InfoDisplayDelayMs)
            };
            _infoTimer.Tick += InfoTimer_Tick;

            // Timer for updating playback position
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _positionTimer.Tick += PositionTimer_Tick;
        }

        /// <summary>
        /// Configures the video for playback (does not start playing)
        /// </summary>
        public void ConfigureVideo(string videoPath, VideoLockScreenSettings settings)
        {
            try
            {
                _currentVideoPath = videoPath;
                _settings = settings;

                _logger.LogInformation("Configuring video: {VideoPath}", videoPath);

                // Configure MediaElement
                VideoPlayer.Source = new Uri(videoPath);
                VideoPlayer.Volume = settings.AudioEnabled ? settings.Volume : 0;
                VideoPlayer.IsEnabled = true;

                // Set video scaling
                VideoPlayer.Stretch = settings.ScalingMode switch
                {
                    VideoScalingMode.Stretch => System.Windows.Media.Stretch.Fill,
                    VideoScalingMode.Uniform => System.Windows.Media.Stretch.Uniform,
                    VideoScalingMode.None => System.Windows.Media.Stretch.None,
                    _ => System.Windows.Media.Stretch.UniformToFill
                };

                // Update video info display
                UpdateVideoInfo(videoPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure video: {VideoPath}", videoPath);
                ShowErrorOverlay($"Failed to configure video: {ex.Message}");
            }
        }

        /// <summary>
        /// Preloads video in background without showing the window - SAFE APPROACH
        /// </summary>
        public async Task<bool> PreloadVideoAsync(string videoPath, VideoLockScreenSettings settings)
        {
            try
            {
                _currentVideoPath = videoPath;
                _settings = settings;
                
                _logger.LogInformation("Preloading video in background: {VideoPath}", videoPath);

                var tcs = new TaskCompletionSource<bool>();
                var timeout = Task.Delay(TimeSpan.FromSeconds(15)); // 15 second timeout for loading

                System.Windows.RoutedEventHandler mediaOpened = null;
                System.EventHandler<System.Windows.ExceptionRoutedEventArgs> mediaFailed = null;

                mediaOpened = (s, e) =>
                {
                    _logger.LogInformation("Video preloaded successfully: {VideoPath}", videoPath);
                    VideoPlayer.MediaOpened -= mediaOpened;
                    VideoPlayer.MediaFailed -= mediaFailed;
                    tcs.TrySetResult(true);
                };

                mediaFailed = (s, e) =>
                {
                    _logger.LogError(e.ErrorException, "Video preload failed: {VideoPath}", videoPath);
                    VideoPlayer.MediaOpened -= mediaOpened;
                    VideoPlayer.MediaFailed -= mediaFailed;
                    tcs.TrySetResult(false);
                };

                // Subscribe to events
                VideoPlayer.MediaOpened += mediaOpened;
                VideoPlayer.MediaFailed += mediaFailed;

                // Configure MediaElement for background loading
                VideoPlayer.Source = new Uri(videoPath);
                VideoPlayer.Volume = 0; // Muted during preload
                VideoPlayer.LoadedBehavior = MediaState.Manual; // Manual control
                VideoPlayer.UnloadedBehavior = MediaState.Stop;

                // Set video scaling for when it's eventually shown
                VideoPlayer.Stretch = settings.ScalingMode switch
                {
                    VideoScalingMode.Stretch => System.Windows.Media.Stretch.Fill,
                    VideoScalingMode.Uniform => System.Windows.Media.Stretch.Uniform,
                    VideoScalingMode.None => System.Windows.Media.Stretch.None,
                    _ => System.Windows.Media.Stretch.UniformToFill
                };

                // Wait for either success or timeout
                var completedTask = await Task.WhenAny(tcs.Task, timeout);
                
                // Cleanup event handlers
                VideoPlayer.MediaOpened -= mediaOpened;
                VideoPlayer.MediaFailed -= mediaFailed;

                if (completedTask == timeout)
                {
                    _logger.LogWarning("Video preload timed out: {VideoPath}", videoPath);
                    return false;
                }

                var success = await tcs.Task;
                if (success)
                {
                    // Configure final settings now that video is loaded
                    VideoPlayer.Volume = settings.AudioEnabled ? settings.Volume : 0;
                    UpdateVideoInfo(videoPath);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preloading video: {VideoPath}", videoPath);
                return false;
            }
        }

        /// <summary>
        /// Starts video playback (call after PreloadVideoAsync)
        /// </summary>
        public void StartPlayback()
        {
            try
            {
                if (VideoPlayer.Source != null)
                {
                    _logger.LogInformation("Starting preloaded video playback");
                    // Video is already loaded, start playing immediately
                    VideoPlayer.Play();
                }
                else
                {
                    ShowErrorOverlay("No video configured for playback");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start video playback");
                ShowErrorOverlay($"Failed to start playback: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables lock screen mode with system integration
        /// </summary>
        public async Task<bool> ActivateLockScreenAsync()
        {
            try
            {
                _logger.LogInformation("Activating lock screen mode");

                // Enable system security features
                await _systemIntegrationService.BlockSystemKeysAsync();
                await _systemIntegrationService.PreventSleepAsync();
                await _systemIntegrationService.SetProcessPriorityAsync(System.Diagnostics.ProcessPriorityClass.High);

                // Start position timer
                _positionTimer?.Start();

                // Start info display timer
                _infoTimer?.Start();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate lock screen mode");
                return false;
            }
        }

        /// <summary>
        /// Disables lock screen mode and restores system state
        /// </summary>
        public async Task<bool> DeactivateLockScreenAsync()
        {
            try
            {
                _logger.LogInformation("Deactivating lock screen mode");

                // Stop all timers
                _exitTimer?.Stop();
                _infoTimer?.Stop();
                _positionTimer?.Stop();

                // Stop video playback
                VideoPlayer.Stop();

                // Restore system state
                await _systemIntegrationService.UnblockSystemKeysAsync();
                await _systemIntegrationService.AllowSleepAsync();
                await _systemIntegrationService.SetProcessPriorityAsync(System.Diagnostics.ProcessPriorityClass.Normal);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate lock screen mode");
                return false;
            }
        }

        private void UpdateVideoInfo(string videoPath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(videoPath);
                var fileInfo = new FileInfo(videoPath);
                
                VideoTitle.Text = fileName;
                VideoInfo.Text = $"Size: {fileInfo.Length / (1024 * 1024):F1} MB";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update video info");
                VideoTitle.Text = "Unknown";
                VideoInfo.Text = "Information unavailable";
            }
        }

        private void ShowLoadingOverlay(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowErrorOverlay(string errorMessage)
        {
            ErrorMessage.Text = errorMessage;
            ErrorOverlay.Visibility = Visibility.Visible;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowEmergencyInstructions(bool show)
        {
            var animation = new DoubleAnimation
            {
                To = show ? 0.8 : 0.0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase()
            };
            
            EmergencyInstructions.BeginAnimation(OpacityProperty, animation);
        }

        private void ShowInfoOverlay(bool show)
        {
            var animation = new DoubleAnimation
            {
                To = show ? 1.0 : 0.0,
                Duration = TimeSpan.FromMilliseconds(1000),
                EasingFunction = new QuadraticEase()
            };
            
            InfoOverlay.BeginAnimation(OpacityProperty, animation);
        }

        #region Event Handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Lock screen window loaded - FORCING to top");
            
            // AGGRESSIVE: Force window to top of all others
            ForceWindowToTop();
            
            // IMMEDIATELY show emergency exit instructions for user safety
            ShowEmergencyInstructions(true);
            
            // Focus the window to ensure it receives key events
            Focus();
            Activate();
        }

        /// <summary>
        /// Aggressively forces the window to appear on top of everything, including Windows lock screen
        /// </summary>
        private void ForceWindowToTop()
        {
            try
            {
                // Multiple approaches to force window to top
                Topmost = true;
                WindowState = WindowState.Maximized;
                
                // Win32 API calls to force window above everything
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    // HWND_TOPMOST = -1, SWP_NOSIZE | SWP_NOMOVE = 0x0003
                    SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0003);
                    
                    // Force bring to foreground
                    SetForegroundWindow(hwnd);
                    BringWindowToTop(hwnd);
                }
                
                _logger.LogInformation("Window forced to top successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force window to top");
            }
        }

        // Win32 API declarations for forcing window to top
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                _logger.LogWarning("Lock screen window close attempt blocked");
                ShowEmergencyInstructions(true);
                return;
            }

            await DeactivateLockScreenAsync();
            _logger.LogInformation("Lock screen window closing");
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check for emergency exit key combination
            CheckEmergencyExit();
            
            // Block all other key events
            e.Handled = true;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Block all mouse events
            e.Handled = true;
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Video media opened successfully");
            
            ShowLoadingOverlay(false);
            VideoPlayer.Play();
            
            // Update playback info
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = VideoPlayer.NaturalDuration.TimeSpan;
                PlaybackInfo.Text = $"Duration: {duration:mm\\:ss}";
            }
        }

        private async void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Video playback ended");
            
            try
            {
                if (_settings?.LoopCount == -1) // Infinite loop
                {
                    VideoPlayer.Position = TimeSpan.Zero;
                    VideoPlayer.Play();
                }
                else if (_settings?.LoopCount > 1)
                {
                    // Handle limited loops (would need loop counter in settings)
                    VideoPlayer.Position = TimeSpan.Zero;
                    VideoPlayer.Play();
                }
                else
                {
                    // Exit lock screen when video ends
                    await RequestExit(LockScreenExitReason.VideoEnded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video end");
                await RequestExit(LockScreenExitReason.Error);
            }
        }

        private async void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _logger.LogError(e.ErrorException, "Video playback failed");
            ShowErrorOverlay($"Video playback failed: {e.ErrorException?.Message}");
            
            await Task.Delay(5000); // Show error for 5 seconds
            await RequestExit(LockScreenExitReason.Error);
        }

        private void ExitTimer_Tick(object? sender, EventArgs e)
        {
            if (_emergencyKeysPressed)
            {
                var elapsed = DateTime.Now - _emergencyKeyPressStart;
                if (elapsed.TotalMilliseconds >= EmergencyExitDelayMs)
                {
                    _exitTimer?.Stop();
                    _ = RequestExit(LockScreenExitReason.EmergencyExit);
                }
            }
        }

        private void InfoTimer_Tick(object? sender, EventArgs e)
        {
            ShowInfoOverlay(true);
            _infoTimer?.Stop();
            
            // Hide info after 5 seconds
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            hideTimer.Tick += (s, e) =>
            {
                ShowInfoOverlay(false);
                hideTimer.Stop();
            };
            hideTimer.Start();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var position = VideoPlayer.Position;
                var duration = VideoPlayer.NaturalDuration.TimeSpan;
                PlaybackInfo.Text = $"Duration: {duration:mm\\:ss} | Position: {position:mm\\:ss}";
            }
        }

        #endregion

        private void CheckEmergencyExit()
        {
            bool allKeysPressed = true;
            foreach (var key in _emergencyKeys)
            {
                if (!Keyboard.IsKeyDown(key))
                {
                    allKeysPressed = false;
                    break;
                }
            }

            if (allKeysPressed && !_emergencyKeysPressed)
            {
                _emergencyKeysPressed = true;
                _emergencyKeyPressStart = DateTime.Now;
                _exitTimer?.Start();
                ShowEmergencyInstructions(true);
                _logger.LogInformation("Emergency exit sequence started");
            }
            else if (!allKeysPressed && _emergencyKeysPressed)
            {
                _emergencyKeysPressed = false;
                _exitTimer?.Stop();
                ShowEmergencyInstructions(false);
                _logger.LogInformation("Emergency exit sequence cancelled");
            }
        }

        private async Task RequestExit(LockScreenExitReason reason)
        {
            if (_isExiting) return;
            
            _isExiting = true;
            _logger.LogInformation("Lock screen exit requested: {Reason}", reason);
            
            ExitRequested?.Invoke(this, new LockScreenExitEventArgs(reason));
            
            await DeactivateLockScreenAsync();
            Close();
        }
    }

    /// <summary>
    /// Event arguments for lock screen exit
    /// </summary>
    public class LockScreenExitEventArgs : EventArgs
    {
        public LockScreenExitReason Reason { get; }
        public DateTime ExitTime { get; }

        public LockScreenExitEventArgs(LockScreenExitReason reason)
        {
            Reason = reason;
            ExitTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Reasons for lock screen exit
    /// </summary>
    public enum LockScreenExitReason
    {
        EmergencyExit,
        VideoEnded,
        Error,
        UserRequested,
        SystemShutdown
    }
}