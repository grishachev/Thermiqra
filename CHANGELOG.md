# Change log

## Unreleased

- Show downloaded/total megabytes, current transfer speed and percentage in the update window, including downloads without a known total size.
- Keep transfer status updating during network pauses and display waiting duration, then separate file/signature verification from installation startup.
- Use shared themed button and transfer-progress templates; cover hover, pressed, keyboard-focus and disabled states in all four skins and both languages.

## 1.1.1 — November 21, 2026

- Complete the configured shallow AppData file-identity association search instead of stopping after two seconds or fixed folder/file counts. The search still stays within its two-level scope and excludes protected paths and links.
- Remove the obsolete association-search timeout warning when rechecking saved removal history. Preserve unrelated limitations and genuine current read failures.
- Keep cleanup ownership, fingerprint, shared-data and protected-path checks unchanged. This fix does not grant broader deletion permissions.
- Localize update highlights for already published 1.0.0 clients through hidden legacy language headings, while keeping the visible GitHub release description English-only.
- Add regressions for delayed enumeration, associations beyond former cutoffs, preserved access failures, stale warning removal and the actual 1.0.0 release-note parser.

## 1.1.0 — November 21, 2026

Changes since 1.0.0.

### Application management and removal

- Added an installed-program list covering supported Win32/MSI registrations and Microsoft Store/MSIX packages.
- Added program history, observed installation/change records and saved pre-removal information for later inspections.
- Kept Thermiqra's removal stages in one window, including progress, restore-point handling, leftovers and results. Third-party uninstallers retain their own interfaces.
- Added native-uninstaller result handling that separates success, cancellation, failure, partial removal, active helper processes and restart requirements. A nonzero exit code is not silently converted into a successful uninstall just because the registration disappeared.
- Preserved removal attempts and access to reinspection when an application disappears from the installed list.
- Added supported leftover discovery using product identity, recorded files/registry data, application-data layout and registered shortcuts. No application-name-specific rules are required for each example in the regression suite.
- Added support for confirmed external product-data directories associated with MSIX applications. Windows-managed package directories remain protected.
- Default-selected eligible confirmed leftovers, with shared, protected and ambiguous objects handled separately. Selected application data may include accounts, settings, projects and saves inside that folder.
- Revalidated changes, ownership, shared paths, links and access immediately before cleanup.
- Moved eligible file leftovers to the Windows Recycle Bin and saved typed registry backups before supported registry changes.
- Added recovery with collision protection, so restoring old registry data does not overwrite a newly created replacement.
- Added stale-registration handling for supported cases where the registered uninstaller is missing, with a separate confirmation and checks for inaccessible or disconnected storage.
- Displayed searches immediately, including searches started from history. Removed redundant confirmation/selection steps after leftover selection.
- Grouped leftover lists compactly, with optional evidence details and a collapsed cleanup journal.
- Added an automatic fresh inspection after cleanup. The result shows remaining objects and limitations directly, without requiring a return to the previous page.
- Improved icon lookup using available registration, executable and shortcut data. Portable applications without an installation registration are not automatically treated as installed programs.
- Improved sorting within the same day using available timestamps rather than immediately falling back to alphabetical order. A registration modification timestamp is not presented as guaranteed original installation time.

### CPU details and background monitoring

- Improved logical-processor load, core/thread information and CPU clock presentation.
- Added a compact CPU/GPU/RAM monitoring panel with four distinct themed designs.
- Made tray-icon hover the default display mode, with an optional persistent mode in Settings.
- Reused the existing hardware snapshot; the panel does not start another hardware collector.
- Kept GPU temperature and load tied to the same selected GPU.
- Preserved unavailable readings instead of displaying invented zero values.
- Fixed persistent-mode dismissal when merely moving over empty taskbar areas or neighboring tray icons.
- Improved detection of other tray menus, including custom menus that do not activate normally.
- Excluded background desktop/overlay windows from menu detection to avoid permanently hiding the panel.
- Allowed neighboring tooltips to appear above the panel without dismissing it or repeatedly covering them. The panel temporarily yields when a real menu is opened.

### Storage and system information

- Added prominent total, used and free capacity figures to the Storage Analyzer header, styled for all four skins.
- Improved storage-health compatibility with additional read-only Windows information sources and explicit device identity/bus checks.
- Kept absent/incomplete SMART evidence distinct from a confirmed healthy state. Temperatures alone do not determine disk health.
- Expanded graphics-information fallback handling, including cases where the main sensor source does not identify an adapter completely.
- Improved reporting of available processor, memory and storage information, while retaining explicit unavailable states.

### Update verification

- Added project-key installer signature verification using ECDSA P-256 and SHA-256, without requiring a paid Authenticode certificate.
- Bound verification to the exact version, installer name, size and SHA-256; a correctly signed old installer cannot substitute for a different release.
- Required checksum/signature evidence in the new updater instead of silently skipping required checks.
- Rechecked the verified installer before launch and restricted accepted download sources.
- Added maintainer tools for signing and independently verifying a release bundle. Private keys stay outside the repository and CI.
- Added a compact update-highlights view with a bounded scroll area and access to full release notes.
- Kept the visible GitHub description English-only while supporting separate Russian/English in-app summaries.

Existing 1.0.0 installations obtain this transition release through their existing update flow. New verification applies to subsequent updates after installing the new updater. Project-key signatures do not change UAC publisher text or remove SmartScreen warnings.

### Translation, appearance and maintainability

- Fixed missing shared English translations and localized application-owned errors in removal, registry snapshots, updates and disk analysis.
- Checked static window text and removal states in both languages across all four skins, including narrow layouts.
- Added themed operation surfaces, compact result states and clearer progress instead of silent waits or repeated Thermiqra windows.
- Split large system-information and storage-analyzer implementations into smaller files and corrected the duplicated settings-file extension.
- Added source-level localization auditing and Windows CI configuration.
- Refreshed the English project overview and replaced its Russian screenshot links with English UI examples.

### Validation and remaining boundaries

- Automated regression checks cover SMART/NVMe interpretation, protected paths, shared components, product separation, snapshot comparison, recovery collisions, process/restart handling, update signatures, translations and themed UI states. See [validation notes](RELIABILITY_CHECKS.md) for the current count and scope.
- A separate disposable-Windows run on November 16, 2026 completed **16 EXE/MSI scenarios with 424 successful assertions**, including cancellation, failure, shared data, links, locked/denied files, recovery and restart handling. Later changes also have local checks; the guest run is not presented as a new run of every later revision.
- Tray tooltip stacking has an isolated native-window regression check and user confirmation with the actual Steam tray tooltip/menu.
- Tests do not prove compatibility with every commercial uninstaller, storage controller or hardware sensor. Complete Windows rollback via System Restore is not covered by the registry-backup tests.
- Leftover inspection is bounded. It is not a universal service/driver/task/firewall/shell-extension remover, nor a promise to erase all user files outside known application locations.
- A clean result means nothing remained in the successfully checked locations. Protected, remaining and unverified objects must not be reported as a fully verified cleanup.

## Published releases

The published **1.0.0** and earlier downloads retain their original version-specific descriptions on [GitHub Releases](https://github.com/grishachev/Thermiqra/releases). Features introduced in 1.1.0 are not retroactively included in those installers.
