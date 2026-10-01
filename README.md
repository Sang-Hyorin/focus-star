# Focus Star

A local desktop focus tracker for Windows and macOS, with a star-themed dashboard and overlapping time deduplication across devices.

[中文说明](README.zh-CN.md)

## Features

- Track time when an allowlisted application is in the foreground and recent input falls within your configured idle grace period.
- View today's progress, per-application focus and historical charts.
- Pause or resume from the Windows system tray or macOS menu bar.
- Merge Windows and Mac work intervals without counting overlapping time twice.
- Keep a local recovery buffer and batch writes to a shared folder approximately every ten minutes.
- Read old daily CSVs, completed-day archives and conflict copies without adding duplicate time.

This estimates focus from foreground applications and idle time. It is not a measure of productivity or proof that someone is working.

## Download and run

Download a platform ZIP from [Releases](https://github.com/Sang-Hyorin/focus-star/releases) and extract it into a writable folder.

**Windows:** Windows 10/11 with .NET Framework 4.8. Open `LocalActivityRecorder.exe`. Closing the window leaves recording active in the tray; choose **Exit Focus Star** to stop it.

**macOS:** macOS 13 or later. Open `Focus Star Mac.app`. The build supports Apple Silicon and Intel. Keep the app beside `settings.json`, rather than moving only the app to Applications. Closing the window keeps recording active; choose **Quit** from the menu bar to stop it. The app is ad-hoc signed, not notarized, and macOS may require explicit approval in Privacy & Security.

New installations do not install a login/startup entry. Launch the app manually or manage startup through your operating system.

## Configuration and shared folders

Edit rules in the app or start with `settings.example.json`. Mac creates its own `settings-mac.json`; Windows uses `settings.json`. Set equivalent application allowlists on both devices for comparable totals. Applications outside the allowlist are inactive. macOS names are mapped to compatible Windows-style names such as `Code`, `WINWORD` and `POWERPNT`.

Place the extracted application folders and their recording folders in one shared directory, or synchronize that directory using a file-sync provider you choose. Each device writes its own UUID directory under `windows-data` or `mac-data`. Synchronization is file-based; Focus Star does not operate a data-upload service. Visibility on another device depends on the sync provider and can lag the local view.

Windows **Transfer** exports/imports `.focusstar` archives. The Mac **Transfer** view opens the report and recording directory; it does not implement the Windows backup import/export flow. For a complete manual backup, copy the recording directory.

Statistics currently use a fixed **UTC+08:00** day boundary. Legacy timestamps have no timezone; keep device clocks and timezones consistent when importing old records. Saved daily totals use a maximum rather than a sum to avoid decreases during incomplete synchronization; narrowing your allowlist can therefore leave a previously settled total above the currently readable detail.

## Privacy

Records include timestamps, application/process names, Work/Inactive state, idle/sample durations, classification reasons and a random device UUID. The code does not record typed characters, pointer positions, window titles, document names or visited URLs. Foreground process names and timestamps are still personal activity information. Keep your recording folders private; a sync provider receives files only if you put them in its sync directory.

This repository and release packages contain source, generic configuration and application assets only. No real activity logs, personal settings, device identities or historical reports are included. Local errors and recovery buffers stay in the platform's application-data directory. Do not attach real records or backup files to public issues.

## Build and test

Windows requires a .NET SDK containing Roslyn and the installed .NET Framework 4.8:

```powershell
./scripts/build-windows.ps1 -Test
```

macOS requires Xcode Command Line Tools compatible with the installed Swift compiler:

```sh
bash scripts/build-macos.sh --test
```

Builds are written to ignored `dist/`. Tests use synthetic records. They cover interval merging, duplicate exclusion, archive handling, saved-total validation and recovery behavior. They do not simulate real input, sleep/lock events or a cloud provider. Check those behaviors on the actual device before relying on the totals.

## License

MIT. Copyright 2026 Focus Star contributors.
