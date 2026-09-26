# NRS Workbench roadmap

This roadmap is specific to the independent `Nfreakz/nrs-workbench` application. It must not be used to transfer paths, credentials, release decisions or implementation details to other Neo RS applications.

## v0.18.3

Current unreleased candidate on `main`.

- Smart runner queue dashboard.
- Pause/Resume.
- CPU/RAM resource guard with 5-point recovery margin.
- Professional visual refresh: graphite palette, restrained accent usage, larger metadata text and consistent component styling.
- Version metadata on `main`: `0.18.3`.
- Windows CI is green for `main` commit `721b23a08e78ce48c8973d6e2b061dbbf56e8379`.
- The candidate CI validates portable packaging but intentionally does not upload a public artifact.
- Publishing still requires explicit release approval.

## v0.18.4

Repository Guidance candidate, rebuilt on top of the professional v0.18.3 UI baseline.

Current implementation:
- PR #18: `feat: v0.18.4 repository guidance on professional UI baseline`.
- Branch: `feat/v0.18.4-repository-guidance-v2`.
- Keep the PR separate from `main` until the v0.18.3 release decision is complete.
- PR #16 is obsolete, closed and not merged.

Scope:
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
