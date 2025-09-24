using VideoLockScreen.UI.Interfaces;

namespace VideoLockScreen.UI.Services
{
    public class VideoPreviewService : IVideoPreviewService
    {
        private string? _currentVideoPath;
        private bool _isPlaying;
        private double _volume = 0.5;

        public event EventHandler<string>? VideoLoaded;
        public event EventHandler? PlaybackStarted;
        public event EventHandler? PlaybackPaused;
        public event EventHandler? PlaybackStopped;

        public void LoadVideo(string filePath)
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

        public void SetVolume(double volume)
        {
            _volume = Math.Clamp(volume, 0.0, 1.0);
        }

        public bool IsPlaying => _isPlaying;
        public string? CurrentVideoPath => _currentVideoPath;
        public double Volume => _volume;
    }
}