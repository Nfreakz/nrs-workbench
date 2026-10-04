# Security Policy

NRS Workbench must never log, display, export or commit GitHub Actions runner credentials.

Files such as `.credentials`, `.credentials_rsaparams`, tokens, private keys and future GitHub API secrets are out of scope for display and diagnostics.

If GitHub authentication is added later, secrets should be stored using Windows-native protected credential storage rather than plain-text configuration files.


## Repository write operations

Repository actions delegate to the user's installed `git.exe`. NRS Workbench does not store Git credentials. Commit creation requires explicit file selection and confirmation, blocks unresolved conflicts, and refuses to silently include staged files outside the selection. Pull and Push re-inspect repository state immediately before writing so stale branch, working-tree or synchronization data blocks the action. Push remains conservative and does not auto-merge or auto-rebase remote changes.


## Public vulnerability reporting

When the repository enables GitHub Private Vulnerability Reporting, use the repository **Security** tab for vulnerabilities that could expose credentials, execute unintended commands, cross repository boundaries or perform unsafe Git writes. Do not publish secrets or exploit details in a normal issue.

## Remote URLs and previews

Remote URLs are sanitized before display. Application diagnostics and Git command errors pass through a best-effort secret redactor for embedded HTTP(S) user-info and GitHub token-shaped values. HTTP(S)/SSH user-info is removed and browser links to GitHub are reconstructed without embedded credentials. Diff preview uses `--no-ext-diff --no-textconv`. Untracked-file preview refuses reparse points, and repository scanning skips reparse-point directories.

Git commits use the user's installed Git and therefore preserve normal local Git behavior, including repository/user Git configuration and hooks. Only add repositories you trust, just as you would before running Git commands from a terminal.


## Diagnostic export

Feedback & Diagnostics never uploads data automatically and does not embed webhook credentials. The generated bundle contains only application-generated text and a README; it does not copy runner `_diag`, `.runner`, `.credentials`, `.credentials_rsaparams`, repository files, `settings.json` or remote URLs.

The preview/export passes through the existing token/URL-user-info redactor and a contextual redactor for configured local paths plus Windows user/machine identity. This is a best-effort privacy boundary, not a guarantee that arbitrary third-party error text contains no identifying information, so the user must review the displayed preview before sharing it.


## Portable Workspace credentials

Portable Workspace does not make GitHub Actions runner credential files portable. Windows runner credentials remain machine-bound; NRS Workbench never reads, copies, exports or attempts to decrypt `.credentials` or `.credentials_rsaparams`.

Bulk preparation accepts a GitHub personal access token only in the active window/session. The PAT is used to call the official GitHub runner administration API and is not written to settings, logs or the portable manifest. NRS Workbench requests a short-lived runner registration token and passes only that temporary token to `Runner.Listener.exe` through the process environment, not the command line.

Before removing local runner configuration, Workbench writes only non-secret recovery metadata (runner name/URL/work folder/group/labels and behavior flags) to the portable manifest. This lets an interrupted migration be retried without persisting either the PAT or registration token.

Portable Smart Queue recovery state includes a one-way machine fingerprint. Recovery state from another PC is ignored so moving the drive cannot automatically start runners stopped by a different host.
