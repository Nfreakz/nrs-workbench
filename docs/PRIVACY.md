# Privacy

NRS Workbench is local-first and does not include telemetry.

The application can store local runner roots, repository paths, display preferences and notification preferences in `%LOCALAPPDATA%\NRSWorkbench\settings.json`. Application diagnostics are written locally under `%LOCALAPPDATA%\NRSWorkbench\logs`.

Repository status is obtained through the locally installed `git.exe`. Git authentication remains under the user's Git configuration and credential manager. NRS Workbench does not persist GitHub tokens or Git passwords.

Remote URLs shown in the repositories UI are sanitized before display so HTTP(S)/SSH user-info credentials are not exposed. GitHub browser links are reconstructed without embedded credentials. Application diagnostics also redact embedded HTTP(S) user-info and GitHub token-shaped secrets before writing log text.

Runner credential files such as `.credentials` and `.credentials_rsaparams` are not part of supported display or diagnostic features.

Logs may contain local filesystem paths and ordinary Git/runner error messages. Users should review diagnostics before posting them publicly.
