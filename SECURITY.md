# Security Policy

NRS Workbench must never log, display, export or commit GitHub Actions runner credentials.

Files such as `.credentials`, `.credentials_rsaparams`, tokens, private keys and future GitHub API secrets are out of scope for display and diagnostics.

If GitHub authentication is added later, secrets should be stored using Windows-native protected credential storage rather than plain-text configuration files.


## Repository write operations

Repository actions delegate to the user's installed `git.exe`. NRS Workbench does not store Git credentials. Commit creation requires explicit file selection and confirmation, blocks unresolved conflicts, and refuses to silently include staged files outside the selection. Push remains conservative and does not auto-merge or auto-rebase remote changes.


## Public vulnerability reporting

When the repository enables GitHub Private Vulnerability Reporting, use the repository **Security** tab for vulnerabilities that could expose credentials, execute unintended commands, cross repository boundaries or perform unsafe Git writes. Do not publish secrets or exploit details in a normal issue.

## Remote URLs and previews

Remote URLs are sanitized before display. Application diagnostics and Git command errors pass through a best-effort secret redactor for embedded HTTP(S) user-info and GitHub token-shaped values. HTTP(S)/SSH user-info is removed and browser links to GitHub are reconstructed without embedded credentials. Diff preview uses `--no-ext-diff --no-textconv`. Untracked-file preview refuses reparse points, and repository scanning skips reparse-point directories.

Git commits use the user's installed Git and therefore preserve normal local Git behavior, including repository/user Git configuration and hooks. Only add repositories you trust, just as you would before running Git commands from a terminal.
