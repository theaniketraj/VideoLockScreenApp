using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace VideoLockScreen.Core.Services
{
    /// <summary>
    /// Manages Windows lock screen replacement at the system level
    /// This is the PROPER way to replace the Windows lock screen
    /// </summary>
    public class WindowsLockScreenManager
    {
    private readonly ILogger<WindowsLockScreenManager> _logger;
    private readonly IVideoFrameExtractor _videoFrameExtractor;
        
        // Registry paths for lock screen configuration
        private const string PERSONALIZATION_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
        private const string LOCK_SCREEN_IMAGE_PATH = "LockScreenImagePath";
        private const string LOCK_SCREEN_IMAGE_URL = "LockScreenImageUrl";
        
        public WindowsLockScreenManager(
            ILogger<WindowsLockScreenManager> logger,
            IVideoFrameExtractor videoFrameExtractor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _videoFrameExtractor = videoFrameExtractor ?? throw new ArgumentNullException(nameof(videoFrameExtractor));
        }

        /// <summary>
        /// Sets a video as the lock screen background (converts to image frames)
        /// </summary>
        public async Task<bool> SetVideoLockScreenAsync(string videoPath)
        {
            try
            {
                if (!File.Exists(videoPath))
                {
                    _logger.LogError("Video file not found: {VideoPath}", videoPath);
                    return false;
                }

                _logger.LogInformation("Setting video as lock screen: {VideoPath}", videoPath);

                // Extract first frame as static image for lock screen
                var frameResult = await _videoFrameExtractor.ExtractFrameAsync(videoPath);
                if (!frameResult.Success || string.IsNullOrWhiteSpace(frameResult.ImagePath))
                {
                    _logger.LogError("Failed to prepare lock screen image for video: {VideoPath}. Reason: {Reason}",
                        videoPath,
                        frameResult.ErrorMessage ?? "unknown error");
                    return false;
                }

                if (frameResult.IsPlaceholder)
                {
                    _logger.LogWarning("Using placeholder image for lock screen because: {Reason}", frameResult.ErrorMessage);
                }

                // Set the extracted frame as Windows lock screen
                return SetLockScreenImage(frameResult.ImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set video lock screen");
                return false;
            }
        }

        /// <summary>
        /// Sets a static image as the Windows lock screen background
        /// </summary>
        private bool SetLockScreenImage(string imagePath)
        {
            try
            {
                _logger.LogInformation("Setting lock screen image: {ImagePath}", imagePath);

                // Method 1: PersonalizationCSP registry (Windows 10/11)
                using (var key = Registry.LocalMachine.CreateSubKey(PERSONALIZATION_KEY))
                {
                    if (key != null)
                    {
                        key.SetValue(LOCK_SCREEN_IMAGE_PATH, imagePath, RegistryValueKind.String);
                        key.SetValue(LOCK_SCREEN_IMAGE_URL, $"file:///{imagePath.Replace('\\', '/')}", RegistryValueKind.String);
                        _logger.LogInformation("Lock screen image set via PersonalizationCSP registry");
                        return true;
                    }
                }

                // Method 2: Group Policy approach (fallback)
                return SetLockScreenViaGroupPolicy(imagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set lock screen image via registry");
                return false;
            }
        }

        /// <summary>
        /// Sets lock screen via Group Policy registry entries
        /// </summary>
        private bool SetLockScreenViaGroupPolicy(string imagePath)
        {
            try
            {
                const string gpKey = @"SOFTWARE\Policies\Microsoft\Windows\Personalization";
                
                using (var key = Registry.LocalMachine.CreateSubKey(gpKey))
                {
                    if (key != null)
                    {
                        key.SetValue("LockScreenImage", imagePath, RegistryValueKind.String);
                        key.SetValue("NoLockScreen", 0, RegistryValueKind.DWord);
                        key.SetValue("NoChangingLockScreen", 0, RegistryValueKind.DWord);
                        
                        _logger.LogInformation("Lock screen image set via Group Policy registry");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set lock screen via Group Policy");
                return false;
            }
        }

        /// <summary>
        /// Restores the default Windows lock screen
        /// </summary>
        public bool RestoreDefaultLockScreen()
        {
            try
            {
                _logger.LogInformation("Restoring default Windows lock screen");

                // Remove PersonalizationCSP entries
                using (var key = Registry.LocalMachine.OpenSubKey(PERSONALIZATION_KEY, true))
                {
                    key?.DeleteValue(LOCK_SCREEN_IMAGE_PATH, false);
                    key?.DeleteValue(LOCK_SCREEN_IMAGE_URL, false);
                }

                // Remove Group Policy entries
                const string gpKey = @"SOFTWARE\Policies\Microsoft\Windows\Personalization";
                using (var key = Registry.LocalMachine.OpenSubKey(gpKey, true))
                {
                    key?.DeleteValue("LockScreenImage", false);
                    key?.DeleteValue("NoLockScreen", false);
                    key?.DeleteValue("NoChangingLockScreen", false);
                }

                _logger.LogInformation("Default Windows lock screen restored");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore default lock screen");
                return false;
            }
        }
    }
}