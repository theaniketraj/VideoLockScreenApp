using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VideoLockScreen.Core
{
    /// <summary>
    /// Monitors Windows session events to detect lock/unlock states
    /// </summary>
    public class SessionMonitor : IDisposable
    {
        #region Windows API Declarations

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

        [DllImport("wtsapi32.dll")]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private const int NOTIFY_FOR_THIS_SESSION = 0;
        private const int WM_WTSSESSION_CHANGE = 0x02B1;
        private const int WTS_SESSION_LOCK = 0x7;
        private const int WTS_SESSION_UNLOCK = 0x8;

        #endregion

        #region Events

        /// <summary>
        /// Fired when the Windows session is locked
        /// </summary>
        public event EventHandler<SessionEventArgs>? SessionLocked;

        /// <summary>
        /// Fired when the Windows session is unlocked
        /// </summary>
        public event EventHandler<SessionEventArgs>? SessionUnlocked;

        #endregion

        #region Fields

        private readonly MessageWindow _messageWindow;
        private bool _disposed = false;
        private bool _isRegistered = false;

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// Initializes a new instance of the SessionMonitor class
        /// </summary>
        public SessionMonitor()
        {
            _messageWindow = new MessageWindow();
            _messageWindow.SessionMessage += OnSessionMessage;
        }

        /// <summary>
        /// Starts monitoring session events
        /// </summary>
        /// <returns>True if registration successful, false otherwise</returns>
        public bool StartMonitoring()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SessionMonitor));

            if (_isRegistered)
                return true;

            try
            {
                bool result = WTSRegisterSessionNotification(_messageWindow.Handle, NOTIFY_FOR_THIS_SESSION);
                _isRegistered = result;
                
                if (result)
                {
                    OnMonitoringStarted();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                OnMonitoringError(ex);
                return false;
            }
        }

        /// <summary>
        /// Stops monitoring session events
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isRegistered)
                return;

            try
            {
                WTSUnRegisterSessionNotification(_messageWindow.Handle);
                _isRegistered = false;
                OnMonitoringStopped();
            }
            catch (Exception ex)
            {
                OnMonitoringError(ex);
            }
        }

        #endregion

        #region Event Handlers

        private void OnSessionMessage(object? sender, SessionMessageEventArgs e)
        {
            switch (e.Message)
            {
                case WTS_SESSION_LOCK:
                    OnSessionLocked(new SessionEventArgs(DateTime.Now, SessionState.Locked));
                    break;

                case WTS_SESSION_UNLOCK:
                    OnSessionUnlocked(new SessionEventArgs(DateTime.Now, SessionState.Unlocked));
                    break;
            }
        }

        protected virtual void OnSessionLocked(SessionEventArgs e)
        {
            SessionLocked?.Invoke(this, e);
        }

        protected virtual void OnSessionUnlocked(SessionEventArgs e)
        {
            SessionUnlocked?.Invoke(this, e);
        }

        protected virtual void OnMonitoringStarted()
        {
            // Override in derived classes if needed
        }

        protected virtual void OnMonitoringStopped()
        {
            // Override in derived classes if needed
        }

        protected virtual void OnMonitoringError(Exception ex)
        {
            // Log error - can be overridden in derived classes
            System.Diagnostics.Debug.WriteLine($"SessionMonitor Error: {ex.Message}");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether the monitor is currently active
        /// </summary>
        public bool IsMonitoring => _isRegistered && !_disposed;

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopMonitoring();
                    _messageWindow?.Dispose();
                }

                _disposed = true;
            }
        }

        ~SessionMonitor()
        {
            Dispose(false);
        }

        #endregion

        #region Internal Message Window Class

        /// <summary>
        /// Hidden window for receiving Windows messages
        /// </summary>
        private class MessageWindow : NativeWindow, IDisposable
        {
            public event EventHandler<SessionMessageEventArgs>? SessionMessage;

            public MessageWindow()
            {
                CreateHandle(new CreateParams
                {
                    Caption = "VideoLockScreen_SessionMonitorWindow",
                    Style = 0,
                    ExStyle = 0,
                    Height = 0,
                    Width = 0,
                    Parent = IntPtr.Zero
                });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_WTSSESSION_CHANGE)
                {
                    SessionMessage?.Invoke(this, new SessionMessageEventArgs((int)m.WParam));
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    DestroyHandle();
                }
            }
        }

        #endregion
    }

    #region Event Arguments Classes

    /// <summary>
    /// Event arguments for session state changes
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public SessionState State { get; }

        public SessionEventArgs(DateTime timestamp, SessionState state)
        {
            Timestamp = timestamp;
            State = state;
        }
    }

    /// <summary>
    /// Event arguments for internal session messages
    /// </summary>
    internal class SessionMessageEventArgs : EventArgs
    {
        public int Message { get; }

        public SessionMessageEventArgs(int message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Represents the possible session states
    /// </summary>
    public enum SessionState
    {
        Unknown,
        Locked,
        Unlocked
    }

    #endregion
}