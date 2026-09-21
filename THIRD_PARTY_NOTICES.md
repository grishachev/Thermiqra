# Third-Party Notices

Thermiqra uses third-party software. The notices below are provided to identify those components and their upstream licensing terms.

## LibreHardwareMonitorLib

Thermiqra uses `LibreHardwareMonitorLib` version 0.9.6.

- Upstream project: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- NuGet package: https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.6
- License: Mozilla Public License 2.0 (MPL-2.0)
- MPL 2.0 text: https://www.mozilla.org/MPL/2.0/

LibreHardwareMonitor is licensed under MPL 2.0. Some parts of the upstream project are subject to additional third-party license terms; see the upstream repository's third-party license notices.

When distributing Thermiqra binaries containing LibreHardwareMonitorLib, recipients must be informed where the source code for the MPL-covered component can be obtained, as required by MPL 2.0.

## PawnIO

Thermiqra's Windows installer can install PawnIO. The current bundled installer used during Thermiqra development is PawnIO 2.2.0.

- Upstream project: https://github.com/namazso/PawnIO
- Official installer releases: https://github.com/namazso/PawnIO.Setup/releases
- License: GNU General Public License version 2 or later, with the project-specific special exception described by the PawnIO copyright holder

PawnIO's special exception permits distribution together with independent modules that communicate with PawnIO solely through the device IO control interface, subject to the applicable license terms.

A public Thermiqra binary release that redistributes PawnIO must also comply with PawnIO's applicable GPL source-code and license-distribution obligations. `PawnIO_setup.exe` is therefore not stored in this source repository.

## Thermiqra

No license has yet been selected for Thermiqra's own source code. Until a license is explicitly added, no additional permission to copy, modify, or redistribute Thermiqra's own source code is granted beyond rights provided by applicable law.
