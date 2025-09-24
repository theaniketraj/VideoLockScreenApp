using System.ComponentModel;
using System.IO;

namespace VideoLockScreen.Core.Models
{
    /// <summary>
    /// Configuration settings for the video lock screen application
    /// </summary>
    public class VideoLockScreenSettings : INotifyPropertyChanged
    {
        private string _videoFilePath = string.Empty;
        private bool _isEnabled = true;
        private bool _audioEnabled = false;
        private double _volume = 0.5;
        private VideoScalingMode _scalingMode = VideoScalingMode.Stretch;
        private int _loopCount = -1; // -1 = infinite loop
        private bool _showOnAllMonitors = true;
        private TimeSpan _fadeInDuration = TimeSpan.FromMilliseconds(500);
        private TimeSpan _fadeOutDuration = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Path to the video file to be used as lock screen wallpaper
        /// </summary>
        public string VideoFilePath
        {
            get => _videoFilePath;
            set
            {
                if (_videoFilePath != value)
                {
                    _videoFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether the video lock screen is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether audio should be played with the video
        /// </summary>
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                if (_audioEnabled != value)
                {
                    _audioEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Volume level (0.0 to 1.0)
        /// </summary>
        public double Volume
        {
            get => _volume;
            set
            {
                var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_volume - clampedValue) > 0.001)
                {
                    _volume = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// How the video should be scaled to fit the screen
        /// </summary>
        public VideoScalingMode ScalingMode
        {
            get => _scalingMode;
            set
            {
                if (_scalingMode != value)
                {
                    _scalingMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Number of times to loop the video (-1 for infinite)
        /// </summary>
        public int LoopCount
        {
            get => _loopCount;
            set
            {
                if (_loopCount != value)
                {
                    _loopCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether to show the video on all monitors or just the primary
        /// </summary>
        public bool ShowOnAllMonitors
        {
            get => _showOnAllMonitors;
            set
            {
                if (_showOnAllMonitors != value)
                {
                    _showOnAllMonitors = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Duration for fade-in effect when video starts
        /// </summary>
        public TimeSpan FadeInDuration
        {
            get => _fadeInDuration;
            set
            {
                if (_fadeInDuration != value)
                {
                    _fadeInDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Duration for fade-out effect when video ends
        /// </summary>
        public TimeSpan FadeOutDuration
        {
            get => _fadeOutDuration;
            set
            {
                if (_fadeOutDuration != value)
                {
                    _fadeOutDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Validates the current settings
        /// </summary>
        /// <returns>List of validation errors, empty if valid</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (IsEnabled)
            {
                if (string.IsNullOrWhiteSpace(VideoFilePath))
                {
                    errors.Add("Video file path is required when enabled");
                }
                else if (!File.Exists(VideoFilePath))
                {
                    errors.Add($"Video file not found: {VideoFilePath}");
                }

                if (Volume < 0 || Volume > 1)
                {
                    errors.Add("Volume must be between 0 and 1");
                }

                if (FadeInDuration < TimeSpan.Zero)
                {
                    errors.Add("Fade-in duration cannot be negative");
                }

                if (FadeOutDuration < TimeSpan.Zero)
                {
                    errors.Add("Fade-out duration cannot be negative");
                }
            }

            return errors;
        }

        /// <summary>
        /// Creates a deep copy of the settings
        /// </summary>
        /// <returns>Cloned settings object</returns>
        public VideoLockScreenSettings Clone()
        {
            return new VideoLockScreenSettings
            {
                VideoFilePath = this.VideoFilePath,
                IsEnabled = this.IsEnabled,
                AudioEnabled = this.AudioEnabled,
                Volume = this.Volume,
                ScalingMode = this.ScalingMode,
                LoopCount = this.LoopCount,
                ShowOnAllMonitors = this.ShowOnAllMonitors,
                FadeInDuration = this.FadeInDuration,
                FadeOutDuration = this.FadeOutDuration
            };
        }
    }

    /// <summary>
    /// Defines how video should be scaled to fit the screen
    /// </summary>
    public enum VideoScalingMode
    {
        /// <summary>
        /// Stretch video to fill entire screen (may distort aspect ratio)
        /// </summary>
        Stretch,

        /// <summary>
        /// Scale video uniformly to fit within screen bounds (maintains aspect ratio)
        /// </summary>
        Uniform,

        /// <summary>
        /// Scale video uniformly to fill screen (maintains aspect ratio, may crop)
        /// </summary>
        UniformToFill,

        /// <summary>
        /// Display video at original size (may be larger or smaller than screen)
        /// </summary>
        None
    }
}