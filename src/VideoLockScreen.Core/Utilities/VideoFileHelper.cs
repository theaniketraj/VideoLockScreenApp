using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.Utilities
{
    /// <summary>
    /// Utility class for video file operations and metadata extraction
    /// </summary>
    public class VideoFileHelper
    {
        private readonly ILogger<VideoFileHelper> _logger;

        public VideoFileHelper(ILogger<VideoFileHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets detailed information about a video file
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>VideoInfo object with detailed information</returns>
        public async Task<VideoInfo> GetVideoInfoAsync(string filePath)
        {
            var videoInfo = VideoInfo.FromFile(filePath);

            try
            {
                // Try to get additional metadata using FFProbe if available
                await EnrichVideoInfoWithFFProbe(videoInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get detailed video information for {FilePath}", filePath);
            }

            return videoInfo;
        }

        /// <summary>
        /// Validates if a video file is suitable for lock screen use
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>Validation result with details</returns>
        public async Task<VideoValidationResult> ValidateVideoForLockScreenAsync(string filePath)
        {
            var result = new VideoValidationResult { FilePath = filePath };

            try
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.Issues.Add("File does not exist");
                    return result;
                }

                if (!SupportedVideoFormats.IsSupportedFile(filePath))
                {
                    result.IsValid = false;
                    result.Issues.Add($"Unsupported file format: {Path.GetExtension(filePath)}");
                    return result;
                }

                var videoInfo = await GetVideoInfoAsync(filePath);
                result.VideoInfo = videoInfo;

                // Check file size (warn if too large)
                if (videoInfo.FileSizeBytes > 500 * 1024 * 1024) // 500MB
                {
                    result.Warnings.Add("Large file size may impact performance");
                }

                // Check duration
                if (videoInfo.Duration.TotalSeconds < 1)
                {
                    result.Issues.Add("Video is too short (minimum 1 second)");
                }
                else if (videoInfo.Duration.TotalMinutes > 10)
                {
                    result.Warnings.Add("Long video may impact performance and battery life");
                }

                // Check resolution
                if (videoInfo.Width > 0 && videoInfo.Height > 0)
                {
                    if (videoInfo.Width < 640 || videoInfo.Height < 480)
                    {
                        result.Warnings.Add("Low resolution may appear pixelated on high-DPI displays");
                    }
                    
                    if (videoInfo.Width > 3840 || videoInfo.Height > 2160)
                    {
                        result.Warnings.Add("Very high resolution may impact performance");
                    }
                }

                // Check if video track exists
                if (!videoInfo.HasVideo)
                {
                    result.Issues.Add("File does not contain a video track");
                }

                result.IsValid = result.Issues.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating video file: {FilePath}", filePath);
                result.IsValid = false;
                result.Issues.Add($"Error analyzing file: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Generates a thumbnail image from a video file
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <param name="timePosition">Time position to capture (default: 10% of duration)</param>
        /// <param name="maxWidth">Maximum width of thumbnail</param>
        /// <param name="maxHeight">Maximum height of thumbnail</param>
        /// <returns>Bitmap thumbnail or null if failed</returns>
        public async Task<Bitmap?> GenerateThumbnailAsync(string filePath, TimeSpan? timePosition = null, int maxWidth = 320, int maxHeight = 240)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                // For now, return a placeholder since thumbnail generation requires additional libraries
                // In a full implementation, you would use FFmpeg or MediaFoundation APIs
                _logger.LogDebug("Thumbnail generation requested for {FilePath}", filePath);
                
                // Create a simple placeholder thumbnail
                var thumbnail = new Bitmap(maxWidth, maxHeight);
                using (var graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.Clear(Color.DarkGray);
                    
                    using (var brush = new SolidBrush(Color.White))
                    using (var font = new Font("Arial", 12))
                    {
                        var text = Path.GetFileNameWithoutExtension(filePath);
                        var textSize = graphics.MeasureString(text, font);
                        var x = (maxWidth - textSize.Width) / 2;
                        var y = (maxHeight - textSize.Height) / 2;
                        
                        graphics.DrawString(text, font, brush, x, y);
                    }
                }

                return thumbnail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Gets the optimal playback settings for a video file based on its characteristics
        /// </summary>
        /// <param name="videoInfo">Video information</param>
        /// <returns>Recommended settings</returns>
        public VideoLockScreenSettings GetOptimalSettings(VideoInfo videoInfo)
        {
            var settings = new VideoLockScreenSettings();

            if (videoInfo == null)
                return settings;

            // Set optimal scaling based on resolution
            if (videoInfo.Width > 0 && videoInfo.Height > 0)
            {
                var aspectRatio = (double)videoInfo.Width / videoInfo.Height;
                var screenAspectRatio = 16.0 / 9.0; // Assume widescreen

                if (Math.Abs(aspectRatio - screenAspectRatio) < 0.1)
                {
                    settings.ScalingMode = VideoScalingMode.Stretch;
                }
                else
                {
                    settings.ScalingMode = VideoScalingMode.UniformToFill;
                }
            }

            // Disable audio for very short videos (likely GIFs converted to video)
            if (videoInfo.Duration.TotalSeconds < 5)
            {
                settings.AudioEnabled = false;
            }

            // Set reasonable volume
            settings.Volume = videoInfo.HasAudio ? 0.3 : 0.0;

            // Set loop count based on duration
            if (videoInfo.Duration.TotalSeconds < 30)
            {
                settings.LoopCount = -1; // Infinite loop for short videos
            }
            else
            {
                settings.LoopCount = 1; // Single playback for longer videos
            }

            return settings;
        }

        /// <summary>
        /// Enriches video info using FFProbe if available
        /// </summary>
        private async Task EnrichVideoInfoWithFFProbe(VideoInfo videoInfo)
        {
            try
            {
                // This is a placeholder for FFProbe integration
                // In a real implementation, you would:
                // 1. Check if FFProbe is available
                // 2. Execute FFProbe with appropriate parameters
                // 3. Parse the JSON output to extract detailed metadata
                
                _logger.LogDebug("Attempting to get detailed video info for {FilePath}", videoInfo.FilePath);
                
                // For now, we'll simulate some basic information
                var fileInfo = new FileInfo(videoInfo.FilePath);
                var extension = Path.GetExtension(videoInfo.FilePath).ToLowerInvariant();
                
                // Set some reasonable defaults based on file extension
                switch (extension)
                {
                    case ".mp4":
                        videoInfo.VideoCodec = "H.264";
                        videoInfo.AudioCodec = "AAC";
                        videoInfo.HasVideo = true;
                        videoInfo.HasAudio = true;
                        break;
                    case ".avi":
                        videoInfo.VideoCodec = "XVID";
                        videoInfo.AudioCodec = "MP3";
                        videoInfo.HasVideo = true;
                        videoInfo.HasAudio = true;
                        break;
                    case ".mov":
                        videoInfo.VideoCodec = "H.264";
                        videoInfo.AudioCodec = "AAC";
                        videoInfo.HasVideo = true;
                        videoInfo.HasAudio = true;
                        break;
                    default:
                        videoInfo.HasVideo = true;
                        videoInfo.HasAudio = false;
                        break;
                }

                // Estimate some properties if not set
                if (videoInfo.Width == 0 || videoInfo.Height == 0)
                {
                    videoInfo.Width = 1920;
                    videoInfo.Height = 1080;
                }

                if (videoInfo.Duration == TimeSpan.Zero)
                {
                    // Estimate based on file size (very rough)
                    var estimatedSeconds = Math.Max(1, videoInfo.FileSizeBytes / (1024 * 1024)); // 1MB per second estimate
                    videoInfo.Duration = TimeSpan.FromSeconds(Math.Min(estimatedSeconds, 300)); // Cap at 5 minutes
                }

                if (videoInfo.FrameRate == 0)
                {
                    videoInfo.FrameRate = 30.0;
                }

                if (videoInfo.BitRate == 0)
                {
                    videoInfo.BitRate = (long)(videoInfo.FileSizeBytes * 8 / videoInfo.Duration.TotalSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not enrich video info with FFProbe for {FilePath}", videoInfo.FilePath);
            }
        }
    }

    /// <summary>
    /// Result of video validation
    /// </summary>
    public class VideoValidationResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
        public VideoInfo? VideoInfo { get; set; }
        public List<string> Issues { get; } = new();
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// Gets all issues and warnings combined
        /// </summary>
        public IEnumerable<string> AllMessages => Issues.Concat(Warnings);

        /// <summary>
        /// Gets whether there are any warnings
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets whether there are any issues
        /// </summary>
        public bool HasIssues => Issues.Count > 0;
    }
}