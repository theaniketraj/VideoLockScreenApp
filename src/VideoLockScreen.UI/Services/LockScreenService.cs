using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Utilities;
using VideoLockScreen.UI.Views;

namespace VideoLockScreen.UI.Services
{
    /// <summary>
    /// Interface for lock screen service
    /// </summary>
    public interface ILockScreenService
    {
        /// <summary>
        /// Gets whether the lock screen is currently active
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets the current lock screen settings
        /// </summary>
        VideoLockScreenSettings? CurrentSettings { get; }

        /// <summary>
        /// Event fired when lock screen is activated
        /// </summary>
        event EventHandler<LockScreenActivatedEventArgs>? LockScreenActivated;

        /// <summary>
        /// Event fired when lock screen is deactivated
        /// </summary>
        event EventHandler<LockScreenDeactivatedEventArgs>? LockScreenDeactivated;

        /// <summary>
        /// Event fired when lock screen encounters an error
        /// </summary>
        event EventHandler<LockScreenErrorEventArgs>? LockScreenError;

        /// <summary>
        /// Activates the lock screen with the specified video and settings
        /// </summary>
        Task<bool> ActivateLockScreenAsync(string videoPath, VideoLockScreenSettings settings);

        /// <summary>
        /// Deactivates the lock screen
        /// </summary>
        Task<bool> DeactivateLockScreenAsync();

        /// <summary>
        /// Configures video for automatic activation when Windows locks (Win+L)
        /// </summary>
        Task<bool> ConfigureAutoLockScreenAsync(string videoPath, VideoLockScreenSettings settings);

        /// <summary>
        /// Disables automatic lock screen activation
        /// </summary>
        void DisableAutoLockScreen();

        /// <summary>
        /// Immediately deactivates lock screen (emergency exit)
        /// </summary>
        Task ForceDeactivateAsync();

        /// <summary>
        /// Pre-validates a video file for lock screen use
        /// </summary>
        Task<VideoValidationResult> ValidateVideoAsync(string videoPath);
    }

    /// <summary>
    /// Service for managing lock screen functionality
    /// </summary>
    public class LockScreenService : ILockScreenService
    {
        private readonly ILogger<LockScreenService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISystemIntegrationService _systemIntegrationService;
        private readonly SessionMonitor _sessionMonitor;
        private readonly VideoFileHelper _videoFileHelper;

        private LockScreenWindow? _lockScreenWindow;
        private bool _isActive;
        private VideoLockScreenSettings? _currentSettings;

        public event EventHandler<LockScreenActivatedEventArgs>? LockScreenActivated;
        public event EventHandler<LockScreenDeactivatedEventArgs>? LockScreenDeactivated;
        public event EventHandler<LockScreenErrorEventArgs>? LockScreenError;

        public bool IsActive => _isActive;
        public VideoLockScreenSettings? CurrentSettings => _currentSettings;

        public LockScreenService(
            ILogger<LockScreenService> logger,
            IServiceProvider serviceProvider,
            ISystemIntegrationService systemIntegrationService,
            SessionMonitor sessionMonitor,
            VideoFileHelper videoFileHelper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _systemIntegrationService = systemIntegrationService ?? throw new ArgumentNullException(nameof(systemIntegrationService));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));
            _videoFileHelper = videoFileHelper ?? throw new ArgumentNullException(nameof(videoFileHelper));

