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
        
        // Registry paths for lock screen configuration
        private const string PERSONALIZATION_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
        private const string LOCK_SCREEN_IMAGE_PATH = "LockScreenImagePath";
        private const string LOCK_SCREEN_IMAGE_URL = "LockScreenImageUrl";
        
        public WindowsLockScreenManager(ILogger<WindowsLockScreenManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                var lockScreenImagePath = await ExtractVideoFrameAsync(videoPath);
                if (string.IsNullOrEmpty(lockScreenImagePath))
                {
                    return false;
                }

                // Set the extracted frame as Windows lock screen
                return SetLockScreenImage(lockScreenImagePath);
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
        /// Extracts the first frame of a video as a static image for lock screen
        /// </summary>
        private async Task<string> ExtractVideoFrameAsync(string videoPath)
        {
            try
            {
                var outputDir = Path.Combine(Path.GetTempPath(), "VideoLockScreen");
                Directory.CreateDirectory(outputDir);
                
                var outputImagePath = Path.Combine(outputDir, "lockscreen_frame.jpg");
                
                // TODO: Use FFmpeg or similar to extract first frame
                // For now, create a placeholder approach
                _logger.LogWarning("Video frame extraction not yet implemented - using placeholder");
                
                // Create a simple colored image as placeholder
                await CreatePlaceholderImageAsync(outputImagePath);
                
                return outputImagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract video frame");
                return string.Empty;
            }
        }

        /// <summary>
        /// Creates a placeholder image (temporary solution)
        /// </summary>
        private async Task CreatePlaceholderImageAsync(string outputPath)
        {
            // This is a temporary placeholder - in production, you'd extract actual video frames
            try
            {
                using (var bitmap = new System.Drawing.Bitmap(1920, 1080))
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Black);
                    
                    using (var font = new System.Drawing.Font("Arial", 48, System.Drawing.FontStyle.Bold))
                    using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    {
                        var text = "Video Lock Screen Active";
                        var textSize = graphics.MeasureString(text, font);
                        var x = (bitmap.Width - textSize.Width) / 2;
                        var y = (bitmap.Height - textSize.Height) / 2;
                        
                        graphics.DrawString(text, font, brush, x, y);
                    }
                    
                    bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                
                _logger.LogInformation("Placeholder lock screen image created: {OutputPath}", outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create placeholder image");
                throw;
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