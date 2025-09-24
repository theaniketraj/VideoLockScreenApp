using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoLockScreen.Core;
using VideoLockScreen.Core.Services;
using VideoLockScreen.Core.Models;
using VideoLockScreen.UI.Commands;
using VideoLockScreen.UI.Interfaces;
using VideoLockScreen.UI.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace VideoLockScreen.UI.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configurationService;
        private readonly VideoPlayerService _videoPlayerService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly IDialogService _dialogService;
        private readonly IVideoPreviewService _videoPreviewService;
        private readonly SessionMonitor _sessionMonitor;
        
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
            ConfigurationService configurationService,
            VideoPlayerService videoPlayerService,
            ISystemTrayService systemTrayService,
            IDialogService dialogService,
            IVideoPreviewService videoPreviewService,
            SessionMonitor sessionMonitor)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _videoPlayerService = videoPlayerService ?? throw new ArgumentNullException(nameof(videoPlayerService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _videoPreviewService = videoPreviewService ?? throw new ArgumentNullException(nameof(videoPreviewService));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));

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
        public string ServiceStatusText => _sessionMonitor.IsRunning ? "Service Running" : "Service Stopped";
        public Brush ServiceStatusColor => _sessionMonitor.IsRunning ? Brushes.Green : Brushes.Red;
        public string ServiceActionText => _sessionMonitor.IsRunning ? "Stop Service" : "Start Service";

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
                var config = await _configurationService.LoadConfigurationAsync();
                
                IsEnabled = config.IsEnabled;
                ShowOnAllMonitors = config.ShowOnAllMonitors;
                AudioEnabled = config.AudioEnabled;
                Volume = config.Volume;
                LoopCount = config.LoopCount;

                // Load video files
                VideoFiles.Clear();
                foreach (var videoPath in config.VideoFiles)
                {
                    if (File.Exists(videoPath))
                    {
                        var videoModel = await CreateVideoFileModel(videoPath);
                        VideoFiles.Add(videoModel);
                    }
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

        private async Task LoadMonitorConfigurations()
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
            
            return new VideoFileModel
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                Duration = "00:00:00", // Would be determined by media info
                Resolution = "Unknown", // Would be determined by media info
                ThumbnailPath = null // Would be generated
            };
        }

        #endregion

        #region Command Implementations

        private void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
            // Save configuration change
            _ = Task.Run(SaveConfiguration);
        }

        private void OpenSettings()
        {
            _dialogService.ShowSettings();
        }

        private async void BrowseVideos()
        {
            try
            {
                var videoFiles = await _dialogService.SelectVideoFilesAsync();
                if (videoFiles?.Any() == true)
                {
                    foreach (var filePath in videoFiles)
                    {
                        if (!VideoFiles.Any(v => v.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            var videoModel = await CreateVideoFileModel(filePath);
                            VideoFiles.Add(videoModel);
                        }
                    }
                    
                    StatusMessage = $"Added {videoFiles.Count()} video file(s)";
                    await SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error browsing videos: {ex.Message}";
            }
        }

        private async void AddFolder()
        {
            try
            {
                var folderPath = await _dialogService.SelectFolderAsync();
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" };
                    var videoFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    int addedCount = 0;
                    foreach (var filePath in videoFiles)
                    {
                        if (!VideoFiles.Any(v => v.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            var videoModel = await CreateVideoFileModel(filePath);
                            VideoFiles.Add(videoModel);
                            addedCount++;
                        }
                    }
                    
                    StatusMessage = $"Added {addedCount} video file(s) from folder";
                    await SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding folder: {ex.Message}";
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

        private void PlayPreview()
        {
            _videoPreviewService.Play();
        }

        private void PausePreview()
        {
            _videoPreviewService.Pause();
        }

        private void StopPreview()
        {
            _videoPreviewService.Stop();
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

        private async void ServiceAction()
        {
            try
            {
                if (_sessionMonitor.IsRunning)
                {
                    await _sessionMonitor.StopAsync();
                    StatusMessage = "Service stopped";
                }
                else
                {
                    await _sessionMonitor.StartAsync();
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
                var config = new LockScreenConfiguration
                {
                    IsEnabled = IsEnabled,
                    VideoFiles = VideoFiles.Select(v => v.FilePath).ToList(),
                    ShowOnAllMonitors = ShowOnAllMonitors,
                    AudioEnabled = AudioEnabled,
                    Volume = Volume,
                    LoopCount = LoopCount,
                    ScalingMode = ScalingMode
                };

                await _configurationService.SaveConfigurationAsync(config);
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