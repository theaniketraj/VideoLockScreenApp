using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using VideoLockScreen.Core.Models;

namespace VideoLockScreen.Core.Services
{
    /// <summary>
    /// Advanced system integration service for deep Windows API interaction
    /// </summary>
    public interface ISystemIntegrationService
    {
        Task<bool> EnableKioskModeAsync();
        Task<bool> DisableKioskModeAsync();
        Task<bool> BlockSystemKeysAsync();
        Task<bool> UnblockSystemKeysAsync();
        Task<bool> SetProcessPriorityAsync(ProcessPriorityClass priority);
        Task<bool> PreventSleepAsync();
        Task<bool> AllowSleepAsync();
        Task<bool> DisableTaskManagerAsync();
        Task<bool> EnableTaskManagerAsync();
        Task<bool> SetSystemSecurityAsync(bool secure);
        event EventHandler<SystemSecurityEventArgs> SecurityStateChanged;
    }

    /// <summary>
    /// Implementation of advanced system integration service
    /// </summary>
    public class SystemIntegrationService : ISystemIntegrationService
    {
        private readonly ILogger<SystemIntegrationService> _logger;
        private bool _kioskModeEnabled = false;
        private bool _systemKeysBlocked = false;
        private bool _sleepPrevented = false;
        private bool _taskManagerDisabled = false;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private const int WH_KEYBOARD_LL = 13;

        public event EventHandler<SystemSecurityEventArgs>? SecurityStateChanged;

