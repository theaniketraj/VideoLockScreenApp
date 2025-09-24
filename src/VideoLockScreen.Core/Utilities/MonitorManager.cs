using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.Utilities
{
    /// <summary>
    /// Enhanced monitor management for multi-monitor video lock screen support
    /// </summary>
    public class MonitorManager
    {
        private readonly ILogger<MonitorManager> _logger;
        private readonly SystemHelper _systemHelper;
        private readonly List<MonitorConfiguration> _monitorConfigurations = new();
        private bool _preferPrimaryOnly = false;

        public MonitorManager(ILogger<MonitorManager> logger, SystemHelper systemHelper)
        {
            _logger = logger;
            _systemHelper = systemHelper;
        }

        /// <summary>
        /// Gets the current monitor configuration
        /// </summary>
        public async Task<List<MonitorConfiguration>> GetCurrentConfigurationAsync()
        {
            try
            {
                _logger.LogDebug("Getting current monitor configuration");

                var monitors = _systemHelper.GetMonitorInfo();
                var configurations = new List<MonitorConfiguration>();

                foreach (var monitor in monitors)
                {
                    var config = new MonitorConfiguration
                    {
                        Monitor = monitor,
                        IsEnabled = monitor.IsPrimary || !_preferPrimaryOnly,
                        RenderingMode = DetermineOptimalRenderingMode(monitor),
                        Priority = monitor.IsPrimary ? 1 : 0
                    };

                    configurations.Add(config);
                }

                _logger.LogInformation("Retrieved configuration for {Count} monitors", configurations.Count);
                return configurations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current monitor configuration");
                return new List<MonitorConfiguration>();
            }
        }

        /// <summary>
        /// Creates a configuration for each connected monitor
        /// </summary>
        public MonitorConfiguration CreateConfiguration(MonitorInfo monitor, VideoLockScreenSettings videoSettings)
        {
            var configuration = new MonitorConfiguration
            {
                Monitor = monitor,
                VideoSettings = videoSettings,
                RenderingMode = DetermineOptimalRenderingMode(monitor),
                IsEnabled = true,
                Priority = monitor.IsPrimary ? 1 : 0
            };

            _logger.LogDebug("Created configuration for monitor {MonitorName}: {Width}x{Height}, Mode: {RenderingMode}",
                monitor.DeviceName, monitor.Bounds.Width, monitor.Bounds.Height, configuration.RenderingMode);

            return configuration;
        }

        /// <summary>
        /// Determines the optimal rendering mode for a monitor
        /// </summary>
        private RenderingMode DetermineOptimalRenderingMode(MonitorInfo monitor)
        {
            var totalPixels = monitor.Bounds.Width * monitor.Bounds.Height;

            if (totalPixels >= 3840 * 2160) // 4K or higher
            {
                return RenderingMode.HighQuality;
            }
            else if (totalPixels >= 1920 * 1080) // 1080p
            {
                return RenderingMode.Optimized;
            }
            else
            {
                return RenderingMode.Standard;
            }
        }

        /// <summary>
        /// Optimizes configurations for performance
        /// </summary>
        public async Task<ValidationResult> OptimizeConfigurationsAsync(VideoPerformanceMode performanceMode)
        {
            try
            {
                _logger.LogInformation("Optimizing monitor configurations for {PerformanceMode} mode", performanceMode);

                var result = new ValidationResult();

                foreach (var config in _monitorConfigurations)
                {
                    // Adjust rendering mode based on performance requirements
                    config.RenderingMode = performanceMode switch
                    {
                        VideoPerformanceMode.Battery => RenderingMode.PowerSaver,
                        VideoPerformanceMode.Balanced => RenderingMode.Optimized,
                        VideoPerformanceMode.Quality => RenderingMode.HighQuality,
                        _ => RenderingMode.Standard
                    };

                    // Validate the optimized configuration
                    if (ValidateConfiguration(config))
                    {
                        result.ValidConfigurations.Add(config);
                    }
                    else
                    {
                        result.AddWarning($"Configuration for monitor {config.Monitor.DeviceName} failed validation after optimization");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize monitor configurations");
                var result = new ValidationResult();
                result.AddError($"Optimization failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates all monitor configurations
        /// </summary>
        public async Task<ValidationResult> ValidateAllConfigurationsAsync()
        {
            try
            {
                _logger.LogInformation("Validating all monitor configurations");

                var result = new ValidationResult();
                var currentMonitors = _systemHelper.GetMonitorInfo();

                foreach (var config in _monitorConfigurations)
                {
                    // Check if monitor is still connected
                    var monitorExists = currentMonitors.Any(m => m.DeviceName == config.Monitor.DeviceName);
                    if (!monitorExists)
                    {
                        result.AddWarning($"Monitor {config.Monitor.DeviceName} is no longer connected");
                        continue;
                    }

                    // Validate video file exists
                    if (!System.IO.File.Exists(config.VideoSettings.VideoFilePath))
                    {
                        result.AddError($"Video file not found: {config.VideoSettings.VideoFilePath}");
                        continue;
                    }

                    // Validate monitor bounds
                    if (config.Monitor.Bounds.Width <= 0 || config.Monitor.Bounds.Height <= 0)
                    {
                        result.AddError($"Invalid monitor bounds for {config.Monitor.DeviceName}");
                        continue;
                    }

                    result.ValidConfigurations.Add(config);
                }

                _logger.LogInformation("Validated {ValidCount} of {TotalCount} monitor configurations",
                    result.ValidConfigurations.Count, _monitorConfigurations.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate monitor configurations");
                var result = new ValidationResult();
                result.AddError($"Validation failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Creates an optimized configuration for a monitor
        /// </summary>
        public Models.MonitorConfiguration CreateOptimizedConfiguration(MonitorInfo monitor, VideoLockScreenSettings settings)
        {
            return new Models.MonitorConfiguration
            {
                Monitor = monitor,
                VideoSettings = settings,
                RenderingMode = DetermineOptimalRenderingMode(monitor),
                IsEnabled = true,
                Priority = monitor.IsPrimary ? 1 : 0
            };
        }

        /// <summary>
        /// Validates a monitor configuration
        /// </summary>
        public bool ValidateConfiguration(Models.MonitorConfiguration config)
        {
            if (config?.Monitor == null || config.VideoSettings == null)
                return false;

            if (string.IsNullOrEmpty(config.VideoSettings.VideoFilePath))
                return false;

            if (!System.IO.File.Exists(config.VideoSettings.VideoFilePath))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Video performance optimization modes
    /// </summary>
    public enum VideoPerformanceMode
    {
        Battery,
        Balanced,
        Quality
    }

    /// <summary>
    /// Validation result for monitor configurations
    /// </summary>
    public class ValidationResult
    {
        public List<MonitorConfiguration> ValidConfigurations { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public bool IsValid => Errors.Count == 0 && ValidConfigurations.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;

        public void AddWarning(string warning) => Warnings.Add(warning);
        public void AddError(string error) => Errors.Add(error);
    }
}