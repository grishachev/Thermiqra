# Thermiqra

Thermiqra is a Windows 10/11 desktop application for real-time PC hardware monitoring with local history, statistics, alerts and system information.

<p align="center">
  <img src="thermiqra-main.png" alt="Thermiqra main monitoring window" width="820">
</p>

<p align="center">
  <a href="https://github.com/grishachev/Thermiqra/releases/latest"><strong>Download the latest release</strong></a>
</p>

## Features

- real-time CPU, GPU, RAM, storage and logical drive monitoring
- detailed system information
- local monitoring history stored in SQLite
- statistics for Today, 24 hours, 7 days and 30 days
- CPU, GPU, RAM and storage charts with exact values on hover
- average and maximum values with timestamps
- all-time hardware records
- Warning/Critical event history
- temperature warnings and critical alerts
- system tray operation
- elevated autostart through Windows Task Scheduler
- persistent settings, statistics, window size and position
- four interface skins: Cyber Tech, SteamPunk, Frost Core and Military Ops
- GitHub release update checks
- Inno Setup installer with PawnIO installation
- clean uninstall with optional removal of Thermiqra settings and history

## Screenshots

### System information and statistics

<p align="center">
  <img src="thermiqra-system-info.png" alt="Thermiqra system information" width="49%">
  <img src="thermiqra-statistics.png" alt="Thermiqra statistics" width="49%">
</p>

### Themes

Thermiqra includes four complete interface skins. The screenshot below shows the SteamPunk skin.

<p align="center">
  <img src="thermiqra-steampunk.png" alt="Thermiqra SteamPunk theme" width="820">
</p>

## Version 0.5.0

Thermiqra 0.5.0 focuses on monitoring history and statistics.

Highlights:

- SQLite-based local statistics database
- history retention for up to 30 days
- Today / 24h / 7d / 30d periods
- CPU temperature and load statistics
- GPU temperature, load, Hot Spot and VRAM temperature when available
- RAM usage statistics
- per-device storage temperature history
- interactive historical charts
- all-time records with date and time
- Warning/Critical event history
- manual statistics clearing
- GitHub release update checking
- improved installer and uninstall behavior, including closing a running Thermiqra before update or removal

## Technology

- C# / WPF
- .NET 10
- LibreHardwareMonitorLib 0.9.6
- Microsoft.Data.Sqlite
- PawnIO
- Inno Setup

## System requirements

- Windows 10 or Windows 11
- x64 system
- administrator rights are required for hardware access and installation

## Build

Main project:

`PCHardwareMonitor/PCHardwareMonitor.csproj`

The Release build uses `win-x64` and self-contained publishing:

```powershell
dotnet publish .\PCHardwareMonitor\PCHardwareMonitor.csproj -c Release -r win-x64 --self-contained true -o .\PCHardwareMonitor\bin\Release\net10.0-windows\win-x64\publish
```

## Installer

Inno Setup script:

`Installer/Thermiqra.iss`

`PawnIO_setup.exe` is intentionally not stored in the repository. For a local installer build, place it at:

`Installer/Dependencies/PawnIO_setup.exe`

PawnIO is installed by the Thermiqra installer but is not automatically removed with Thermiqra because it may be used by other software.

## User data

Thermiqra stores user settings and statistics in:

`%LocalAppData%\Thermiqra`

During uninstall, the user can choose whether to keep or remove settings and history.

## Note

The internal project name and namespace currently remain `PCHardwareMonitor`.
