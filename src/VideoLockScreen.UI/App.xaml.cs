using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.DependencyInjection;
using VideoLockScreen.UI.ViewModels;
using VideoLockScreen.UI.Views;
using VideoLockScreen.UI.Services;
using VideoLockScreen.UI.Interfaces;
using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;

namespace VideoLockScreen.UI
{
    /// <summary>
    /// Main application class with dependency injection support
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        /// <summary>
        /// Gets the current service provider
        /// </summary>
        public static IServiceProvider Services => ((App)Current)._host?.Services ?? throw new InvalidOperationException("Services not available");

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Build the host with dependency injection
                _host = Host.CreateDefaultBuilder()
                    .ConfigureServices(ConfigureServices)
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    .Build();

                await _host.StartAsync();

                // Get the main window and show it
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }

        /// <summary>
        /// Configures the dependency injection container
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Register Core services
            services.ConfigureCoreServices();

            // Register UI services
            services.AddSingleton<ISystemTrayService, SystemTrayService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IVideoPreviewService, VideoPreviewService>();

            // Register ViewModels
            services.AddTransient<MainWindowViewModel>();

            // Register Views
            services.AddTransient<MainWindow>();

            // Register as singleton to maintain state
            services.AddSingleton<SystemTrayManager>();
        }

        /// <summary>
        /// Gets a service from the DI container
        /// </summary>
        public static T GetService<T>() where T : class
        {
            return Services.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets a service from the DI container (nullable)
        /// </summary>
        public static T? GetOptionalService<T>() where T : class
        {
            return Services.GetService<T>();
        }
    }
}