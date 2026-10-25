# Thermiqra

<p align="center">
  <strong>Thermiqra 1.0.0 — hardware monitoring, SMART drive diagnostics and storage analysis for Windows 10/11.</strong>
  <br>
  <strong>Thermiqra 1.0.0 — мониторинг оборудования, SMART-диагностика накопителей и анализ дисков для Windows 10/11.</strong>
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
  <a href="https://github.com/grishachev/Thermiqra/releases/download/v1.0.0/Thermiqra_Setup_1.0.0.exe"><strong>Download Thermiqra 1.0.0 / Скачать Thermiqra 1.0.0</strong></a>
</p>

Thermiqra is a native Windows desktop application for monitoring PC hardware in real time. It combines live CPU/GPU/RAM/storage data, detailed system information, memory diagnostics, local statistics, alerts, storage analysis, practical explanations, upgrade guidance, reporting and built-in updates in one application.

## Highlights

| Area | What Thermiqra provides |
| --- | --- |
| **Live monitoring** | CPU, GPU, RAM, physical storage and logical drive monitoring with one-second updates |
| **CPU threads** | Live load view for individual logical processors directly from the CPU monitor |
| **System information** | Detailed hardware and Windows information in a dedicated system view |
| **Memory diagnostics** | RAM module information, SPD data, JEDEC information, XMP 2.0 profiles, timings and upgrade information |
| **SMART drive diagnostics** | ATA SMART attributes and firmware thresholds for supported SATA drives, standard NVMe SMART/Health counters, and clear evidence-based drive statuses |
| **Smart Guidance** | Plain-language explanations of important hardware states without hiding the underlying technical values |
| **Upgrade Advisor** | Conservative upgrade suggestions based on installed hardware and repeated real usage, with benchmark periods excluded from recommendations |
| **Storage analyzer** | Fast NTFS/MFT analysis, full folder hierarchy, contextual explanations, safe cleanup, Downloads/Recycle Bin actions and user-confirmed deletion |
| **Statistics** | Local SQLite history for Today, 24 hours, 7 days and 30 days, with charts, averages, maxima and timestamps |
| **Reports and export** | Technical diagnostic TXT report, raw statistics CSV export and visual HTML summary report with charts |
| **History analytics** | Period comparison, recent summaries and recurring warning analysis based on local monitoring history |
| **Hardware changes** | One-shot detection of meaningful hardware configuration changes |
| **Alerts** | Custom themed Warning/Critical/Info notifications, selectable sounds, configurable thresholds and Warning/Critical event history |
| **Themes** | Cyber Tech, SteamPunk, Frost Core and Military Ops |
| **Localization** | Full English and Russian interface with automatic System language mode |
| **Background operation** | System tray support, temperature tooltip and elevated autostart through Windows Task Scheduler |
| **Removable drives** | Connected removable USB volumes appear dynamically without restarting Thermiqra |
| **Updates** | In-app GitHub Release checks, release notes, progress display, SHA-256 verification, cancelable downloads and automatic installation |

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

## Current release — 1.0.0

**Thermiqra 1.0.0 is the first stable release.** The main addition is evidence-based storage health diagnostics, while the existing monitoring, storage analysis, history and updater remain part of the application.

