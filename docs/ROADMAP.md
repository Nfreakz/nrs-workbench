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

Repository Guidance candidate integrated into `main` and undergoing final polish.

Current implementation:
- PR #18 merged into `main`.
- Main integration commit: `77a4a633b4b90af1067f9155d823dda31304aa9e`.
- Post-merge Windows CI completed successfully.
- Final polish is isolated in `polish/v0.18.4-final`.

Scope:
- Contextual safe-action guidance with a clearer recommendation hierarchy.
- Session-only repository action history with an explicit empty state.
- Clear AHEAD/BEHIND/dirty visibility.
- Revalidation immediately before Pull/Push.
- Optional Buy Me a Coffee card in Settings.

## v0.18.5

Feedback and diagnostics.

- In-app **Report an Issue**.
- In-app **Suggest Feature**.
- Reviewable, sanitized diagnostics bundle.
- Better unexpected-error/crash-report flow.
- No automatic upload of logs and no embedded private webhook credentials.
- Anonymous diagnostics submission only if a future backend is justified and explicit user consent is implemented.

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
