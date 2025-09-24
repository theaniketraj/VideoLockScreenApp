using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.Services
{
    /// <summary>
    /// Service for managing application configuration and settings
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the current settings
        /// </summary>
        VideoLockScreenSettings Settings { get; }

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task LoadSettingsAsync();

        /// <summary>
        /// Saves settings to storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task SaveSettingsAsync();

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Event fired when settings are changed
        /// </summary>
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
    }

    /// <summary>
    /// Implementation of configuration service using JSON file storage
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger<ConfigurationService> _logger;
        private readonly string _settingsFilePath;
        private VideoLockScreenSettings _settings;

        public VideoLockScreenSettings Settings => _settings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public ConfigurationService(ILogger<ConfigurationService> logger)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoLockScreen",
                "settings.json"
            );
            
            _settings = new VideoLockScreenSettings();
            _settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        /// <summary>
        /// Loads settings from the JSON file
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInformation("Settings file not found, using defaults: {FilePath}", _settingsFilePath);
                    await SaveSettingsAsync(); // Create default settings file
                    return;
                }

                _logger.LogDebug("Loading settings from: {FilePath}", _settingsFilePath);

                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var loadedSettings = JsonSerializer.Deserialize<VideoLockScreenSettings>(json, GetJsonOptions());

                if (loadedSettings != null)
                {
                    // Unsubscribe from old settings
                    _settings.PropertyChanged -= OnSettingsPropertyChanged;
                    
                    _settings = loadedSettings;
                    
                    // Subscribe to new settings
                    _settings.PropertyChanged += OnSettingsPropertyChanged;

                    _logger.LogInformation("Settings loaded successfully");
                    OnSettingsChanged(new SettingsChangedEventArgs(_settings, SettingsChangeType.Loaded));
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings from {FilePath}", _settingsFilePath);
                // Continue with default settings
            }
        }

        /// <summary>
        /// Saves current settings to the JSON file
        /// </summary>
        public async Task SaveSettingsAsync()
        {
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(_settings, GetJsonOptions());
                await File.WriteAllTextAsync(_settingsFilePath, json);

                _logger.LogDebug("Settings saved to: {FilePath}", _settingsFilePath);
                OnSettingsChanged(new SettingsChangedEventArgs(_settings, SettingsChangeType.Saved));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings to {FilePath}", _settingsFilePath);
                throw;
            }
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        public void ResetToDefaults()
        {
            _logger.LogInformation("Resetting settings to defaults");
            
            // Unsubscribe from old settings
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            
            _settings = new VideoLockScreenSettings();
            
            // Subscribe to new settings
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            
            OnSettingsChanged(new SettingsChangedEventArgs(_settings, SettingsChangeType.Reset));
        }

        /// <summary>
        /// Gets JSON serializer options for settings
        /// </summary>
        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new TimeSpanJsonConverter() }
            };
        }

        /// <summary>
        /// Handles property changes in settings
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _logger.LogDebug("Setting changed: {PropertyName}", e.PropertyName);
            OnSettingsChanged(new SettingsChangedEventArgs(_settings, SettingsChangeType.PropertyChanged, e.PropertyName));
        }

        /// <summary>
        /// Raises the SettingsChanged event
        /// </summary>
        protected virtual void OnSettingsChanged(SettingsChangedEventArgs e)
        {
            SettingsChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for settings changes
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        public VideoLockScreenSettings Settings { get; }
        public SettingsChangeType ChangeType { get; }
        public string? PropertyName { get; }

        public SettingsChangedEventArgs(VideoLockScreenSettings settings, SettingsChangeType changeType, string? propertyName = null)
        {
            Settings = settings;
            ChangeType = changeType;
            PropertyName = propertyName;
        }
    }

    /// <summary>
    /// Types of settings changes
    /// </summary>
    public enum SettingsChangeType
    {
        PropertyChanged,
        Loaded,
        Saved,
        Reset
    }

    /// <summary>
    /// Custom JSON converter for TimeSpan to handle serialization
    /// </summary>
    public class TimeSpanJsonConverter : System.Text.Json.Serialization.JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.GetString() is string value && TimeSpan.TryParse(value, out TimeSpan result))
            {
                return result;
            }
            return TimeSpan.Zero;
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}