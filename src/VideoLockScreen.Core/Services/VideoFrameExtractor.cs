using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.Services
{
    /// <summary>
    /// Result of attempting to extract a frame from a video file.
    /// </summary>
    public sealed class VideoFrameExtractionResult
    {
        public bool Success { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public bool IsPlaceholder { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Interface for extracting a representative frame from a video file.
    /// </summary>
    public interface IVideoFrameExtractor
    {
        Task<VideoFrameExtractionResult> ExtractFrameAsync(string videoPath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extracts video frames using an FFmpeg command-line invocation with graceful fallbacks.
    /// </summary>
    public sealed class FfmpegVideoFrameExtractor : IVideoFrameExtractor
    {
        private readonly ILogger<FfmpegVideoFrameExtractor> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly string _frameDirectory;

        public FfmpegVideoFrameExtractor(
            ILogger<FfmpegVideoFrameExtractor> logger,
            IConfigurationService configurationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            _frameDirectory = Path.Combine(Path.GetTempPath(), "VideoLockScreen", "frames");
        }

        public async Task<VideoFrameExtractionResult> ExtractFrameAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath))
            {
                throw new ArgumentException("Video path is required", nameof(videoPath));
            }

            if (!File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video file not found", videoPath);
            }

            Directory.CreateDirectory(_frameDirectory);
            CleanupOldFrames();

            var outputFile = Path.Combine(
                _frameDirectory,
                $"{Path.GetFileNameWithoutExtension(videoPath)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.jpg");

            var ffmpegPath = ResolveFfmpegPath();
            if (ffmpegPath is null)
            {
                _logger.LogWarning("FFmpeg executable not found. Falling back to placeholder frame generation.");
                return await CreatePlaceholderFrameAsync(videoPath, "FFmpeg executable not found");
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -i \"{videoPath}\" -frames:v 1 -q:v 2 \"{outputFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var ffmpegProcess = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

                if (!ffmpegProcess.Start())
                {
                    _logger.LogWarning("Failed to launch FFmpeg process. Using placeholder frame.");
                    return await CreatePlaceholderFrameAsync(videoPath, "Failed to launch FFmpeg process");
                }

                var stdOutTask = ffmpegProcess.StandardOutput.ReadToEndAsync();
                var stdErrTask = ffmpegProcess.StandardError.ReadToEndAsync();

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!ffmpegProcess.HasExited)
                        {
                            ffmpegProcess.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Suppress all exceptions from cancellation disposal
                    }
                }))
                {
                    await ffmpegProcess.WaitForExitAsync(cancellationToken);
                }

                var exitCode = ffmpegProcess.ExitCode;
                var stdErr = await stdErrTask;
                var stdOut = await stdOutTask;

                if (exitCode == 0 && File.Exists(outputFile))
                {
                    _logger.LogInformation("FFmpeg extracted frame: {OutputFile}", outputFile);
                    return new VideoFrameExtractionResult
                    {
                        Success = true,
                        ImagePath = outputFile,
                        IsPlaceholder = false
                    };
                }

                _logger.LogWarning(
                    "FFmpeg failed with exit code {ExitCode}. StdOut: {StdOut}. StdErr: {StdErr}",
                    exitCode,
                    stdOut?.Trim(),
                    stdErr?.Trim());

                return await CreatePlaceholderFrameAsync(videoPath, $"FFmpeg failed with exit code {exitCode}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting frame with FFmpeg. Falling back to placeholder.");
                return await CreatePlaceholderFrameAsync(videoPath, ex.Message);
            }
        }

        private string? ResolveFfmpegPath()
        {
            // 1. Explicit setting
            var settingsPath = _configurationService.Settings.FfmpegPath;
            if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
            {
                return settingsPath;
            }

            // 2. Environment variable
            var envPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // 3. Application directory
            var localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // 4. PATH lookup
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var path in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = Path.Combine(path.Trim(), "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private void CleanupOldFrames()
        {
            try
            {
                var directory = new DirectoryInfo(_frameDirectory);
                if (!directory.Exists)
                {
                    return;
                }

                var threshold = DateTime.UtcNow.AddDays(-2);
                foreach (var file in directory.GetFiles("*.jpg"))
                {
                    if (file.CreationTimeUtc < threshold)
                    {
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to clean up historical frame files");
            }
        }

        private Task<VideoFrameExtractionResult> CreatePlaceholderFrameAsync(string videoPath, string reason)
        {
            try
            {
                Directory.CreateDirectory(_frameDirectory);

                var placeholderPath = Path.Combine(_frameDirectory, $"placeholder_{Guid.NewGuid():N}.jpg");
                using var bitmap = new Bitmap(1920, 1080);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.Black);

                var primaryText = "Video Lock Screen";
                var secondaryText = Path.GetFileName(videoPath);
                var tertiaryText = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;

                using var primaryFont = new Font("Segoe UI", 52, FontStyle.Bold, GraphicsUnit.Point);
                using var secondaryFont = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Point);
                using var tertiaryFont = new Font("Segoe UI", 18, FontStyle.Italic, GraphicsUnit.Point);
                using var primaryBrush = new SolidBrush(Color.White);
                using var secondaryBrush = new SolidBrush(Color.Gainsboro);
                using var tertiaryBrush = new SolidBrush(Color.DarkGray);

                var centerX = bitmap.Width / 2f;
                var centerY = bitmap.Height / 2f;

                var primarySize = graphics.MeasureString(primaryText, primaryFont);
                graphics.DrawString(primaryText, primaryFont, primaryBrush, centerX - primarySize.Width / 2f, centerY - primarySize.Height);

                if (!string.IsNullOrWhiteSpace(secondaryText))
                {
                    var secondarySize = graphics.MeasureString(secondaryText, secondaryFont);
                    graphics.DrawString(secondaryText, secondaryFont, secondaryBrush, centerX - secondarySize.Width / 2f, centerY + 10);
                }

                if (!string.IsNullOrWhiteSpace(tertiaryText))
                {
                    var tertiarySize = graphics.MeasureString(tertiaryText, tertiaryFont);
                    graphics.DrawString(tertiaryText, tertiaryFont, tertiaryBrush, centerX - tertiarySize.Width / 2f, centerY + 60);
                }

                bitmap.Save(placeholderPath, ImageFormat.Jpeg);

                _logger.LogInformation("Placeholder frame generated at {PlaceholderPath}", placeholderPath);

                return Task.FromResult(new VideoFrameExtractionResult
                {
                    Success = true,
                    ImagePath = placeholderPath,
                    IsPlaceholder = true,
                    ErrorMessage = reason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate placeholder frame");
                return Task.FromResult(new VideoFrameExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Placeholder generation failed: {ex.Message}"
                });
            }
        }
    }
}
