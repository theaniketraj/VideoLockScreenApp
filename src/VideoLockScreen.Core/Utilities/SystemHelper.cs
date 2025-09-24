using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VideoLockScreen.Core.Utilities
{
    /// <summary>
    /// Utility class for Windows system operations
    /// </summary>
    public class SystemHelper
    {
        private readonly ILogger<SystemHelper> _logger;

        #region Windows API Declarations

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        #endregion

        public SystemHelper(ILogger<SystemHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the time since the last user input (keyboard or mouse)
        /// </summary>
        /// <returns>Time span since last input</returns>
        public TimeSpan GetTimeSinceLastInput()
        {
            try
            {
                var lastInputInfo = new LASTINPUTINFO();
                lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

                if (GetLastInputInfo(ref lastInputInfo))
                {
                    uint currentTickCount = GetTickCount();
                    uint idleTime = currentTickCount - lastInputInfo.dwTime;
                    return TimeSpan.FromMilliseconds(idleTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last input time");
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Determines if the workstation is currently locked
        /// </summary>
        /// <returns>True if locked, false otherwise</returns>
        public bool IsWorkstationLocked()
        {
            try
            {
                // Check if the current process has the desktop
                IntPtr desktop = GetForegroundWindow();
                return desktop == IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking workstation lock status");
                return false;
            }
        }

        /// <summary>
        /// Gets information about all connected monitors
        /// </summary>
        /// <returns>List of monitor information</returns>
        public List<MonitorInfo> GetMonitorInfo()
        {
            var monitors = new List<MonitorInfo>();

            try
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    monitors.Add(new MonitorInfo
                    {
                        DeviceName = screen.DeviceName,
                        IsPrimary = screen.Primary,
                        WorkingArea = new System.Drawing.Rectangle(
                            screen.WorkingArea.X,
                            screen.WorkingArea.Y,
                            screen.WorkingArea.Width,
                            screen.WorkingArea.Height
                        ),
                        Bounds = new System.Drawing.Rectangle(
                            screen.Bounds.X,
                            screen.Bounds.Y,
                            screen.Bounds.Width,
                            screen.Bounds.Height
                        ),
                        BitsPerPixel = screen.BitsPerPixel
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monitor information");
            }

            return monitors;
        }

        /// <summary>
        /// Makes a window topmost
        /// </summary>
        /// <param name="windowHandle">Handle to the window</param>
        /// <returns>True if successful</returns>
        public bool MakeWindowTopmost(IntPtr windowHandle)
        {
            try
            {
                return SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making window topmost");
                return false;
            }
        }

        /// <summary>
        /// Removes topmost flag from a window
        /// </summary>
        /// <param name="windowHandle">Handle to the window</param>
        /// <returns>True if successful</returns>
        public bool RemoveWindowTopmost(IntPtr windowHandle)
        {
            try
            {
                return SetWindowPos(windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing window topmost flag");
                return false;
            }
        }

        /// <summary>
        /// Gets system power information
        /// </summary>
        /// <returns>Power information</returns>
        public PowerInfo GetPowerInfo()
        {
            var powerInfo = new PowerInfo();

            try
            {
                var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
                powerInfo.PowerLineStatus = powerStatus.PowerLineStatus.ToString();
                powerInfo.BatteryChargeStatus = powerStatus.BatteryChargeStatus.ToString();
                powerInfo.BatteryLifePercent = powerStatus.BatteryLifePercent;
                powerInfo.BatteryLifeRemaining = powerStatus.BatteryLifeRemaining;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting power information");
            }

            return powerInfo;
        }

        /// <summary>
        /// Checks if the system is running on battery power
        /// </summary>
        /// <returns>True if on battery power</returns>
        public bool IsOnBatteryPower()
        {
            try
            {
                var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
                return powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking battery status");
                return false;
            }
        }

        /// <summary>
        /// Gets the current user's session ID
        /// </summary>
        /// <returns>Session ID</returns>
        public int GetCurrentSessionId()
        {
            try
            {
                return Process.GetCurrentProcess().SessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session ID");
                return -1;
            }
        }

        /// <summary>
        /// Checks if the current process is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator</returns>
        public bool IsRunningAsAdministrator()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking administrator status");
                return false;
            }
        }

        /// <summary>
        /// Gets the Windows version information
        /// </summary>
        /// <returns>Windows version information</returns>
        public WindowsVersionInfo GetWindowsVersion()
        {
            var versionInfo = new WindowsVersionInfo();

            try
            {
                var osVersion = Environment.OSVersion;
                versionInfo.Version = osVersion.Version;
                versionInfo.Platform = osVersion.Platform.ToString();
                versionInfo.ServicePack = osVersion.ServicePack;
                versionInfo.VersionString = osVersion.VersionString;

                // Get more detailed Windows 10/11 information if available
                var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (registryKey != null)
                {
                    versionInfo.ProductName = registryKey.GetValue("ProductName")?.ToString() ?? "Unknown";
                    versionInfo.DisplayVersion = registryKey.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                    versionInfo.BuildLabEx = registryKey.GetValue("BuildLabEx")?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Windows version information");
            }

            return versionInfo;
        }
    }

    /// <summary>
    /// Information about a monitor/display
    /// </summary>
    public class MonitorInfo
    {
        public string DeviceName { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public System.Drawing.Rectangle WorkingArea { get; set; }
        public System.Drawing.Rectangle Bounds { get; set; }
        public int BitsPerPixel { get; set; }

        public int Width => Bounds.Width;
        public int Height => Bounds.Height;
        public string Resolution => $"{Width}x{Height}";

        public override string ToString()
        {
            return $"{DeviceName} ({Resolution}){(IsPrimary ? " [Primary]" : "")}";
        }
    }

    /// <summary>
    /// System power information
    /// </summary>
    public class PowerInfo
    {
        public string PowerLineStatus { get; set; } = string.Empty;
        public string BatteryChargeStatus { get; set; } = string.Empty;
        public float BatteryLifePercent { get; set; }
        public int BatteryLifeRemaining { get; set; }
    }

    /// <summary>
    /// Windows version information
    /// </summary>
    public class WindowsVersionInfo
    {
        public Version Version { get; set; } = new Version();
        public string Platform { get; set; } = string.Empty;
        public string ServicePack { get; set; } = string.Empty;
        public string VersionString { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string BuildLabEx { get; set; } = string.Empty;

        public bool IsWindows10OrLater => Version.Major >= 10;
        public bool IsWindows11OrLater => Version.Major >= 10 && Version.Build >= 22000;

        public override string ToString()
        {
            return $"{ProductName} {DisplayVersion} (Build {Version})";
        }
    }
}