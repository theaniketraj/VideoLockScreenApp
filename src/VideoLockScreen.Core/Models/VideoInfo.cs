using System.IO;

namespace VideoLockScreen.Core.Models
{
    /// <summary>
    /// Information about a video file
    /// </summary>
    public class VideoInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string Format { get; set; } = string.Empty;
        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public bool HasAudio { get; set; }
        public bool HasVideo { get; set; }
        public long BitRate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Gets a human-readable string representation of the file size
        /// </summary>
        public string FileSizeFormatted
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = FileSizeBytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// Gets a formatted duration string (HH:MM:SS)
        /// </summary>
        public string DurationFormatted
        {
            get
            {
                if (Duration.TotalHours >= 1)
                    return Duration.ToString(@"hh\:mm\:ss");
                else
                    return Duration.ToString(@"mm\:ss");
            }
        }

        /// <summary>
        /// Gets the video resolution as a string (e.g., "1920x1080")
        /// </summary>
        public string Resolution => $"{Width}x{Height}";

        /// <summary>
        /// Gets the aspect ratio as a simplified fraction string
        /// </summary>
        public string AspectRatio
        {
            get
            {
                if (Width == 0 || Height == 0)
                    return "Unknown";

                int gcd = CalculateGCD(Width, Height);
                int aspectWidth = Width / gcd;
                int aspectHeight = Height / gcd;
                
                return $"{aspectWidth}:{aspectHeight}";
            }
        }

        /// <summary>
        /// Determines if the video is suitable for lock screen use
        /// </summary>
        public bool IsSuitableForLockScreen
        {
            get
            {
                // Check if video has reasonable duration (not too short or too long)
                if (Duration.TotalSeconds < 1 || Duration.TotalMinutes > 10)
                    return false;

                // Check if video has decent resolution
                if (Width < 640 || Height < 480)
                    return false;

                // Must have video track
                if (!HasVideo)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Creates VideoInfo from a file path
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>VideoInfo object with basic file information</returns>
        public static VideoInfo FromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Video file not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            
            return new VideoInfo
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()
            };
        }

        /// <summary>
        /// Calculates the Greatest Common Divisor of two numbers
        /// </summary>
        private static int CalculateGCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        public override string ToString()
        {
            return $"{FileName} ({Resolution}, {DurationFormatted}, {FileSizeFormatted})";
        }
    }

    /// <summary>
    /// Supported video file formats
    /// </summary>
    public static class SupportedVideoFormats
    {
        public static readonly string[] Extensions = new[]
        {
            ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".flv", ".webm", ".m4v", ".3gp", ".mpg", ".mpeg"
        };

        public static readonly string Filter = "Video Files|" + 
            string.Join(";", Extensions.Select(ext => $"*{ext}")) +
            "|All Files|*.*";

        /// <summary>
        /// Checks if a file extension is supported
        /// </summary>
        /// <param name="extension">File extension (with or without dot)</param>
        /// <returns>True if supported</returns>
        public static bool IsSupported(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            if (!extension.StartsWith("."))
                extension = "." + extension;

            return Extensions.Contains(extension.ToLowerInvariant());
        }

        /// <summary>
        /// Checks if a file path has a supported video extension
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file has a supported extension</returns>
        public static bool IsSupportedFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            return IsSupported(Path.GetExtension(filePath));
        }
    }
}