namespace VideoLockScreen.UI.Models
{
    public class VideoFileModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public long FileSize { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }

    public class MonitorConfigurationModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string? VideoFile { get; set; }
        public bool IsPrimary { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class SystemInfoModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}