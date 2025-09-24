using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using VideoLockScreen.UI.Interfaces;
using VideoLockScreen.UI.Views;

namespace VideoLockScreen.UI.Services
{
    public class SystemTrayService : ISystemTrayService, IDisposable
    {
        private TaskbarIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private bool _disposed = false;

        public event EventHandler? TrayIconClicked;
        public event EventHandler? ExitRequested;

        public void Initialize()
        {
            _trayIcon = new TaskbarIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                ToolTipText = "Video Lock Screen"
            };

            // Create context menu
            var contextMenu = new ContextMenu();
            
            var showMenuItem = new MenuItem { Header = "Show Configuration" };
            showMenuItem.Click += (s, e) => TrayIconClicked?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(showMenuItem);

            contextMenu.Items.Add(new Separator());

            var enableMenuItem = new MenuItem { Header = "Enable Lock Screen" };
            enableMenuItem.Click += EnableMenuItem_Click;
            contextMenu.Items.Add(enableMenuItem);

            var disableMenuItem = new MenuItem { Header = "Disable Lock Screen" };
            disableMenuItem.Click += DisableMenuItem_Click;
            contextMenu.Items.Add(disableMenuItem);

            contextMenu.Items.Add(new Separator());

            var exitMenuItem = new MenuItem { Header = "Exit" };
            exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(exitMenuItem);

            _trayIcon.ContextMenu = contextMenu;
            _trayIcon.TrayLeftMouseUp += (s, e) => TrayIconClicked?.Invoke(this, EventArgs.Empty);
        }

        public void ShowInTray()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Visible;
            }
        }

        public void HideFromTray()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Hidden;
            }
        }

        public void ShowNotification(string title, string message)
        {
            _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        public void UpdateStatus(string status)
        {
            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = $"Video Lock Screen - {status}";
            }
        }

        private void EnableMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            // This would communicate with the main application to enable the lock screen
            ShowNotification("Video Lock Screen", "Lock screen enabled");
        }

        private void DisableMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            // This would communicate with the main application to disable the lock screen
            ShowNotification("Video Lock Screen", "Lock screen disabled");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
                _disposed = true;
            }
        }
    }
}