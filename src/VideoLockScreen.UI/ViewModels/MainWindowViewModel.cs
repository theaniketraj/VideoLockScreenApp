using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Models;
using VideoLockScreen.Core.Utilities;
using VideoLockScreen.UI.Commands;
using VideoLockScreen.UI.Interfaces;
using VideoLockScreen.UI.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace VideoLockScreen.UI.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationService _configurationService;
        private readonly IVideoPlayerService _videoPlayerService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly IDialogService _dialogService;
        private readonly IVideoPreviewService _videoPreviewService;
        private readonly SessionMonitor _sessionMonitor;
        private readonly VideoFileHelper _videoFileHelper;
        
        private bool _isEnabled;
        private string _statusMessage = "Ready";
        private VideoFileModel? _selectedVideo;
        private string? _selectedVideoPath;
        private bool _showOnAllMonitors = true;
        private string _scalingMode = "Stretch to Fill";
        private int _loopCount = -1;
        private bool _audioEnabled = false;
        private double _volume = 0.5;

        public MainWindowViewModel(
            IConfigurationService configurationService,
            IVideoPlayerService videoPlayerService,
            ISystemTrayService systemTrayService,
            IDialogService dialogService,
            IVideoPreviewService videoPreviewService,
            SessionMonitor sessionMonitor,
            VideoFileHelper videoFileHelper)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _videoPlayerService = videoPlayerService ?? throw new ArgumentNullException(nameof(videoPlayerService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _videoPreviewService = videoPreviewService ?? throw new ArgumentNullException(nameof(videoPreviewService));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));
            _videoFileHelper = videoFileHelper ?? throw new ArgumentNullException(nameof(videoFileHelper));

            InitializeCollections();
            InitializeCommands();
            LoadConfiguration();
            LoadSystemInfo();
        }

        #region Properties

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    StatusMessage = value ? "Video Lock Screen is enabled" : "Video Lock Screen is disabled";
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<VideoFileModel> VideoFiles { get; } = new();
        public ObservableCollection<MonitorConfigurationModel> ConnectedMonitors { get; } = new();
        public ObservableCollection<SystemInfoModel> SystemInfo { get; } = new();

        public VideoFileModel? SelectedVideo
        {
            get => _selectedVideo;
            set
            {
                if (SetProperty(ref _selectedVideo, value))
                {
                    SelectedVideoPath = value?.FilePath;
                }
            }
        }

        public string? SelectedVideoPath
        {
            get => _selectedVideoPath;
            set => SetProperty(ref _selectedVideoPath, value);
        }

        public bool ShowOnAllMonitors
        {
            get => _showOnAllMonitors;
            set => SetProperty(ref _showOnAllMonitors, value);
        }

        public string ScalingMode
        {
            get => _scalingMode;
            set => SetProperty(ref _scalingMode, value);
        }

        public int LoopCount
        {
            get => _loopCount;
            set
            {
                if (SetProperty(ref _loopCount, value))
                {
                    OnPropertyChanged(nameof(LoopCountText));
                }
            }
        }

        public string LoopCountText => LoopCount == -1 ? "Infinite" : LoopCount.ToString();

        public bool AudioEnabled
        {
            get => _audioEnabled;
            set => SetProperty(ref _audioEnabled, value);
        }

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        // Service Status Properties
        public string ServiceStatusText => _sessionMonitor.IsMonitoring ? "Service Running" : "Service Stopped";
        public Brush ServiceStatusColor => _sessionMonitor.IsMonitoring ? Brushes.Green : Brushes.Red;
        public string ServiceActionText => _sessionMonitor.IsMonitoring ? "Stop Service" : "Start Service";

        #endregion

        #region Commands

        public ICommand ToggleEnabledCommand { get; private set; } = null!;
        public ICommand OpenSettingsCommand { get; private set; } = null!;
        public ICommand BrowseVideosCommand { get; private set; } = null!;
        public ICommand AddFolderCommand { get; private set; } = null!;
        public ICommand RemoveVideoCommand { get; private set; } = null!;
        public ICommand PreviewVideoCommand { get; private set; } = null!;
        public ICommand PlayPreviewCommand { get; private set; } = null!;
        public ICommand PausePreviewCommand { get; private set; } = null!;
        public ICommand StopPreviewCommand { get; private set; } = null!;
        public ICommand TestLockScreenCommand { get; private set; } = null!;
        public ICommand ApplySettingsCommand { get; private set; } = null!;
        public ICommand ServiceActionCommand { get; private set; } = null!;
        public ICommand MinimizeToTrayCommand { get; private set; } = null!;
        public ICommand CloseCommand { get; private set; } = null!;

        #endregion

        #region Initialization

        private void InitializeCollections()
        {
            // This will be populated from configuration and system detection
        }

        private void InitializeCommands()
        {
            ToggleEnabledCommand = new RelayCommand(ToggleEnabled);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            BrowseVideosCommand = new RelayCommand(BrowseVideos);
            AddFolderCommand = new RelayCommand(AddFolder);
            RemoveVideoCommand = new RelayCommand<VideoFileModel>(RemoveVideo);
            PreviewVideoCommand = new RelayCommand<VideoFileModel>(PreviewVideo);
            PlayPreviewCommand = new RelayCommand(PlayPreview);
            PausePreviewCommand = new RelayCommand(PausePreview);
            StopPreviewCommand = new RelayCommand(StopPreview);
            TestLockScreenCommand = new RelayCommand(TestLockScreen);
            ApplySettingsCommand = new RelayCommand(ApplySettings);
            ServiceActionCommand = new RelayCommand(ServiceAction);
            MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
            CloseCommand = new RelayCommand(Close);
        }

        private async void LoadConfiguration()
        {
            try
            {
                await _configurationService.LoadSettingsAsync();
                var config = _configurationService.Settings;
                
                IsEnabled = config.IsEnabled;
                ShowOnAllMonitors = config.ShowOnAllMonitors;
                AudioEnabled = config.AudioEnabled;
                Volume = config.Volume;
                LoopCount = config.LoopCount;

                // Load video file
                VideoFiles.Clear();
                if (!string.IsNullOrEmpty(config.VideoFilePath) && File.Exists(config.VideoFilePath))
                {
                    var videoModel = await CreateVideoFileModel(config.VideoFilePath);
                    VideoFiles.Add(videoModel);
                }

                // Load monitor configurations
                await LoadMonitorConfigurations();

                StatusMessage = "Configuration loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading configuration: {ex.Message}";
            }
        }

        private Task LoadMonitorConfigurations()
        {
            // This would integrate with the monitor detection service
            // For now, we'll create sample monitor configurations
            ConnectedMonitors.Clear();
            
            // In a real implementation, this would come from the monitor service
            var monitors = new[]
            {
                new MonitorConfigurationModel 
                { 
                    DisplayName = "Primary Monitor", 
                    Resolution = "1920x1080",
                    IsEnabled = true 
                },
                new MonitorConfigurationModel 
                { 
                    DisplayName = "Secondary Monitor", 
                    Resolution = "1440x900",
                    IsEnabled = false 
                }
            };

            foreach (var monitor in monitors)
            {
                ConnectedMonitors.Add(monitor);
            }
            
            return Task.CompletedTask;
        }

        private void LoadSystemInfo()
        {
            SystemInfo.Clear();
            SystemInfo.Add(new SystemInfoModel { Key = "OS Version", Value = Environment.OSVersion.ToString() });
            SystemInfo.Add(new SystemInfoModel { Key = "Machine Name", Value = Environment.MachineName });
            SystemInfo.Add(new SystemInfoModel { Key = "User Name", Value = Environment.UserName });
            SystemInfo.Add(new SystemInfoModel { Key = "Working Set", Value = $"{Environment.WorkingSet / 1024 / 1024} MB" });
            SystemInfo.Add(new SystemInfoModel { Key = "Processor Count", Value = Environment.ProcessorCount.ToString() });
        }

        private async Task<VideoFileModel> CreateVideoFileModel(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            
            try
            {
                // Get video metadata using VideoFileHelper on background thread
                var videoInfo = await Task.Run(async () => await _videoFileHelper.GetVideoInfoAsync(filePath));
                
                return new VideoFileModel
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    Duration = FormatDuration(videoInfo.Duration),
                    Resolution = $"{videoInfo.Width}x{videoInfo.Height}",
                    FileSize = fileInfo.Length,
                    ThumbnailPath = null, // TODO: Generate thumbnail
                    DateAdded = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                // Log the error (for now just create a debug output)
                System.Diagnostics.Debug.WriteLine($"Failed to get video info for {filePath}: {ex.Message}");
                
                // Fallback to basic file info if video analysis fails
                return new VideoFileModel
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    Duration = "Unknown",
                    Resolution = "Unknown",
                    FileSize = fileInfo.Length,
                    ThumbnailPath = null,
                    DateAdded = DateTime.Now
                };
            }
        }
        
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return duration.ToString(@"h\:mm\:ss");
            else
                return duration.ToString(@"m\:ss");
        }

        #endregion

        #region Command Implementations

        private void ToggleEnabled()
        {
            try
            {
                IsEnabled = !IsEnabled;
                // Save configuration change
                _ = Task.Run(SaveConfiguration);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling enabled state: {ex.Message}";
            }
        }

        private void OpenSettings()
        {
            try
            {
                _dialogService.ShowSettings();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private async void BrowseVideos()
        {
            try
            {
                StatusMessage = "Selecting video files...";
                var videoFiles = await _dialogService.SelectVideoFilesAsync();
                
                if (videoFiles?.Any() == true)
                {
                    StatusMessage = "Processing selected videos...";
                    int addedCount = 0;
                    int skippedCount = 0;
                    var validationErrors = new List<string>();

                    foreach (var filePath in videoFiles)
                    {
                        if (!VideoFiles.Any(v => v.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                // Validate the video file first
                                var validation = await _videoFileHelper.ValidateVideoForLockScreenAsync(filePath);
                                
                                if (validation.IsValid)
                                {
                                    var videoModel = await CreateVideoFileModel(filePath);
                                    VideoFiles.Add(videoModel);
                                    addedCount++;
                                }
                                else
                                {
                                    var fileName = Path.GetFileName(filePath);
                                    validationErrors.Add($"{fileName}: {string.Join(", ", validation.Issues)}");
                                    skippedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                var fileName = Path.GetFileName(filePath);
                                validationErrors.Add($"{fileName}: {ex.Message}");
                                skippedCount++;
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    
                    // Update status message with results
                    if (addedCount > 0)
                    {
                        StatusMessage = $"Added {addedCount} video file(s)";
                        if (skippedCount > 0)
                        {
                            StatusMessage += $", skipped {skippedCount}";
                        }
                        await SaveConfiguration();
                    }
                    else
                    {
                        StatusMessage = $"No valid videos added. Skipped {skippedCount} file(s)";
                    }
                    
                    // Show validation errors if any
                    if (validationErrors.Any())
                    {
                        var errorMessage = "Some files could not be added:\n\n" + string.Join("\n", validationErrors.Take(5));
                        if (validationErrors.Count > 5)
                        {
                            errorMessage += $"\n... and {validationErrors.Count - 5} more.";
                        }
                        await _dialogService.ShowMessageAsync("Video Validation", errorMessage);
                    }
                }
                else
                {
                    StatusMessage = "No files selected";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error browsing videos: {ex.Message}";
                await _dialogService.ShowMessageAsync("Error", $"Failed to browse videos: {ex.Message}");
            }
        }

        private async void AddFolder()
        {
            try
            {
                StatusMessage = "Selecting folder...";
                var folderPath = await _dialogService.SelectFolderAsync();
                
                if (!string.IsNullOrEmpty(folderPath))
                {
                    StatusMessage = "Scanning folder for videos...";
                    var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".m4v", ".webm" };
                    var videoFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    if (!videoFiles.Any())
                    {
                        StatusMessage = "No video files found in the selected folder";
                        await _dialogService.ShowMessageAsync("No Videos Found", 
                            $"No supported video files were found in:\n{folderPath}");
                        return;
                    }

                    StatusMessage = $"Processing {videoFiles.Count} video files...";
                    int addedCount = 0;
                    int skippedCount = 0;
                    var validationErrors = new List<string>();

                    foreach (var filePath in videoFiles)
                    {
                        if (!VideoFiles.Any(v => v.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                // Validate the video file first
                                var validation = await _videoFileHelper.ValidateVideoForLockScreenAsync(filePath);
                                
                                if (validation.IsValid)
                                {
                                    var videoModel = await CreateVideoFileModel(filePath);
                                    VideoFiles.Add(videoModel);
                                    addedCount++;
                                }
                                else
                                {
                                    var fileName = Path.GetFileName(filePath);
                                    validationErrors.Add($"{fileName}: {string.Join(", ", validation.Issues)}");
                                    skippedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                var fileName = Path.GetFileName(filePath);
                                validationErrors.Add($"{fileName}: {ex.Message}");
                                skippedCount++;
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    
                    // Update status and save configuration
                    if (addedCount > 0)
                    {
                        StatusMessage = $"Added {addedCount} video file(s) from folder";
                        if (skippedCount > 0)
                        {
                            StatusMessage += $", skipped {skippedCount}";
                        }
                        await SaveConfiguration();
                    }
                    else
                    {
                        StatusMessage = $"No valid videos added from folder. Skipped {skippedCount} file(s)";
                    }
                    
                    // Show validation errors if any
                    if (validationErrors.Any())
                    {
                        var errorMessage = "Some files could not be added:\n\n" + string.Join("\n", validationErrors.Take(5));
                        if (validationErrors.Count > 5)
                        {
                            errorMessage += $"\n... and {validationErrors.Count - 5} more.";
                        }
                        await _dialogService.ShowMessageAsync("Video Validation", errorMessage);
                    }
                }
                else
                {
                    StatusMessage = "No folder selected";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding folder: {ex.Message}";
                await _dialogService.ShowMessageAsync("Error", $"Failed to add folder: {ex.Message}");
            }
        }

        private async void RemoveVideo(VideoFileModel? video)
        {
            if (video != null && VideoFiles.Contains(video))
            {
                VideoFiles.Remove(video);
                StatusMessage = $"Removed {video.FileName}";
                await SaveConfiguration();
            }
        }

        private void PreviewVideo(VideoFileModel? video)
        {
            if (video != null)
            {
                SelectedVideo = video;
                _videoPreviewService.LoadVideo(video.FilePath);
            }
        }

        private async void PlayPreview()
        {
            try
            {
                // If no video is loaded in preview service, load the currently selected video
                if (string.IsNullOrEmpty(_videoPreviewService.CurrentVideoPath))
                {
                    if (SelectedVideo != null)
                    {
                        _videoPreviewService.LoadVideo(SelectedVideo.FilePath);
                    }
                    else
                    {
                        await _dialogService.ShowMessageAsync("No Video Selected", "Please select a video file first.");
                        return;
                    }
                }
                
                _videoPreviewService.Play();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Preview Error", $"Failed to play video preview: {ex.Message}");
            }
        }

        private async void PausePreview()
        {
            try
            {
                // If no video is loaded in preview service, load the currently selected video first
                if (string.IsNullOrEmpty(_videoPreviewService.CurrentVideoPath))
                {
                    if (SelectedVideo != null)
                    {
                        _videoPreviewService.LoadVideo(SelectedVideo.FilePath);
                        return; // Don't pause immediately after loading
                    }
                    else
                    {
                        await _dialogService.ShowMessageAsync("No Video Selected", "Please select a video file first.");
                        return;
                    }
                }
                
                _videoPreviewService.Pause();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Preview Error", $"Failed to pause video preview: {ex.Message}");
            }
        }

        private async void StopPreview()
        {
            try
            {
                // If no video is loaded in preview service, load the currently selected video first
                if (string.IsNullOrEmpty(_videoPreviewService.CurrentVideoPath))
                {
                    if (SelectedVideo != null)
                    {
                        _videoPreviewService.LoadVideo(SelectedVideo.FilePath);
                        return; // Don't stop immediately after loading
                    }
                    else
                    {
                        await _dialogService.ShowMessageAsync("No Video Selected", "Please select a video file first.");
                        return;
                    }
                }
                
                _videoPreviewService.Stop();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Preview Error", $"Failed to stop video preview: {ex.Message}");
            }
        }

        private async void TestLockScreen()
        {
            try
            {
                StatusMessage = "Testing lock screen...";
                
                // This would trigger a test of the lock screen functionality
                // For now, we'll just show a message
                await _dialogService.ShowMessageAsync("Test Lock Screen", 
                    "Lock screen test would be triggered here. This functionality will simulate the lock screen experience.");
                
                StatusMessage = "Lock screen test completed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error testing lock screen: {ex.Message}";
            }
        }

        private async void ApplySettings()
        {
            try
            {
                await SaveConfiguration();
                StatusMessage = "Settings applied successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying settings: {ex.Message}";
            }
        }

        private void ServiceAction()
        {
            try
            {
                if (_sessionMonitor.IsMonitoring)
                {
                    _sessionMonitor.StopMonitoring();
                    StatusMessage = "Service stopped";
                }
                else
                {
                    _sessionMonitor.StartMonitoring();
                    StatusMessage = "Service started";
                }
                
                // Refresh service status display
                OnPropertyChanged(nameof(ServiceStatusText));
                OnPropertyChanged(nameof(ServiceStatusColor));
                OnPropertyChanged(nameof(ServiceActionText));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error managing service: {ex.Message}";
            }
        }

        private void MinimizeToTray()
        {
            _systemTrayService.ShowInTray();
        }

        private void Close()
        {
            // This would actually minimize to tray rather than close
            MinimizeToTray();
        }

        private async Task SaveConfiguration()
        {
            try
            {
                var config = _configurationService.Settings;
                
                config.IsEnabled = IsEnabled;
                config.VideoFilePath = VideoFiles.FirstOrDefault()?.FilePath ?? string.Empty;
                config.ShowOnAllMonitors = ShowOnAllMonitors;
                config.AudioEnabled = AudioEnabled;
                config.Volume = Volume;
                config.LoopCount = LoopCount;
                
                // Convert ScalingMode string to enum
                if (Enum.TryParse<VideoScalingMode>(ScalingMode.Replace(" ", ""), out var mode))
                {
                    config.ScalingMode = mode;
                }

                await _configurationService.SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving configuration: {ex.Message}";
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}