using System.Windows;
using System.Windows.Interop;
using VideoLockScreen.UI.ViewModels;
using VideoLockScreen.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;

namespace VideoLockScreen.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IHotkeyService? _hotkeyService;

        public MainWindow()
        {
            InitializeComponent();
            
            // Get ViewModel from DI container
            DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
            
            // Handle window state changes for system tray
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize hotkey service when window is loaded
            try
            {
                _hotkeyService = App.Services.GetRequiredService<IHotkeyService>();
                var windowHandle = new WindowInteropHelper(this).Handle;
                _hotkeyService.RegisterHotkeys(windowHandle);
            }
            catch (Exception ex)
            {
                // Log error but don't prevent window from loading
                System.Diagnostics.Debug.WriteLine($"Failed to register hotkeys: {ex.Message}");
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Unregister hotkeys
            _hotkeyService?.UnregisterHotkeys();
            
            // Don't actually close, minimize to tray instead
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        public void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            Focus();
        }
    }
}