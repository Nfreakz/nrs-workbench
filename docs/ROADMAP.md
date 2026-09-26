# NRS Workbench roadmap

This roadmap is specific to the independent `Nfreakz/nrs-workbench` application. It must not be used to transfer paths, credentials, release decisions or implementation details to other Neo RS applications.

## v0.18.3

Current unreleased candidate on `main`.

- Smart runner queue dashboard.
- Pause/Resume.
- CPU/RAM resource guard with recovery margin.
- Professional visual refresh before release: graphite palette, restrained accent usage, larger metadata text and consistent component styling.

## v0.18.4

Repository guidance candidate, currently isolated in PR #16 until the v0.18.3 release decision is complete.

- Contextual safe-action guidance.
- Session-only repository action history.
- Clear AHEAD/BEHIND/dirty visibility.
- Revalidation immediately before Pull/Push.

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
