# NRS Workbench roadmap

This roadmap is specific to the independent `Nfreakz/nrs-workbench` application. It must not be used to transfer paths, credentials, release decisions or implementation details to other Neo RS applications.

## v0.18.3

Published Public Preview.

- Smart runner queue dashboard.
- Pause/Resume.
- CPU/RAM resource guard with 5-point recovery margin.
- Professional visual refresh: graphite palette, restrained accent usage, larger metadata text and consistent component styling.
- Published release: `v0.18.3`.
- Portable asset: `NRSWorkbench-v0.18.3-win-x64.zip`.

## v0.18.4

Published Public Preview. [Release and Windows x64 ZIP](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.4).

- Contextual repository guidance and session-only action history.
- Clear AHEAD/BEHIND and local-change summary.
- Revalidation immediately before Pull/Push and a timeout for Git inspection.
- Compact runner-details layout fix.
- Optional Buy Me a Coffee card in Settings.

PR #25's runner discovery and Smart Queue selection work is separate from this release.

## v0.18.5

Published Public Preview. [Release and Windows x64 ZIP](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.5).

- Runner discovery based on real installation markers.
- Explicit Smart Queue runner pool selection.
- Feedback & Diagnostics with review-before-share local export.
- Long-running refresh/error hardening and runtime-session heartbeat.
- Runner Doctor with local, explainable, read-only health checks.
- Published asset: `NRSWorkbench-v0.18.5-win-x64.zip`.

## v0.18.6

Maintenance and storage hygiene.

- Maintenance Center with measured/reclaimable storage categories.
- Automatic NRS Workbench log rotation.
- Safe cleanup of Workbench and PREVIEW logs.
- Reviewed cleanup of old runner `_diag` logs.
- BUSY runners are never cleaned.
- Runner `_work` is inventory-only; no automatic deletion.

## v0.18.7

Portable Workspace and runner migration.

- True portable Workbench profile with a `Data` directory beside the executable and relative same-volume paths.
- Detect current-machine preparation state from a credential-free portable manifest.
- Machine-bound Smart Queue recovery journal so a moved workspace cannot auto-recover another PC's queue state.
- Session-only GitHub authorization for bulk runner preparation.
- Re-register stopped interactive GitHub.com runners in bulk without storing the GitHub PAT or temporary registration token.
- Preserve runner name, target, work folder, group and labels where GitHub exposes them.
- Explicit confirmation, per-runner progress and retryable pending migrations; no automatic migration on disk insertion.
- Service-installed runners remain out of scope for portable migration.

## v0.19.0

GitHub Actions control.

- Inspect workflows and recent runs.
- Trigger supported `workflow_dispatch` workflows.
- Re-run failed workflow runs/jobs where the GitHub API permits it.
- Cancel supported workflow runs.
- Improve runner diagnostics/log access without leaving NRS Workbench.
- Authentication and least-privilege permissions must be designed before enabling write actions.

## Later

- Product website / technical SEO as a separate web project or explicitly approved repository, not silently added to the desktop-app repository.
- GitLab provider only if there is demonstrated demand after GitHub support is mature.
- Continue measuring startup, idle CPU and RAM use before making public performance claims.
