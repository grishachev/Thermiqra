# Thermiqra

Thermiqra is a Windows 10/11 desktop application for real-time PC hardware monitoring.

## Features

- CPU, GPU, RAM, storage and logical drive monitoring
- temperature warnings and critical alerts
- system tray operation
- elevated autostart through Windows Task Scheduler
- persistent settings, window size and position
- multiple interface color themes
- Inno Setup installer with PawnIO installation

## Technology

- C# / WPF
- .NET 10
- LibreHardwareMonitorLib 0.9.6
- PawnIO
- Inno Setup

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

## Note

The internal project name and namespace currently remain `PCHardwareMonitor`.
