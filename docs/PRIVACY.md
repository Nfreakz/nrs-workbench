# Privacy

NRS Workbench is local-first and does not include telemetry. The optional Buy Me a Coffee control opens the external support website only after the user clicks it; NRS Workbench does not send application data or diagnostics with that action.

The application can store local runner roots, repository paths, display preferences and notification preferences in `%LOCALAPPDATA%\NRSWorkbench\settings.json`. Application diagnostics are written locally under `%LOCALAPPDATA%\NRSWorkbench\logs`.

Repository status is obtained through the locally installed `git.exe`. Git authentication remains under the user's Git configuration and credential manager. NRS Workbench does not persist GitHub tokens or Git passwords.

Remote URLs shown in the repositories UI are sanitized before display so HTTP(S)/SSH user-info credentials are not exposed. GitHub browser links are reconstructed without embedded credentials. Application diagnostics also redact embedded HTTP(S) user-info and GitHub token-shaped secrets before writing log text.

A portable configuration JSON contains only preferences, local runner roots and repository paths from an explicit allow-list. It does not copy runner registration files, Git credentials, project files or diagnostic logs. Paths can still reveal usernames and project/folder names. Importing a configuration only registers paths: it does not transfer repositories or runner credentials. Existing local repository registrations can be retained, replaced or left unchanged by cancelling before saving.

Runner credential files such as `.credentials` and `.credentials_rsaparams` are not part of supported display or diagnostic features.

Logs may contain local filesystem paths and ordinary Git/runner error messages. Users should review diagnostics before posting them publicly.


## Feedback and diagnostic export

The Feedback & Diagnostics window is local-first. Opening a bug report or feature request launches the repository's GitHub issue template in the user's browser, but the application does not attach logs or send diagnostics.

Diagnostic export first shows the exact generated `diagnostics.txt` content for review. The ZIP is written only to the location selected by the user and contains generated diagnostic text plus a short README. It intentionally excludes `settings.json`, runner `_diag` files, runner credentials, repository contents and remote URLs.

Before the preview/export is produced, NRS Workbench applies its existing secret redactor and additionally replaces configured runner/repository paths, the local Windows user/profile, LocalAppData location and machine name. Sanitization remains best-effort, so the preview is the final privacy check before the user shares anything.
