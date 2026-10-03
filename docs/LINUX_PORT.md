# Linux port

NRS Workbench remains one product in one repository. Linux support is being developed as an additional platform, not as a fork of the Windows application.

## Current spike

Branch: `spike/linux-port-foundation`.

The first spike deliberately leaves the existing WPF application untouched. It adds a `net8.0` Linux platform library that can be built and smoke-tested from the existing Windows CI.

Implemented foundation:

- Linux runner layout detection using `.runner`, `run.sh` and `bin/Runner.Listener`;
- local `.service` marker reading without opening runner credential files;
- systemd service inspection/control through argument-safe `systemctl` calls;
- no implicit `sudo`, shell execution or root requirement inside the library;
- `/proc/stat` aggregate CPU parsing;
- `/proc/meminfo` physical-memory parsing;
- root-filesystem disk figures through .NET `DriveInfo`;
- XDG-aware config/data paths with an isolated preview profile;
- synthetic smoke coverage that runs on Windows without pretending a Linux host is present.

This branch does **not** yet produce a Linux NRS Workbench desktop application.

## Platform boundary

The intended architecture is:

- `NRS.Workbench.Core`: shared models, Git behavior, queue rules and other platform-neutral logic;
- `NRS.Workbench.Platform.Linux`: Linux runner/process/service/resource/path integration;
- existing Windows-specific implementations remain functional while they are gradually moved behind the same platform contracts;
- a later desktop-shell phase replaces WPF-only UI dependencies with a cross-platform UI layer.

Do not add scattered `OperatingSystem.IsLinux()` branches throughout UI/business code. Platform behavior should stay behind explicit services.

## Security / privilege model

NRS Workbench must not run its full Linux desktop process as root.

The Linux service controller calls `systemctl` directly with argument lists. It never prepends `sudo` and never invokes a shell. If service start/stop needs elevated permission, that should be granted explicitly through the host's systemd/polkit policy or another narrowly scoped mechanism.

Runner credential files remain outside supported inspection:

- `.credentials`
- `.credentials_rsaparams`

## Next phases

### Phase 2 · Real Linux runner inspection

- process discovery for `Runner.Listener` and `Runner.Worker`;
- map Linux runner/service/process state into the existing `RunnerInfo` model;
- Linux Runner Doctor checks using the same user-facing semantics as Windows;
- parse local runner version without assuming a Windows PE file;
- exercise the implementation on a real systemd Linux host.

### Phase 3 · Shared platform contracts

- introduce common runner service/process/resource interfaces;
- adapt the existing Windows services to those contracts without changing Windows behavior;
- move shared discovery/orchestration out of the WPF project where appropriate.

### Phase 4 · Cross-platform desktop shell

- replace WPF/Windows Forms UI-only dependencies with a cross-platform desktop shell;
- preserve the current NRS Workbench visual system and workflows;
- provide Linux equivalents for tray integration and notifications where the desktop environment supports them.

### Phase 5 · Packaging

Candidate release outputs:

- `NRSWorkbench-vX.Y.Z-win-x64.zip`
- `NRSWorkbench-vX.Y.Z-linux-x64.tar.gz`

ARM64 is intentionally deferred until there is a real target host.

## Host validation

After Linux installation, run:

```bash
bash scripts/linux/check-host.sh
```

The script is read-only. It reports the OS, architecture, init/systemd availability, Git, .NET and whether a graphical desktop session is visible. It does not install packages, change permissions or inspect runner credentials.