        // Windows API declarations
        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref int lpvParam, int fuWinIni);

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("advapi32.dll")]
        private static extern int RegSetValueEx(IntPtr hKey, string lpValueName, int reserved, uint dwType, byte[] lpData, int cbData);

        [DllImport("advapi32.dll")]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int options, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);

        // Constants
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        private const int SPI_SETSCREENSAVERRUNNING = 97;
        private const int SPIF_SENDWININICHANGE = 2;
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(-2147483647);
        private const int KEY_SET_VALUE = 0x0002;
        private const uint REG_DWORD = 4;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc? _keyboardProc;

        public SystemIntegrationService(ILogger<SystemIntegrationService> logger)
        {
            _logger = logger;
            _keyboardProc = HookCallback;
        }

        /// <summary>
        /// Enables kiosk mode for full system lockdown
        /// </summary>
        public async Task<bool> EnableKioskModeAsync()
        {
            try
            {
                _logger.LogInformation("Enabling kiosk mode");

                // Disable screensaver
                int screensaverActive = 0;
                SystemParametersInfo(SPI_SETSCREENSAVERRUNNING, 1, ref screensaverActive, SPIF_SENDWININICHANGE);

                // Block system keys
                await BlockSystemKeysAsync();

                // Prevent sleep
                await PreventSleepAsync();

                // Disable task manager
                await DisableTaskManagerAsync();

                // Set high process priority
                await SetProcessPriorityAsync(ProcessPriorityClass.High);

                _kioskModeEnabled = true;

                OnSecurityStateChanged(new SystemSecurityEventArgs
                {
                    SecurityLevel = SystemSecurityLevel.Kiosk,
                    IsEnabled = true,
                    Features = "Screensaver disabled, System keys blocked, Sleep prevented, Task manager disabled"
                });

                _logger.LogInformation("Successfully enabled kiosk mode");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable kiosk mode");
                return false;
            }
        }

        /// <summary>
        /// Disables kiosk mode and restores normal system behavior
        /// </summary>
        public async Task<bool> DisableKioskModeAsync()
        {
            try
            {
                _logger.LogInformation("Disabling kiosk mode");

                // Re-enable screensaver
                int screensaverActive = 0;
                SystemParametersInfo(SPI_SETSCREENSAVERRUNNING, 0, ref screensaverActive, SPIF_SENDWININICHANGE);

                // Unblock system keys
                await UnblockSystemKeysAsync();

                // Allow sleep
                await AllowSleepAsync();

                // Enable task manager
                await EnableTaskManagerAsync();

                // Reset process priority
                await SetProcessPriorityAsync(ProcessPriorityClass.Normal);

                _kioskModeEnabled = false;

                OnSecurityStateChanged(new SystemSecurityEventArgs
                {
                    SecurityLevel = SystemSecurityLevel.Normal,
                    IsEnabled = false,
                    Features = "All restrictions removed"
                });

                _logger.LogInformation("Successfully disabled kiosk mode");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable kiosk mode");
                return false;
            }
        }

        /// <summary>
        /// Blocks system key combinations (Alt+Tab, Ctrl+Alt+Del, Windows key, etc.)
        /// </summary>
        public async Task<bool> BlockSystemKeysAsync()
        {
            try
            {
                _logger.LogInformation("Blocking system keys");

                if (_keyboardHook == IntPtr.Zero && _keyboardProc != null)
                {
                    using (var curProcess = Process.GetCurrentProcess())
                    using (var curModule = curProcess.MainModule)
                    {
                        if (curModule?.ModuleName != null)
                        {
                            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                                GetModuleHandle(curModule.ModuleName), 0);
                        }
                    }
                }

                _systemKeysBlocked = _keyboardHook != IntPtr.Zero;

                if (_systemKeysBlocked)
                {
                    _logger.LogInformation("Successfully blocked system keys");
                }
                else
                {
                    _logger.LogWarning("Failed to install keyboard hook");
                }

                return await Task.FromResult(_systemKeysBlocked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to block system keys");
                return false;
            }
        }

        /// <summary>
        /// Unblocks system key combinations
        /// </summary>
        public async Task<bool> UnblockSystemKeysAsync()
        {
            try
            {
                _logger.LogInformation("Unblocking system keys");

                if (_keyboardHook != IntPtr.Zero)
                {
                    bool unhooked = UnhookWindowsHookEx(_keyboardHook);
                    if (unhooked)
                    {
                        _keyboardHook = IntPtr.Zero;
                        _systemKeysBlocked = false;
                        _logger.LogInformation("Successfully unblocked system keys");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to unhook keyboard hook");
                    }
                }

                return await Task.FromResult(!_systemKeysBlocked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unblock system keys");
                return false;
            }
        }

        /// <summary>
        /// Sets the process priority class
        /// </summary>
        public async Task<bool> SetProcessPriorityAsync(ProcessPriorityClass priority)
        {
            try
            {
                _logger.LogInformation("Setting process priority to {Priority}", priority);

                using (var currentProcess = Process.GetCurrentProcess())
                {
                    currentProcess.PriorityClass = priority;
                }

                _logger.LogInformation("Successfully set process priority to {Priority}", priority);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set process priority to {Priority}", priority);
                return false;
            }
        }

        /// <summary>
        /// Prevents the system from going to sleep
        /// </summary>
        public async Task<bool> PreventSleepAsync()
        {
            try
            {
                _logger.LogInformation("Preventing system sleep");

                uint result = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                _sleepPrevented = result != 0;

                if (_sleepPrevented)
                {
                    _logger.LogInformation("Successfully prevented system sleep");
                }
                else
                {
                    _logger.LogWarning("Failed to prevent system sleep");
                }

                return await Task.FromResult(_sleepPrevented);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prevent system sleep");
                return false;
            }
        }

        /// <summary>
        /// Allows the system to go to sleep normally
        /// </summary>
        public async Task<bool> AllowSleepAsync()
        {
            try
            {
                _logger.LogInformation("Allowing system sleep");

                uint result = SetThreadExecutionState(ES_CONTINUOUS);
                _sleepPrevented = false;

                _logger.LogInformation("Successfully restored normal sleep behavior");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore sleep behavior");
                return false;
            }
        }

        /// <summary>
        /// Disables Task Manager access
        /// </summary>
        public async Task<bool> DisableTaskManagerAsync()
        {
            try
            {
                _logger.LogInformation("Disabling Task Manager");

                IntPtr hKey;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, 
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System", 
                    0, KEY_SET_VALUE, out hKey);

                if (result == 0)
                {
                    byte[] data = BitConverter.GetBytes(1);
                    result = RegSetValueEx(hKey, "DisableTaskMgr", 0, REG_DWORD, data, data.Length);
                    RegCloseKey(hKey);

                    _taskManagerDisabled = result == 0;
                    
                    if (_taskManagerDisabled)
                    {
                        _logger.LogInformation("Successfully disabled Task Manager");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to set Task Manager registry value");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to open registry key for Task Manager disable");
                }

                return await Task.FromResult(_taskManagerDisabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable Task Manager");
                return false;
            }
        }

        /// <summary>
        /// Enables Task Manager access
        /// </summary>
        public async Task<bool> EnableTaskManagerAsync()
        {
            try
            {
                _logger.LogInformation("Enabling Task Manager");

                IntPtr hKey;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, 
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System", 
                    0, KEY_SET_VALUE, out hKey);

                if (result == 0)
                {
                    byte[] data = BitConverter.GetBytes(0);
                    result = RegSetValueEx(hKey, "DisableTaskMgr", 0, REG_DWORD, data, data.Length);
                    RegCloseKey(hKey);

                    _taskManagerDisabled = false;
                    _logger.LogInformation("Successfully enabled Task Manager");
                }
                else
                {
                    _logger.LogWarning("Failed to open registry key for Task Manager enable");
                }

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable Task Manager");
                return false;
            }
        }

        /// <summary>
        /// Sets the overall system security level
        /// </summary>
        public async Task<bool> SetSystemSecurityAsync(bool secure)
        {
            try
            {
                _logger.LogInformation("Setting system security level to {Level}", secure ? "High" : "Normal");

                if (secure)
                {
                    return await EnableKioskModeAsync();
                }
                else
                {
                    return await DisableKioskModeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set system security level");
                return false;
            }
        }

        /// <summary>
        /// Low-level keyboard hook callback
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                
                // Block specific key combinations
                if (IsBlockedKey(vkCode))
                {
                    _logger.LogDebug("Blocked system key: {VkCode}", vkCode);
                    return new IntPtr(1); // Block the key
                }
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        /// <summary>
        /// Determines if a virtual key code should be blocked
        /// </summary>
        private bool IsBlockedKey(int vkCode)
        {
            // Block Windows keys, Alt+Tab, Ctrl+Alt+Del, etc.
            return vkCode switch
            {
                0x5B or 0x5C => true, // Left/Right Windows key
                0x12 when (GetAsyncKeyState(0x09) & 0x8000) != 0 => true, // Alt+Tab
                0x1B => true, // Escape key
                0x73 => true, // F4 (for Alt+F4)
                _ => false
            };
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// Raises the security state changed event
        /// </summary>
        private void OnSecurityStateChanged(SystemSecurityEventArgs args)
        {
            SecurityStateChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Event arguments for system security state changes
    /// </summary>
    public class SystemSecurityEventArgs : EventArgs
    {
        public SystemSecurityLevel SecurityLevel { get; set; }
        public bool IsEnabled { get; set; }
        public string Features { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// System security levels
    /// </summary>
    public enum SystemSecurityLevel
    {
        Normal,
        Enhanced,
        Kiosk,
        Maximum
    }
}