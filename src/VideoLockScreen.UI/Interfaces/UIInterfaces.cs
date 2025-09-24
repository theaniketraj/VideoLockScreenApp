namespace VideoLockScreen.UI.Interfaces
{
    public interface ISystemTrayService
    {
        void Initialize();
        void ShowInTray();
        void HideFromTray();
        void ShowNotification(string title, string message);
        void UpdateStatus(string status);
        event EventHandler? TrayIconClicked;
        event EventHandler? ExitRequested;
    }

    public interface IDialogService
    {
        Task<IEnumerable<string>?> SelectVideoFilesAsync();
        Task<string?> SelectFolderAsync();
        Task ShowMessageAsync(string title, string message);
        Task<bool> ShowConfirmationAsync(string title, string message);
        void ShowSettings();
        void ShowAbout();
    }

    public interface IVideoPreviewService
    {
        void LoadVideo(string filePath);
        void Play();
        void Pause();
        void Stop();
        void SetVolume(double volume);
        bool IsPlaying { get; }
        string? CurrentVideoPath { get; }
        double Volume { get; }
        event EventHandler<string>? VideoLoaded;
        event EventHandler? PlaybackStarted;
        event EventHandler? PlaybackPaused;
        event EventHandler? PlaybackStopped;
    }

    public interface IThemeService
    {
        void ApplyTheme(string themeName);
        IEnumerable<string> GetAvailableThemes();
        string CurrentTheme { get; }
        event EventHandler<string>? ThemeChanged;
    }
}