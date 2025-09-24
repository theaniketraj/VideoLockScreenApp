# Video Lock Screen Wallpaper Application

A Windows application that displays video wallpapers on lock screens, providing dynamic and visually appealing backgrounds when the system is locked.

## 🎯 Features

- **Dynamic Video Lock Screens**: Display video files as animated wallpapers on Windows lock screens
- **Multi-Monitor Support**: Automatically detects and covers all connected monitors
- **Windows Service Integration**: Runs as a background service for seamless operation
- **Session Monitoring**: Automatically responds to Windows lock/unlock events
- **Configurable Settings**: Customizable video files, playback options, and display settings
- **Hardware Acceleration**: Utilizes WPF MediaElement for optimized video rendering
- **Format Support**: Compatible with common video formats (MP4, AVI, MOV, etc.)

## 🏗️ Architecture

The application follows a multi-layered architecture:

### Core Components

- **VideoLockScreen.Core**: Core business logic and models
  - `ConfigurationService`: JSON-based settings management
  - `VideoPlayerService`: WPF MediaElement wrapper for video playback
  - `SessionMonitor`: Windows session lock/unlock detection
  - `VideoFileHelper`: Video file operations and metadata extraction
  - `SystemHelper`: Monitor detection and system utilities

- **VideoLockScreen.Service**: Windows Service implementation
  - `VideoLockScreenService`: Main background service
  - `LockScreenManager`: Full-screen overlay window management

## 🚀 Technology Stack

- **.NET 8.0** (Windows-specific)
- **WPF** for video rendering and UI
- **Windows Forms** for system integration
- **Microsoft.Extensions** suite for dependency injection and logging
- **System.Text.Json** for configuration management
- **Windows APIs** for session monitoring

## 📁 Project Structure

```pgsql
VideoLockScreenApp/
├── src/
│   ├── VideoLockScreen.Core/          # Core business logic
│   │   ├── Models/                    # Data models and settings
│   │   ├── Services/                  # Core services
│   │   └── Utilities/                 # Helper utilities
│   └── VideoLockScreen.Service/       # Windows Service
│       ├── VideoLockScreenService.cs  # Main service implementation
│       └── LockScreenManager.cs       # Lock screen window management
├── Video-Lock-Screen-Plan.md          # Detailed implementation plan
└── VideoLockScreenApp.sln             # Visual Studio solution
```

## ⚙️ Installation & Setup

### Prerequisites

- Windows 10/11
- .NET 8.0 Runtime
- Administrator privileges (for Windows Service installation)

### Building from Source

1. Clone the repository:

   ```bash
   git clone https://github.com/theaniketraj/VideoLockScreenApp.git
   cd VideoLockScreenApp
   ```

2. Build the solution:

   ```bash
   dotnet build
   ```

3. Run tests (when available):

   ```bash
   dotnet test
   ```

## 🔧 Configuration

The application uses JSON-based configuration stored in the user's application data folder. Key settings include:

- **Video File Path**: Path to the video file to display
- **Playback Settings**: Loop, volume, scaling options
- **Monitor Settings**: Multi-monitor configuration
- **Service Settings**: Auto-start, error handling preferences

## 🚦 Development Status

### ✅ Phase 1 - Core Infrastructure (Completed)

- [x] Project structure and solution setup
- [x] Core models and services implementation
- [x] Windows session monitoring
- [x] Configuration management
- [x] Video player service foundation
- [x] Windows Service infrastructure

### 🔄 Phase 2 - Lock Screen Integration (In Progress)

- [ ] Full-screen overlay implementation
- [ ] Video rendering integration
- [ ] Multi-monitor support enhancement
- [ ] User interface project
- [ ] Testing and refinement

### ⏳ Future Phases

- [ ] Advanced Features (Phase 3)
- [ ] User Interface (Phase 4)
- [ ] Testing & Quality Assurance (Phase 5)
- [ ] Deployment & Distribution (Phase 6)

## 🤝 Contributing

This project is currently in active development. Contributions, issues, and feature requests are welcome!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Related Projects

- [Windows Lock Screen API Documentation](https://docs.microsoft.com/en-us/windows/win32/api/wtsapi32/)
- [WPF MediaElement Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.mediaelement)

## 🏆 Acknowledgments

- Microsoft for the comprehensive .NET ecosystem
- Windows API documentation and community resources
- Open source community for inspiration and best practices

---

**Note**: This application modifies Windows lock screen behavior and requires appropriate permissions. Always test in a safe environment before deploying to production systems.