**[Download Thermiqra_Setup_1.0.0.exe](https://github.com/grishachev/Thermiqra/releases/download/v1.0.0/Thermiqra_Setup_1.0.0.exe)** · [Full release notes / Полный список изменений](https://github.com/grishachev/Thermiqra/releases/tag/v1.0.0)

### Русский — что нового в 1.0.0

- Расширена SMART-диагностика HDD, SATA SSD и NVMe SSD с использованием данных, действительно полученных от накопителей.
- Для поддерживаемых ATA-накопителей доступны атрибуты SMART, пороги производителя и раскрываемые технические подробности.
- Для NVMe отображаются доступные показатели SMART/Health: предупреждения контроллера, износ, ошибки, температура, объёмы чтения/записи и циклы питания.
- Состояние накопителя сопровождается объяснением: норма, требуется внимание, критично или недостаточно данных. Thermiqra не придумывает «процент здоровья».
- Добавлено запоминание положения и размеров изменяемых окон. Анализатор дисков сохраняет предусмотренное поведение открытия по размеру главного окна.
- Улучшено представление системной информации и технических параметров накопителей.
- Сохранены русский и английский интерфейсы и четыре темы оформления.

### English — what's new in 1.0.0

- Expanded HDD, SATA SSD and NVMe SSD diagnostics using actual SMART/Health data returned by the devices.
- Added ATA SMART attribute details and drive-reported thresholds where supported.
- Expanded available NVMe health metrics: controller warnings, wear, errors, temperature, read/write totals and power cycles.
- Added understandable drive status explanations: normal, attention required, critical or insufficient data. Thermiqra does not invent a health percentage.
- Added persistence of positions and sizes for resizable windows. Storage Analyzer continues to open with the main window's geometry as designed.
- Improved the System Information presentation and detailed storage diagnostics.
- Retained Russian and English localization and four interface themes.

Detailed historical changes, including the 0.9.0 storage analyzer redesign, are available on the [Releases](https://github.com/grishachev/Thermiqra/releases) page.

## First-run introduction and guidance

Thermiqra includes a short introduction for new users. It explains the main monitoring areas, where to find additional information and how Thermiqra works in the background.

The introduction can be skipped and opened again later from Settings.

Smart Guidance is designed to explain what a value means and when it may deserve attention without replacing the actual hardware readings or inventing a synthetic PC health score.

## Upgrade Advisor

The **What can be improved** section uses information Thermiqra can actually observe.

Recommendations are intentionally conservative:

- a single short spike is not enough for an upgrade recommendation
- repeated real-world evidence matters more than one isolated reading
- recognized benchmark activity is excluded from Advisor evidence
- Thermiqra does not invent FPS gains or arbitrary overall PC scores
- when the available data is not sufficient, Thermiqra does not pretend to know the answer

## Memory diagnostics

Thermiqra's System Information view includes detailed RAM diagnostics where supported by the hardware and driver stack:

- installed memory modules and slot information
- SPD information
- JEDEC information
- XMP 2.0 profile detection
- SPD/XMP timing information
- occupied and available slot information for upgrades

> Thermiqra reports XMP profiles stored in SPD data. It does not claim that a specific XMP profile is currently active unless that state can be determined reliably.

## Drive health: ATA SMART and NVMe SMART/Health

Thermiqra reads supported storage health information **on demand**, rather than running an expensive continuous SMART scan.

- **HDD / SATA SSD:** ATA SMART attributes, normalized values and firmware-reported failure thresholds where Windows and the drive expose them; relevant disk error indicators are explained when available.
- **NVMe SSD:** standard SMART/Health information, including critical warning flags, available spare, percentage used, power-on hours, error counters, unsafe shutdowns, data read/written and power cycles where accessible.
- **Health status:** clear normal / attention / critical / insufficient-data indicators based on observed information. A percentage is displayed only when it is a meaningful device-provided measurement, not a fabricated overall score.
- **Technical details:** expandable information remains available for experienced users without overwhelming less technical users.

Not every controller or USB bridge exposes SMART information. Missing fields remain unavailable rather than being treated as zero or as proof that a device is healthy.

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
- period comparison and history analytics
- CSV export and HTML summary reporting

## Storage analysis

Thermiqra includes a dedicated storage analyzer for understanding where disk space is going and acting on it without leaving the application for every step.

For NTFS volumes, Thermiqra can use a fast MFT-based analysis path. For exFAT, FAT32 and other supported file systems, it falls back to safe directory traversal.

The analyzer provides:

- expandable drive, folder, subfolder and file hierarchy
- logical and allocated file sizes
- file and directory counts
- size/share information
- largest branches and files
- contextual explanations for selected folders and files
- automatic navigation to important storage items
- conservative Safe Cleanup for known cleanup scenarios
- Downloads and Recycle Bin shortcuts
- user-confirmed file and folder deletion
- automatic refresh after cleanup or deletion

**Safe Cleanup never silently treats an unknown folder as disposable.** Before cleanup, Thermiqra shows what will be cleaned and lets the user deselect individual items.

For known cache locations where only contents should be removed, Thermiqra preserves the parent folder.

Ordinary files and folders are deleted only after an explicit user action. When applicable, Thermiqra offers a choice between moving the item to the Recycle Bin and permanent deletion.

Removable USB volumes are detected dynamically and can be analyzed without restarting Thermiqra.

## Automatic updates

Thermiqra checks GitHub Releases for newer versions and can install them automatically.

When an update is available, Thermiqra can show the release notes, download the matching installer, display download progress, verify the SHA-256 digest when GitHub provides one, start the installer in silent mode and launch Thermiqra again after installation.

Updates can also be checked manually from the system tray.

Starting with 0.8.1, the update download can be canceled safely. Closing the progress window or pressing **Cancel / Отмена** cancels the active download, and the main application remains usable.

Thermiqra 0.6.1 and 0.6.2 contain an older updater bug that can prevent automatic installation after the installer download completes. Users on those versions should install 0.6.3 or newer manually once. After that, future releases can use the corrected in-app update flow.

Users running Thermiqra 0.6.0 or earlier also need to install a newer release manually before using the in-app update flow.

## Localization

Thermiqra supports:

- **System** — Russian Windows uses Russian; other Windows UI languages use English
- **English**
- **Русский**

The main monitor, Settings, Statistics, System Information, Storage Analyzer, cleanup/delete dialogs, first-run introduction, tray menu, notifications and update interface are localized.

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
