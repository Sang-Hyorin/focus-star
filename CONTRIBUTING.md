# Contributing

Build and run the synthetic tests for your platform before opening a pull request.
Preserve interval deduplication, local recovery and per-device isolation. Use
synthetic activity records for reports and tests; never submit real activity,
device identifiers, personal settings, backups or diagnostic logs without
reviewing their contents. Windows and macOS dashboards have separate UI code;
changes to shared CSV/archive semantics should be tested on both platforms.
