using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Services;

namespace VideoLockScreen.Service
{
    /// <summary>
    /// Main Windows Service for Video Lock Screen application
    /// </summary>
    public class VideoLockScreenService : BackgroundService
    {
        private readonly ILogger<VideoLockScreenService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly LockScreenManager _lockScreenManager;
        private SessionMonitor? _sessionMonitor;

        public VideoLockScreenService(
            ILogger<VideoLockScreenService> logger,
            IConfigurationService configurationService,
            IVideoPlayerService videoPlayerService,
            LockScreenManager lockScreenManager)
        {
            _logger = logger;
            _configurationService = configurationService;
            _videoPlayerService = videoPlayerService;
            _lockScreenManager = lockScreenManager;
        }

        /// <summary>
        /// Called when the service starts
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Video Lock Screen Service starting");

            try
            {
                // Load configuration
                await _configurationService.LoadSettingsAsync();
                
                // Initialize session monitoring
                _sessionMonitor = new SessionMonitor();
                _sessionMonitor.SessionLocked += OnSessionLocked;
                _sessionMonitor.SessionUnlocked += OnSessionUnlocked;

                // Start monitoring sessions
                if (!_sessionMonitor.StartMonitoring())
                {
                    _logger.LogError("Failed to start session monitoring");
                    throw new InvalidOperationException("Could not initialize session monitoring");
                }

                _logger.LogInformation("Session monitoring started successfully");

                // Subscribe to settings changes
                _configurationService.SettingsChanged += OnSettingsChanged;

                await base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Video Lock Screen Service");
                throw;
            }
        }

        /// <summary>
        /// Called when the service stops
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Video Lock Screen Service stopping");

            try
            {
                // Stop session monitoring
                _sessionMonitor?.StopMonitoring();
                
                // Hide any active lock screen
                await _lockScreenManager.HideLockScreenAsync();

                // Unsubscribe from events
                if (_sessionMonitor != null)
                {
                    _sessionMonitor.SessionLocked -= OnSessionLocked;
                    _sessionMonitor.SessionUnlocked -= OnSessionUnlocked;
                }

                _configurationService.SettingsChanged -= OnSettingsChanged;

                await base.StopAsync(cancellationToken);

                _logger.LogInformation("Video Lock Screen Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Video Lock Screen Service");
            }
        }

        /// <summary>
        /// Main service execution loop
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Video Lock Screen Service is running");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Perform periodic health checks and maintenance
                    await PerformHealthCheckAsync();
                    
                    // Wait for 30 seconds before next check
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Service execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in service execution loop");
                throw;
            }
        }

        /// <summary>
        /// Handles session locked events
        /// </summary>
        private async void OnSessionLocked(object? sender, SessionEventArgs e)
        {
            _logger.LogInformation("Session locked at {Timestamp}", e.Timestamp);

            try
            {
                var settings = _configurationService.Settings;
                
                if (!settings.IsEnabled)
                {
                    _logger.LogDebug("Video lock screen is disabled, skipping");
                    return;
                }

                if (string.IsNullOrEmpty(settings.VideoFilePath) || !File.Exists(settings.VideoFilePath))
                {
                    _logger.LogWarning("Video file not found: {VideoFilePath}", settings.VideoFilePath);
                    return;
                }

                // Show the video lock screen
                await _lockScreenManager.ShowLockScreenAsync(settings);
                
                _logger.LogInformation("Video lock screen displayed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session locked event");
            }
        }

        /// <summary>
        /// Handles session unlocked events
        /// </summary>
        private async void OnSessionUnlocked(object? sender, SessionEventArgs e)
        {
            _logger.LogInformation("Session unlocked at {Timestamp}", e.Timestamp);

            try
            {
                // Hide the video lock screen
                await _lockScreenManager.HideLockScreenAsync();
                
                _logger.LogInformation("Video lock screen hidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session unlocked event");
            }
        }

        /// <summary>
        /// Handles settings changes
        /// </summary>
        private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            _logger.LogDebug("Settings changed: {ChangeType}", e.ChangeType);

            try
            {
                // If video file changed, validate it
                if (e.ChangeType == SettingsChangeType.PropertyChanged && 
                    e.PropertyName == nameof(VideoLockScreenSettings.VideoFilePath))
                {
                    ValidateVideoFile(e.Settings.VideoFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling settings change");
            }
        }

        /// <summary>
        /// Validates a video file
        /// </summary>
        private void ValidateVideoFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Video file does not exist: {FilePath}", filePath);
                    return;
                }

                var fileInfo = new FileInfo(filePath);
                _logger.LogInformation("Video file validated: {FileName} ({FileSize} bytes)", 
                    fileInfo.Name, fileInfo.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating video file: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Performs periodic health checks
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            try
            {
                // Check if session monitor is still working
                if (_sessionMonitor == null || !_sessionMonitor.IsMonitoring)
                {
                    _logger.LogWarning("Session monitor is not active, attempting to restart");
                    
                    // Try to restart session monitoring
                    _sessionMonitor?.StopMonitoring();
                    _sessionMonitor = new SessionMonitor();
                    _sessionMonitor.SessionLocked += OnSessionLocked;
                    _sessionMonitor.SessionUnlocked += OnSessionUnlocked;
                    
                    if (!_sessionMonitor.StartMonitoring())
                    {
                        _logger.LogError("Failed to restart session monitoring");
                    }
                    else
                    {
                        _logger.LogInformation("Session monitoring restarted successfully");
                    }
                }

                // Check if video file still exists
                var settings = _configurationService.Settings;
                if (settings.IsEnabled && !string.IsNullOrEmpty(settings.VideoFilePath))
                {
                    if (!File.Exists(settings.VideoFilePath))
                    {
                        _logger.LogWarning("Configured video file no longer exists: {FilePath}", 
                            settings.VideoFilePath);
                    }
                }

                // Log basic health status
                _logger.LogDebug("Health check completed - Service is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public override void Dispose()
        {
            try
            {
                _sessionMonitor?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session monitor");
            }

            base.Dispose();
        }
    }
}