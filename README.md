# Thermiqra

<p align="center">
  <strong>Real-time PC hardware monitoring, diagnostics, statistics and alerts for Windows 10/11.</strong>
</p>

<p align="center">
  <a href="https://github.com/grishachev/Thermiqra/releases/latest">
    <img src="https://img.shields.io/github/v/release/grishachev/Thermiqra?label=release" alt="Latest release">
  </a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/languages-English%20%7C%20Русский-6f42c1" alt="English and Russian">
</p>

<p align="center">
  <img src="thermiqra-main.png" alt="Thermiqra main monitoring window" width="820">
</p>

<p align="center">
  <a href="https://github.com/grishachev/Thermiqra/releases/latest"><strong>Download the latest Thermiqra release</strong></a>
</p>

Thermiqra is a native Windows desktop application for monitoring PC hardware in real time. It combines live CPU/GPU/RAM/storage data, detailed system information, memory diagnostics, local statistics, alerts, multiple visual themes and built-in updates in one application.

## Highlights

| Area | What Thermiqra provides |
| --- | --- |
| **Live monitoring** | CPU, GPU, RAM, physical storage and logical drive monitoring with one-second updates |
| **CPU threads** | Live load view for individual logical processors directly from the CPU monitor |
| **System information** | Detailed hardware and Windows information in a dedicated system view |
| **Memory diagnostics** | RAM module information, SPD data, JEDEC information, XMP 2.0 profiles, timings and upgrade information |
| **Statistics** | Local SQLite history for Today, 24 hours, 7 days and 30 days, with charts, averages, maxima and timestamps |
| **Alerts** | Custom themed Warning/Critical/Info notifications, selectable sounds, configurable thresholds and Warning/Critical event history |
| **Themes** | Cyber Tech, SteamPunk, Frost Core and Military Ops |
| **Localization** | Full English and Russian interface with automatic System language mode |
| **Background operation** | System tray support and elevated autostart through Windows Task Scheduler |
| **Updates** | In-app GitHub Release update checks, download progress, SHA-256 verification and automatic installation |

## Screenshots

The screenshots are currently shown with the Russian interface. Thermiqra also includes a complete English localization.

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

## Current release — 0.7.0

Thermiqra 0.7.0 introduces a fully custom notification system integrated with all four interface skins and improves startup responsiveness.

### What's included

- custom Thermiqra notification windows instead of standard Windows balloon notifications
- separate notification designs for Cyber Tech, SteamPunk, Frost Core and Military Ops
- Warning, Critical and Info notification types
- three selectable sounds for each notification type
- optional Critical notifications above other windows without stealing focus
- localized notification settings and sound preview controls
- notification state reset after settings changes so newly applied thresholds are evaluated correctly
- faster startup responsiveness by moving SPD/XMP cache loading out of the blocking startup path
- continued use of the corrected SHA-256 verified automatic updater introduced in 0.6.3

See the full release notes on the [Thermiqra 0.7.0 release page](https://github.com/grishachev/Thermiqra/releases/tag/v0.7.0).

## Memory diagnostics

Thermiqra's System Information view includes detailed RAM diagnostics where supported by the hardware and driver stack:

- installed memory modules and slot information
- SPD information
- JEDEC information
- XMP 2.0 profile detection
- SPD/XMP timing information
- occupied and available slot information for upgrades

> Thermiqra reports XMP profiles stored in SPD data. It does not claim that a specific XMP profile is currently active unless that state can be determined reliably.

## Statistics and history

Monitoring history is stored locally in SQLite. Thermiqra provides:

- Today / 24h / 7d / 30d periods
- CPU temperature and load history
- GPU temperature, load, Hot Spot and VRAM temperature when available
- RAM usage history
- per-device storage temperature history
- interactive historical charts
- average and maximum values with timestamps
- all-time hardware records
- Warning/Critical event history
- manual statistics clearing

## Automatic updates

Thermiqra 0.7.0 uses the corrected in-app updater introduced in 0.6.3.

When a newer GitHub Release is available, Thermiqra notifies the user and asks whether the update should be installed. If accepted, Thermiqra downloads the matching installer, shows download progress, verifies the SHA-256 digest when GitHub provides one, starts the installer in silent mode and launches Thermiqra again after the update.

Thermiqra 0.6.1 and 0.6.2 contain an updater bug that can prevent automatic installation after the installer download completes. Users on those versions should install 0.6.3 or newer manually once. After that, future releases can use the corrected in-app update flow.

Users running Thermiqra 0.6.0 or earlier also need to install a newer release manually before using the in-app update flow.

## Localization

Thermiqra supports:

- **System** — Russian Windows uses Russian; other Windows UI languages use English
- **English**
- **Русский**

The main monitor, Settings, Statistics, System Information, tray menu, notifications and update interface are localized.

## Technology

- C# / WPF
- .NET 10
- LibreHardwareMonitorLib 0.9.6
- Microsoft.Data.Sqlite
- System.Management
- PawnIO
- Inno Setup

## System requirements

- Windows 10 or Windows 11
- x64 system
- administrator rights for installation and full hardware access
- PawnIO for supported low-level hardware access

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

Thermiqra stores user settings and local statistics in:

`%LocalAppData%\Thermiqra`

During uninstall, the user can choose whether to keep or remove Thermiqra settings and history.

## Project note

The public application name is **Thermiqra**. The internal project name and namespace currently remain `PCHardwareMonitor`.
