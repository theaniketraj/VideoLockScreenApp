using System.IO;
using System.Windows;
using VideoLockScreen.UI.Interfaces;
using VideoLockScreen.UI.Views;

namespace VideoLockScreen.UI.Services
{
    public class VideoPreviewService : IVideoPreviewService
    {
        private string? _currentVideoPath;
        private bool _isPlaying;
        private double _volume = 0.5;
        private VideoPreviewWindow? _previewWindow;

        public event EventHandler<string>? VideoLoaded;
        public event EventHandler? PlaybackStarted;
        public event EventHandler? PlaybackPaused;
        public event EventHandler? PlaybackStopped;

        public void LoadVideo(string filePath)
        {
            LoadVideoInternal(filePath);
            
            // Show preview window
            ShowPreviewWindow();
        }
        
        private void LoadVideoInternal(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException($"Video file not found: {filePath}");
            }

            _currentVideoPath = filePath;
            _isPlaying = false;
            
            VideoLoaded?.Invoke(this, filePath);
        }

        public void Play()
        {
            if (string.IsNullOrEmpty(_currentVideoPath))
            {
                throw new InvalidOperationException("No video loaded");
            }

            ShowPreviewWindow();
            _isPlaying = true;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isPlaying = false;
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isPlaying = false;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
        
        private void ShowPreviewWindow()
        {
            if (_previewWindow == null || !_previewWindow.IsLoaded)
            {
                _previewWindow = new VideoPreviewWindow();
                _previewWindow.Owner = System.Windows.Application.Current.MainWindow;
                _previewWindow.Closed += (s, e) => _previewWindow = null;
            }
            
            if (!string.IsNullOrEmpty(_currentVideoPath))
            {
                _previewWindow.LoadVideo(_currentVideoPath);
            }
            
            if (!_previewWindow.IsVisible)
            {
                _previewWindow.Show();
            }
            
            _previewWindow.Activate();
        }

        public void SetVolume(double volume)
        {
            _volume = Math.Clamp(volume, 0.0, 1.0);
        }

        public bool IsPlaying => _isPlaying;
        public string? CurrentVideoPath => _currentVideoPath;
        public double Volume => _volume;
    }
}