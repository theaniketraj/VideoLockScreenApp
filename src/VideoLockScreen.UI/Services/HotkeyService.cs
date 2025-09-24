using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace VideoLockScreen.UI.Services
{
    /// <summary>
    /// Interface for hotkey service
    /// </summary>
    public interface IHotkeyService
    {
        /// <summary>
        /// Event fired when emergency exit hotkey is pressed
        /// </summary>
        event EventHandler? EmergencyExitRequested;

        /// <summary>
        /// Event fired when activation hotkey is pressed
        /// </summary>
        event EventHandler? ActivationRequested;

        /// <summary>
        /// Registers global hotkeys
        /// </summary>
        bool RegisterHotkeys(IntPtr windowHandle);

        /// <summary>
        /// Unregisters global hotkeys
        /// </summary>
        void UnregisterHotkeys();
    }

    /// <summary>
    /// Service for managing global hotkeys
    /// </summary>
    public class HotkeyService : IHotkeyService
    {
        private readonly ILogger<HotkeyService> _logger;
        
        // Hotkey IDs
        private const int EMERGENCY_EXIT_HOTKEY_ID = 1001;
        private const int ACTIVATION_HOTKEY_ID = 1002;
        
        // Windows API constants
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private IntPtr _windowHandle;
        private HwndSource? _hwndSource;
        private bool _hotkeysRegistered;

        public event EventHandler? EmergencyExitRequested;
        public event EventHandler? ActivationRequested;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyService(ILogger<HotkeyService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers global hotkeys
        /// </summary>
        public bool RegisterHotkeys(IntPtr windowHandle)
        {
            try
            {
                _windowHandle = windowHandle;
                
                // Set up window message handling
                _hwndSource = HwndSource.FromHwnd(_windowHandle);
                if (_hwndSource != null)
                {
                    _hwndSource.AddHook(WndProc);
                }

                // Register emergency exit hotkey: Ctrl+Alt+Shift+F12
                bool emergencyRegistered = RegisterHotKey(
                    _windowHandle, 
                    EMERGENCY_EXIT_HOTKEY_ID,
                    MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT,
                    (uint)KeyInterop.VirtualKeyFromKey(Key.F12));

                // Register activation hotkey: Ctrl+Alt+L
                bool activationRegistered = RegisterHotKey(
                    _windowHandle,
                    ACTIVATION_HOTKEY_ID,
                    MOD_CONTROL | MOD_ALT | MOD_NOREPEAT,
                    (uint)KeyInterop.VirtualKeyFromKey(Key.L));

                _hotkeysRegistered = emergencyRegistered && activationRegistered;

                if (_hotkeysRegistered)
                {
                    _logger.LogInformation("Global hotkeys registered successfully");
                    _logger.LogInformation("Emergency Exit: Ctrl+Alt+Shift+F12");
                    _logger.LogInformation("Quick Activation: Ctrl+Alt+L");
                }
                else
                {
                    _logger.LogWarning("Failed to register some hotkeys. Emergency: {Emergency}, Activation: {Activation}", 
                        emergencyRegistered, activationRegistered);
                }

                return _hotkeysRegistered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register global hotkeys");
                return false;
            }
        }

        /// <summary>
        /// Unregisters global hotkeys
        /// </summary>
        public void UnregisterHotkeys()
        {
            try
            {
                if (_hotkeysRegistered && _windowHandle != IntPtr.Zero)
                {
                    UnregisterHotKey(_windowHandle, EMERGENCY_EXIT_HOTKEY_ID);
                    UnregisterHotKey(_windowHandle, ACTIVATION_HOTKEY_ID);
                    
                    _hotkeysRegistered = false;
                    _logger.LogInformation("Global hotkeys unregistered");
                }

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister global hotkeys");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                
                switch (hotkeyId)
                {
                    case EMERGENCY_EXIT_HOTKEY_ID:
                        _logger.LogInformation("Emergency exit hotkey pressed");
                        EmergencyExitRequested?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                        
                    case ACTIVATION_HOTKEY_ID:
                        _logger.LogInformation("Activation hotkey pressed");
                        ActivationRequested?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotkeys();
        }
    }
}