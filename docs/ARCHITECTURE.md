# Architecture

NRS Workbench is a local Windows desktop application built on .NET 8 and WPF.

## Projects

- `NRS.Workbench.Core`: domain models and interfaces without a WPF dependency.
- `NRS.Workbench.App`: WPF UI plus Windows-specific runner, Git, tray, notification and local-storage services.
- `NRS.Workbench.SmokeTests`: dependency-free executable smoke tests that exercise the real Git service against temporary repositories.

## Design boundaries

The application delegates repository operations to the user's installed `git.exe`. It does not implement Git, store Git credentials or require GitHub API access for local features.

Runner diagnostics are read from local self-hosted runner files. Credential files are intentionally outside the application's supported diagnostic surface.

Repository write actions are deliberately conservative. Pull is fast-forward-only, push is blocked when the known remote state is ahead, commits require explicit file selection, and unresolved conflicts block automated writes.

## Local data

Settings and application logs live under `%LOCALAPPDATA%\NRSWorkbench`. Legacy `%LOCALAPPDATA%\RunnerManager\settings.json` is migrated once when the NRS Workbench settings file does not yet exist. The legacy file is not deleted.
