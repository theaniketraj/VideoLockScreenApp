using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace VideoLockScreen.UI.Views
{
    public partial class VideoPreviewWindow : Window
    {
        private readonly DispatcherTimer _progressTimer;
        private bool _isUserDragging = false;
        
        public VideoPreviewWindow()
        {
            InitializeComponent();
            
            // Initialize progress timer
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _progressTimer.Tick += ProgressTimer_Tick;
        }
        
        public void LoadVideo(string videoPath)
        {
            try
            {
                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    ShowError($"Video file not found: {videoPath}");
                    return;
                }
                
                ShowLoading();
                
                // Set video source
                VideoPlayer.Source = new Uri(videoPath, UriKind.Absolute);
                VideoPlayer.Volume = VolumeSlider.Value;
                
                Title = $"Video Preview - {Path.GetFileName(videoPath)}";
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load video: {ex.Message}");
            }
        }
        
        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                HideLoading();
                HideError();
                
                if (VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    var duration = VideoPlayer.NaturalDuration.TimeSpan;
                    DurationText.Text = FormatTime(duration);
                    ProgressSlider.Maximum = duration.TotalSeconds;
                    
                    PlayButton.IsEnabled = true;
                }
                else
                {
                    ShowError("Unable to determine video duration");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Media opened error: {ex.Message}");
            }
        }
        
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            StopPlayback();
        }
        
        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ShowError($"Media failed to load: {e.ErrorException?.Message ?? "Unknown error"}");
        }
        
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback();
        }
        
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            PausePlayback();
        }
        
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VideoPlayer != null)
            {
                VideoPlayer.Volume = e.NewValue;
            }
        }
        
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUserDragging && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                VideoPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }
        
        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isUserDragging && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = VideoPlayer.Position.TotalSeconds;
                CurrentTimeText.Text = FormatTime(VideoPlayer.Position);
            }
        }
        
        private void StartPlayback()
        {
            try
            {
                VideoPlayer.Play();
                _progressTimer.Start();
                
                PlayButton.IsEnabled = false;
                PauseButton.IsEnabled = true;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to start playback: {ex.Message}");
            }
        }
        
        private void PausePlayback()
        {
            try
            {
                VideoPlayer.Pause();
                _progressTimer.Stop();
                
                PlayButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to pause playback: {ex.Message}");
            }
        }
        
        private void StopPlayback()
        {
            try
            {
                VideoPlayer.Stop();
                _progressTimer.Stop();
                
                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "00:00";
                
                PlayButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                StopButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to stop playback: {ex.Message}");
            }
        }
        
        private void ShowLoading()
        {
            LoadingText.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
        }
        
        private void HideLoading()
        {
            LoadingText.Visibility = Visibility.Collapsed;
        }
        
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            LoadingText.Visibility = Visibility.Collapsed;
        }
        
        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
        
        private string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            StopPlayback();
            _progressTimer?.Stop();
            VideoPlayer.Close();
            base.OnClosing(e);
        }
        
        // Handle mouse events for progress slider
        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source == ProgressSlider)
            {
                _isUserDragging = true;
            }
            base.OnPreviewMouseLeftButtonDown(e);
        }
        
        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isUserDragging)
            {
                _isUserDragging = false;
            }
            base.OnPreviewMouseLeftButtonUp(e);
        }
    }
}