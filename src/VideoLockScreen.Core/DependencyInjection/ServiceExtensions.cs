using Microsoft.Extensions.DependencyInjection;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Utilities;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring dependency injection
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Configures dependency injection for the Core library
        /// </summary>
        public static IServiceCollection ConfigureCoreServices(this IServiceCollection services)
        {
            // Register core services
            services.AddSingleton<SessionMonitor>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IVideoPlayerService, VideoPlayerService>();
            
            // Register advanced services
            services.AddSingleton<IVideoSynchronizationService, VideoSynchronizationService>();
            services.AddSingleton<ISystemIntegrationService, SystemIntegrationService>();
            services.AddSingleton<WindowsLockScreenManager>();
            
            // Register utilities
            services.AddSingleton<SystemHelper>();
            services.AddSingleton<VideoFileHelper>();
            services.AddSingleton<MonitorManager>();
            
            return services;
        }

        /// <summary>
        /// Configures services for kiosk mode operation
        /// </summary>
        public static IServiceCollection ConfigureKioskMode(this IServiceCollection services)
        {
            // Configure for kiosk mode with enhanced security
            services.Configure<VideoLockScreenSettings>(settings =>
            {
                settings.IsEnabled = true;
                // Additional kiosk mode settings can be added here
            });

            return services;
        }

        /// <summary>
        /// Configures services for development mode
        /// </summary>
        public static IServiceCollection ConfigureDevelopmentMode(this IServiceCollection services)
        {
            // Configure for development with relaxed security
            services.Configure<VideoLockScreenSettings>(settings =>
            {
                settings.IsEnabled = false;
                // Additional development mode settings can be added here
            });

            return services;
        }
    }
}