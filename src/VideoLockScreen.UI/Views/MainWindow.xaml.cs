using System.Windows;
using VideoLockScreen.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace VideoLockScreen.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Get ViewModel from DI container
            var app = (App)Application.Current;
            DataContext = app.ServiceProvider.GetRequiredService<MainWindowViewModel>();
            
            // Handle window state changes for system tray
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
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