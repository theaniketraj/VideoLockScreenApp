using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;
using WpfApplication = System.Windows.Application;

namespace VideoLockScreen.Core.Services
{
    /// <summary>
    /// Service for managing video playback functionality
    /// </summary>
    public interface IVideoPlayerService
    {
        /// <summary>
        /// Gets whether a video is currently playing
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Gets whether a video is currently loaded
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the current playback position
        /// </summary>
        TimeSpan Position { get; }

        /// <summary>
        /// Gets the duration of the current video
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Gets or sets the volume (0.0 to 1.0)
        /// </summary>
        double Volume { get; set; }

        /// <summary>
        /// Gets or sets whether the video is muted
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// Event fired when video playback ends
        /// </summary>
        event EventHandler? PlaybackEnded;

        /// <summary>
        /// Event fired when video fails to load or play
        /// </summary>
        event EventHandler<VideoErrorEventArgs>? PlaybackError;

        /// <summary>
        /// Event fired when video position changes
        /// </summary>
        event EventHandler<TimeSpan>? PositionChanged;

        /// <summary>
        /// Loads a video file
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>Task representing the async operation</returns>
        Task<bool> LoadVideoAsync(string filePath);

        /// <summary>
        /// Starts or resumes video playback
        /// </summary>
        void Play();

        /// <summary>
        /// Pauses video playback
        /// </summary>
        void Pause();

        /// <summary>
        /// Stops video playback and resets to beginning
        /// </summary>
        void Stop();

        /// <summary>
        /// Seeks to a specific position in the video
        /// </summary>
        /// <param name="position">Position to seek to</param>
        void Seek(TimeSpan position);

        /// <summary>
        /// Creates a MediaElement for displaying the video
        /// </summary>
        /// <returns>MediaElement configured for video playback</returns>
        MediaElement CreateMediaElement();

        /// <summary>
        /// Configures a MediaElement with current settings
        /// </summary>
        /// <param name="mediaElement">MediaElement to configure</param>
        /// <param name="settings">Settings to apply</param>
        void ConfigureMediaElement(MediaElement mediaElement, VideoLockScreenSettings settings);
    }

    /// <summary>
    /// Implementation of video player service using WPF MediaElement
    /// </summary>
    public class VideoPlayerService : IVideoPlayerService
    {
        private readonly ILogger<VideoPlayerService> _logger;
        private readonly DispatcherTimer _positionTimer;
        private string _currentFilePath = string.Empty;
        private MediaElement? _primaryMediaElement;
        private bool _isLoaded = false;
        private bool _isPlaying = false;
        private double _volume = 0.5;
        private bool _isMuted = false;
        private TimeSpan _duration = TimeSpan.Zero;

