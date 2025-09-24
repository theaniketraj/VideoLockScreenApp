using System.IO;
using System.Windows;
using Microsoft.Win32;
using VideoLockScreen.UI.Interfaces;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace VideoLockScreen.UI.Services
{
    public class DialogService : IDialogService
    {
        public async Task<IEnumerable<string>?> SelectVideoFilesAsync()
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Video Files",
                    Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.m4v;*.webm|" +
                            "MP4 Files|*.mp4|" +
                            "AVI Files|*.avi|" +
                            "MKV Files|*.mkv|" +
                            "MOV Files|*.mov|" +
                            "WMV Files|*.wmv|" +
                            "All Files|*.*",
                    Multiselect = true,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    return openFileDialog.FileNames;
                }

                return null;
            });
        }

        public async Task<string?> SelectFolderAsync()
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Using a folder browser dialog would require additional NuGet packages
                // For now, we'll use a simplified approach with OpenFileDialog
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Folder Containing Videos",
                    Filter = "Folders|*.folder",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Select Folder"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    return System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                }

                return null;
            });
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            });
        }

        public void ShowSettings()
        {
            // This would open a settings dialog
            // For now, we'll show a placeholder message
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Settings dialog would open here.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public void ShowAbout()
        {
            var aboutMessage = @"Video Lock Screen Application
Version 1.0.0

A modern application to display videos on your lock screen.

Features:
• Multi-monitor support
• Video synchronization
• System tray integration
• Modern WPF interface

Built with .NET 8 and WPF";

            MessageBox.Show(aboutMessage, "About Video Lock Screen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}