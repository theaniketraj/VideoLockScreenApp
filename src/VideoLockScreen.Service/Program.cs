using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using VideoLockScreen.Core.DependencyInjection;
using VideoLockScreen.Core.Services;
using WpfApplication = System.Windows.Application;

namespace VideoLockScreen.Service
{
    /// <summary>
    /// Program entry point for Video Lock Screen Service
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Initialize WPF application for video playback
                var app = new WpfApplication();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Create and run the host
                var host = CreateHostBuilder(args).Build();
                
                // Start the host in a background task
                var hostTask = host.RunAsync();
                
                // Run WPF application
                app.Run();
                
                // Wait for host to complete
                await hostTask;
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Creates the host builder for the service
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Host builder</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "VideoLockScreenService";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Register core services
                    services.ConfigureCoreServices();
                    services.AddSingleton<LockScreenManager>();
                    
                    // Register the main service
                    services.AddHostedService<VideoLockScreenService>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.ClearProviders();
                    
                    // Add console logging for development
                    logging.AddConsole();
                    
                    // Add Windows event log for production
                    logging.AddEventLog(settings =>
                    {
                        settings.SourceName = "VideoLockScreenService";
                        settings.LogName = "Application";
                    });
                    
                    // Set logging levels
                    logging.SetMinimumLevel(LogLevel.Information);
                    
                    // Add debug logging in debug builds
#if DEBUG
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
#endif
                });
    }
}