        public bool IsPlaying => _isPlaying;
        public bool IsLoaded => _isLoaded;
        public TimeSpan Position => _primaryMediaElement?.Position ?? TimeSpan.Zero;
        public TimeSpan Duration => _duration;

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0.0, Math.Min(1.0, value));
                UpdateVolumeOnMediaElements();
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                UpdateVolumeOnMediaElements();
            }
        }

        public event EventHandler? PlaybackEnded;
        public event EventHandler<VideoErrorEventArgs>? PlaybackError;
        public event EventHandler<TimeSpan>? PositionChanged;

        private readonly List<WeakReference<MediaElement>> _mediaElements = new();

        public VideoPlayerService(ILogger<VideoPlayerService> logger)
        {
            _logger = logger;
            
            // Timer to track playback position
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _positionTimer.Tick += OnPositionTimerTick;
        }

        /// <summary>
        /// Loads a video file for playback
        /// </summary>
        public async Task<bool> LoadVideoAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning("Video file not found: {FilePath}", filePath);
                    return false;
                }

                _logger.LogInformation("Loading video: {FilePath}", filePath);

                Stop(); // Stop current playback
                _currentFilePath = filePath;
                _isLoaded = false;

                // Update all media elements with new source
                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateMediaElementSources();
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading video: {FilePath}", filePath);
                OnPlaybackError(new VideoErrorEventArgs(ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// Starts video playback
        /// </summary>
        public void Play()
        {
            if (!_isLoaded || string.IsNullOrEmpty(_currentFilePath))
            {
                _logger.LogWarning("Cannot play - no video loaded");
                return;
            }

            try
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mediaElement in GetAliveMediaElements())
                    {
                        mediaElement.Play();
                    }
                });

                _isPlaying = true;
                _positionTimer.Start();
                _logger.LogDebug("Video playback started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting video playback");
                OnPlaybackError(new VideoErrorEventArgs(ex.Message, ex));
            }
        }

        /// <summary>
        /// Pauses video playback
        /// </summary>
        public void Pause()
        {
            try
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mediaElement in GetAliveMediaElements())
                    {
                        mediaElement.Pause();
                    }
                });

                _isPlaying = false;
                _positionTimer.Stop();
                _logger.LogDebug("Video playback paused");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing video playback");
            }
        }

        /// <summary>
        /// Stops video playback
        /// </summary>
        public void Stop()
        {
            try
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mediaElement in GetAliveMediaElements())
                    {
                        mediaElement.Stop();
                    }
                });

                _isPlaying = false;
                _positionTimer.Stop();
                _logger.LogDebug("Video playback stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping video playback");
            }
        }

        /// <summary>
        /// Seeks to a specific position
        /// </summary>
        public void Seek(TimeSpan position)
        {
            try
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var mediaElement in GetAliveMediaElements())
                    {
                        mediaElement.Position = position;
                    }
                });

                _logger.LogDebug("Seeked to position: {Position}", position);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeking to position: {Position}", position);
            }
        }

        /// <summary>
        /// Creates a new MediaElement configured for video playback
        /// </summary>
        public MediaElement CreateMediaElement()
        {
            var mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both
            };

            // Subscribe to events
            mediaElement.MediaOpened += OnMediaOpened;
            mediaElement.MediaEnded += OnMediaEnded;
            mediaElement.MediaFailed += OnMediaFailed;

            // Set source if we have one
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                mediaElement.Source = new Uri(_currentFilePath);
            }

            // Apply current volume settings
            mediaElement.Volume = _isMuted ? 0 : _volume;

            // Add to tracking list
            _mediaElements.Add(new WeakReference<MediaElement>(mediaElement));

            // Set as primary if we don't have one
            if (_primaryMediaElement == null)
            {
                _primaryMediaElement = mediaElement;
            }

            return mediaElement;
        }

        /// <summary>
        /// Configures a MediaElement with the specified settings
        /// </summary>
        public void ConfigureMediaElement(MediaElement mediaElement, VideoLockScreenSettings settings)
        {
            if (mediaElement == null) return;

            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                // Set stretch mode
                mediaElement.Stretch = settings.ScalingMode switch
                {
                    VideoScalingMode.Stretch => Stretch.Fill,
                    VideoScalingMode.Uniform => Stretch.Uniform,
                    VideoScalingMode.UniformToFill => Stretch.UniformToFill,
                    VideoScalingMode.None => Stretch.None,
                    _ => Stretch.Uniform
                };

                // Set volume
                mediaElement.Volume = settings.AudioEnabled && !_isMuted ? settings.Volume : 0;
            });
        }

        /// <summary>
        /// Updates the source on all media elements
        /// </summary>
        private void UpdateMediaElementSources()
        {
            foreach (var mediaElement in GetAliveMediaElements())
            {
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    mediaElement.Source = new Uri(_currentFilePath);
                }
                else
                {
                    mediaElement.Source = null;
                }
            }
        }

        /// <summary>
        /// Updates volume on all media elements
        /// </summary>
        private void UpdateVolumeOnMediaElements()
        {
            WpfApplication.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var mediaElement in GetAliveMediaElements())
                {
                    mediaElement.Volume = _isMuted ? 0 : _volume;
                }
            });
        }

        /// <summary>
        /// Gets all alive media elements from weak references
        /// </summary>
        private IEnumerable<MediaElement> GetAliveMediaElements()
        {
            var aliveElements = new List<MediaElement>();
            var deadReferences = new List<WeakReference<MediaElement>>();

            foreach (var weakRef in _mediaElements)
            {
                if (weakRef.TryGetTarget(out MediaElement? element))
                {
                    aliveElements.Add(element);
                }
                else
                {
                    deadReferences.Add(weakRef);
                }
            }

            // Clean up dead references
            foreach (var deadRef in deadReferences)
            {
                _mediaElements.Remove(deadRef);
            }

            return aliveElements;
        }

        /// <summary>
        /// Handles MediaOpened event
        /// </summary>
        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement mediaElement)
            {
                _duration = mediaElement.NaturalDuration.HasTimeSpan ? mediaElement.NaturalDuration.TimeSpan : TimeSpan.Zero;
                _isLoaded = true;
                _logger.LogInformation("Video loaded successfully. Duration: {Duration}", _duration);
            }
        }

        /// <summary>
        /// Handles MediaEnded event
        /// </summary>
        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            _positionTimer.Stop();
            _logger.LogDebug("Video playback ended");
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles MediaFailed event
        /// </summary>
        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _isPlaying = false;
            _isLoaded = false;
            _positionTimer.Stop();
            _logger.LogError("Video playback failed: {Error}", e.ErrorException?.Message);
            OnPlaybackError(new VideoErrorEventArgs(e.ErrorException?.Message ?? "Unknown media error", e.ErrorException));
        }

        /// <summary>
        /// Handles position timer tick
        /// </summary>
        private void OnPositionTimerTick(object? sender, EventArgs e)
        {
            if (_isPlaying && _primaryMediaElement != null)
            {
                PositionChanged?.Invoke(this, Position);
            }
        }

        /// <summary>
        /// Raises the PlaybackError event
        /// </summary>
        protected virtual void OnPlaybackError(VideoErrorEventArgs e)
        {
            PlaybackError?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for video playback errors
    /// </summary>
    public class VideoErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception? Exception { get; }

        public VideoErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }
}