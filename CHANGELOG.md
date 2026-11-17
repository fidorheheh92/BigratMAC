# Changelog

This file documents behavior that is verifiable in the current source tree. The repository contains no earlier release tags or usable Git history, so this entry does not claim unverified comparisons with a previous public version.

## [1.1.0] - released

### Included

- A WPF application shell with Dashboard, MAC Changer, Hardware ID, History, and Settings views.
- Windows network-adapter discovery with current and permanent address lookup, IP configuration, link state, and link speed.
- Validated MAC entry plus cryptographically secure generation of locally administered unicast addresses.
- Elevated MAC override and restore operations using the adapter registry entry, with optional adapter restart and post-operation verification.
- Fallback storage for hardware addresses previously reported by drivers that hide permanent addresses while overridden.
- Live local throughput readings and a rolling 60-second connection chart.
- Read-only WMI hardware and operating-system information.
- Local JSON settings and optional bounded operation history under `%APPDATA%\BigRat`.
- Confirmation dialogs, result notifications, reduced-motion handling, accent selection, and persisted sidebar state.
- Unit coverage for MAC parsing/generation, mock apply/restore workflows, failure handling, and PowerShell command construction.

### Release preparation

- Set application version metadata to `1.1.0`.
- Added public source-build, usage, permission, privacy, limitation, and license-status documentation.
- Added repository ignore rules for generated output, IDE state, local tool configuration, secrets, previews, and backups.
