using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;
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
        /// Immediately deactivates lock screen (emergency exit)
        /// </summary>
        Task ForceDeactivateAsync();
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
            SessionMonitor sessionMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _systemIntegrationService = systemIntegrationService ?? throw new ArgumentNullException(nameof(systemIntegrationService));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));

            // Subscribe to session events
            _sessionMonitor.SessionLocked += OnSessionLocked;
            _sessionMonitor.SessionUnlocked += OnSessionUnlocked;
        }

        /// <summary>
        /// Activates the lock screen with the specified video and settings
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

                _logger.LogInformation("Activating lock screen with video: {VideoPath}", videoPath);

                // Store current settings
                _currentSettings = settings;

                // Create lock screen window on UI thread
                _lockScreenWindow = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var window = _serviceProvider.GetRequiredService<LockScreenWindow>();
                    window.ExitRequested += OnLockScreenExitRequested;
                    return window;
                });

                // Load video
                var videoLoaded = await _lockScreenWindow.LoadVideoAsync(videoPath, settings);
                if (!videoLoaded)
                {
                    await CleanupLockScreenWindow();
                    return false;
                }

                // Show lock screen window
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _lockScreenWindow.Show();
                });

                // Activate lock screen mode
                var activated = await _lockScreenWindow.ActivateLockScreenAsync();
                if (!activated)
                {
                    await CleanupLockScreenWindow();
                    return false;
                }

                _isActive = true;
                
                // Fire activation event
                LockScreenActivated?.Invoke(this, new LockScreenActivatedEventArgs(videoPath, settings));

                _logger.LogInformation("Lock screen activated successfully");
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
            _logger.LogInformation("Windows session locked");
            
            // Don't auto-activate lock screen on session lock
            // This should be handled by the main application logic
        }

        private async void OnSessionUnlocked(object? sender, Core.SessionEventArgs e)
        {
            _logger.LogInformation("Windows session unlocked");
            
            // Optionally deactivate lock screen when session is unlocked
            // For now, don't auto-deactivate on session unlock
            // This behavior can be configured later
        }

        #endregion

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
}