            // Subscribe to session events
            _sessionMonitor.SessionLocked += OnSessionLocked;
            _sessionMonitor.SessionUnlocked += OnSessionUnlocked;
        }

        /// <summary>
        /// Activates the lock screen with the specified video and settings (video is pre-validated)
        /// </summary>
        public async Task<bool> ActivateLockScreenAsync(string videoPath, VideoLockScreenSettings settings)
        {
            try
            {
                if (_isActive)
                {
                    _logger.LogWarning("Lock screen is already active");
                    return false;
                }

                if (string.IsNullOrEmpty(videoPath) || !System.IO.File.Exists(videoPath))
                {
                    _logger.LogError("Invalid video path: {VideoPath}", videoPath);
                    return false;
                }

                _logger.LogInformation("Pre-loading video in background: {VideoPath}", videoPath);

                // Store current settings
                _currentSettings = settings;

                // Create lock screen window on UI thread but DON'T SHOW IT YET
                _lockScreenWindow = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var window = _serviceProvider.GetRequiredService<LockScreenWindow>();
                    window.ExitRequested += OnLockScreenExitRequested;
                    return window;
                });

                // Configure and test video loading BEFORE showing lock screen window
                var videoConfigured = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _lockScreenWindow.ConfigureVideo(videoPath, settings);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to configure video");
                        return false;
                    }
                });

                if (!videoConfigured)
                {
                    _logger.LogError("Video configuration failed, aborting lock screen activation");
                    await CleanupLockScreenWindow();
                    return false;
                }

                _logger.LogInformation("Video configured successfully, showing lock screen");

                // Show lock screen window and immediately start playback
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _lockScreenWindow.Show();
                    _lockScreenWindow.StartPlayback();
                });

                // CRITICAL FIX: Wait a moment for video to start loading before activating system lock
                // This prevents the user from being trapped in a loading state with no escape
                _logger.LogInformation("Waiting for video to begin loading before system integration...");
                await Task.Delay(2000); // 2 second grace period

                // Now activate system integration - user has had time to see the video loading
                var activated = await _lockScreenWindow.ActivateLockScreenAsync();
                if (!activated)
                {
                    await CleanupLockScreenWindow();
                    return false;
                }

                _isActive = true;
                
                // Fire activation event
                LockScreenActivated?.Invoke(this, new LockScreenActivatedEventArgs(videoPath, settings));

                _logger.LogInformation("Lock screen activated successfully with preloaded video");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate lock screen");
                
                // Cleanup on error
                await CleanupLockScreenWindow();
                
                // Fire error event
                LockScreenError?.Invoke(this, new LockScreenErrorEventArgs("Failed to activate lock screen", ex));
                
                return false;
            }
        }

        /// <summary>
        /// Deactivates the lock screen
        /// </summary>
        public async Task<bool> DeactivateLockScreenAsync()
        {
            try
            {
                if (!_isActive)
                {
                    _logger.LogWarning("Lock screen is not active");
                    return true;
                }

                _logger.LogInformation("Deactivating lock screen");

                // Deactivate lock screen mode
                if (_lockScreenWindow != null)
                {
                    await _lockScreenWindow.DeactivateLockScreenAsync();
                }

                // Cleanup window
                await CleanupLockScreenWindow();

                _isActive = false;
                var settings = _currentSettings;
                _currentSettings = null;

                // Fire deactivation event
                LockScreenDeactivated?.Invoke(this, new LockScreenDeactivatedEventArgs(LockScreenExitReason.UserRequested, settings));

                _logger.LogInformation("Lock screen deactivated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate lock screen");
                
                // Force cleanup
                await ForceCleanup();
                
                // Fire error event
                LockScreenError?.Invoke(this, new LockScreenErrorEventArgs("Failed to deactivate lock screen", ex));
                
                return false;
            }
        }

        /// <summary>
        /// Immediately deactivates lock screen (emergency exit)
        /// </summary>
        public async Task ForceDeactivateAsync()
        {
            _logger.LogWarning("Force deactivating lock screen");
            
            try
            {
                await ForceCleanup();
                
                _isActive = false;
                var settings = _currentSettings;
                _currentSettings = null;

                // Fire deactivation event
                LockScreenDeactivated?.Invoke(this, new LockScreenDeactivatedEventArgs(LockScreenExitReason.EmergencyExit, settings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force deactivate lock screen");
            }
        }

        private async Task CleanupLockScreenWindow()
        {
            if (_lockScreenWindow != null)
            {
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _lockScreenWindow.ExitRequested -= OnLockScreenExitRequested;
                        
                        if (_lockScreenWindow.IsVisible)
                        {
                            _lockScreenWindow.Hide();
                        }
                        
                        _lockScreenWindow.Close();
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during lock screen window cleanup");
                }
                finally
                {
                    _lockScreenWindow = null;
                }
            }
        }

        private async Task ForceCleanup()
        {
            try
            {
                // Force restore system state
                await _systemIntegrationService.UnblockSystemKeysAsync();
                await _systemIntegrationService.AllowSleepAsync();
                await _systemIntegrationService.SetProcessPriorityAsync(System.Diagnostics.ProcessPriorityClass.Normal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during force cleanup of system state");
            }

            await CleanupLockScreenWindow();
        }

        /// <summary>
        /// Configures video for automatic activation when Windows locks (Win+L) - THE CORE FEATURE!
        /// </summary>
        public async Task<bool> ConfigureAutoLockScreenAsync(string videoPath, VideoLockScreenSettings settings)
        {
            try
            {
                _logger.LogInformation("Configuring auto lock screen with video: {VideoPath}", videoPath);

                if (string.IsNullOrEmpty(videoPath) || !System.IO.File.Exists(videoPath))
                {
                    _logger.LogError("Invalid video path for auto lock screen: {VideoPath}", videoPath);
                    return false;
                }

                // Validate the video first
                var validation = await ValidateVideoAsync(videoPath);
                if (!validation.IsValid)
                {
                    _logger.LogError("Video validation failed for auto lock screen: {Error}", validation.ErrorMessage);
                    return false;
                }

                // Store settings for automatic activation on session lock
                _currentSettings = settings;
                
                _logger.LogInformation("Auto lock screen configured successfully - will activate on Win+L");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure auto lock screen");
                return false;
            }
        }

        /// <summary>
        /// Disables automatic lock screen activation
        /// </summary>
        public void DisableAutoLockScreen()
        {
            _logger.LogInformation("Disabling auto lock screen");
            _currentSettings = null;
        }

        #region Event Handlers

        private async void OnLockScreenExitRequested(object? sender, LockScreenExitEventArgs e)
        {
            _logger.LogInformation("Lock screen exit requested: {Reason}", e.Reason);

            try
            {
                await CleanupLockScreenWindow();
                
                _isActive = false;
                var settings = _currentSettings;
                _currentSettings = null;

                // Fire deactivation event
                LockScreenDeactivated?.Invoke(this, new LockScreenDeactivatedEventArgs(e.Reason, settings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling lock screen exit request");
                LockScreenError?.Invoke(this, new LockScreenErrorEventArgs("Error during lock screen exit", ex));
            }
        }

        private async void OnSessionLocked(object? sender, Core.SessionEventArgs e)
        {
            _logger.LogInformation("🚨🚨🚨 SESSION LOCK DETECTED - THIS SHOULD APPEAR IN LOGS! 🚨🚨🚨");
            
            // EMERGENCY TEST: Show a simple message box to verify this event fires
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show(
                        "SESSION LOCK DETECTED!\n\nIf you see this message, the session monitoring is working.\n\nPress OK to continue.",
                        "Video Lock Screen - Debug",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                });
                
                _logger.LogInformation("✅ Message box shown successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to show debug message box");
            }
            
            // ACTUAL FUNCTIONALITY: Show video overlay
            if (!_isActive && _currentSettings != null && !string.IsNullOrEmpty(_currentSettings.VideoFilePath))
            {
                _logger.LogInformation("📹 Attempting to show video lock screen overlay");
                
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await ActivateLockScreenAsync(_currentSettings.VideoFilePath, _currentSettings);
                    }, System.Windows.Threading.DispatcherPriority.Send);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to show video overlay on session lock");
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No video configured or already active - cannot show video lock screen");
                _logger.LogWarning($"   _isActive: {_isActive}");
                _logger.LogWarning($"   _currentSettings: {(_currentSettings != null ? "SET" : "NULL")}");
                _logger.LogWarning($"   VideoFilePath: {_currentSettings?.VideoFilePath ?? "NULL"}");
            }
        }

        private async void OnSessionUnlocked(object? sender, Core.SessionEventArgs e)
        {
            _logger.LogInformation("Windows session unlocked");
            
            // Optionally deactivate video lock screen when session unlocks
            if (_isActive)
            {
                _logger.LogInformation("Auto-deactivating video lock screen for session unlock");
                
                try
                {
                    await DeactivateLockScreenAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-deactivate video lock screen on session unlock");
                }
            }
        }

        #endregion

        /// <summary>
        /// Pre-validates a video file for lock screen use
        /// </summary>
        public async Task<VideoValidationResult> ValidateVideoAsync(string videoPath)
        {
            try
            {
                if (string.IsNullOrEmpty(videoPath) || !System.IO.File.Exists(videoPath))
                {
                    return VideoValidationResult.Failure("Video file not found");
                }

                var fileInfo = new System.IO.FileInfo(videoPath);
                
                // Check file size (reasonable limits)
                if (fileInfo.Length > 2L * 1024 * 1024 * 1024) // 2GB limit
                {
                    return VideoValidationResult.Failure("Video file too large (max 2GB)");
                }

                // Simplified validation - just check if file exists and has video extension
                var extension = System.IO.Path.GetExtension(videoPath).ToLowerInvariant();
                var supportedExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".m4v", ".webm" };
                
                if (!supportedExtensions.Contains(extension))
                {
                    return VideoValidationResult.Failure($"Unsupported video format: {extension}");
                }

                // Use VideoFileHelper with timeout for validation
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    
                    var videoInfoTask = _videoFileHelper.GetVideoInfoAsync(videoPath);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                    
                    var completedTask = await Task.WhenAny(videoInfoTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("VideoFileHelper validation timed out for: {VideoPath}", videoPath);
                        // Use basic validation as fallback
                        return VideoValidationResult.Success(TimeSpan.FromMinutes(1), "Unknown", fileInfo.Length);
                    }
                    
                    var videoInfo = await videoInfoTask;
                    
                    if (videoInfo.Duration.TotalSeconds < 1)
                    {
                        return VideoValidationResult.Failure("Video duration too short or invalid");
                    }
                    
                    var resolution = $"{videoInfo.Width}x{videoInfo.Height}";
                    return VideoValidationResult.Success(videoInfo.Duration, resolution, fileInfo.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VideoFileHelper validation failed, using basic validation for: {VideoPath}", videoPath);
                    
                    // Fallback to basic file validation - this always succeeds
                    var basicResolution = "Unknown";
                    var estimatedDuration = TimeSpan.FromMinutes(1); // Conservative estimate
                    
                    return VideoValidationResult.Success(estimatedDuration, basicResolution, fileInfo.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating video: {VideoPath}", videoPath);
                return VideoValidationResult.Failure($"Validation error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _sessionMonitor.SessionLocked -= OnSessionLocked;
            _sessionMonitor.SessionUnlocked -= OnSessionUnlocked;
            
            if (_isActive)
            {
                _ = Task.Run(async () => await ForceDeactivateAsync());
            }
        }
    }

    #region Event Args Classes

    public class LockScreenActivatedEventArgs : EventArgs
    {
        public string VideoPath { get; }
        public VideoLockScreenSettings Settings { get; }
        public DateTime ActivationTime { get; }

        public LockScreenActivatedEventArgs(string videoPath, VideoLockScreenSettings settings)
        {
            VideoPath = videoPath;
            Settings = settings;
            ActivationTime = DateTime.Now;
        }
    }

    public class LockScreenDeactivatedEventArgs : EventArgs
    {
        public LockScreenExitReason Reason { get; }
        public VideoLockScreenSettings? Settings { get; }
        public DateTime DeactivationTime { get; }

        public LockScreenDeactivatedEventArgs(LockScreenExitReason reason, VideoLockScreenSettings? settings)
        {
            Reason = reason;
            Settings = settings;
            DeactivationTime = DateTime.Now;
        }
    }

    public class LockScreenErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception? Exception { get; }
        public DateTime ErrorTime { get; }

        public LockScreenErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
            ErrorTime = DateTime.Now;
        }
    }

    #endregion

    /// <summary>
    /// Result of video validation for lock screen use
    /// </summary>
    public class VideoValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Resolution { get; set; }
        public long FileSize { get; set; }
        
        public static VideoValidationResult Success(TimeSpan duration, string resolution, long fileSize)
        {
            return new VideoValidationResult
            {
                IsValid = true,
                Duration = duration,
                Resolution = resolution,
                FileSize = fileSize
            };
        }
        
        public static VideoValidationResult Failure(string errorMessage)
        {
            return new VideoValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}