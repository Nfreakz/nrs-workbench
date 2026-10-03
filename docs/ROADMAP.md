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

Runner control, discovery, feedback and diagnostics.

- Detect runner installations from their real files rather than folder-name prefixes.
- Let users choose which detected runners participate in Smart Queue; excluded runners are not started, stopped or counted toward the queue limit.
- In-app **Report an Issue**.
- In-app **Suggest Feature**.
- Reviewable, sanitized diagnostics bundle.
- Better unexpected-error/crash-report flow.
- No automatic upload of logs and no embedded private webhook credentials.
- Anonymous diagnostics submission only if a future backend is justified and explicit user consent is implemented.
- **Runner Doctor** with local, explainable health checks for installation, registration, process/service state, version and `_diag` activity. The first iteration remains read-only and does not auto-repair or update runners.

Development status (not published):
- runner discovery and explicit Smart Queue pool selection are merged to `main`;
- Feedback & Diagnostics plus long-running runtime hardening are merged to `main`;
- Runner Doctor is the active v0.18.5 development block.

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
