# Testing

## Local build

On Windows with .NET 8 SDK:

```powershell
dotnet build .\NRSWorkbench.sln -c Release
```

## Git smoke tests

The smoke-test executable uses temporary local repositories and a temporary bare remote. It does not require a GitHub account or network access.

```powershell
dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release
```

It also covers portable-settings privacy and round-trip checks plus unambiguous, ambiguous and invalid repository-path relocation. The queue smoke tests cover the connected-runner limit, active-job protection, idle rotation, and preservation of manually stopped runners when the queue is disabled. Review/Cancel UI behavior and ZIP startup must still be checked manually.\n\nIt covers clean/dirty inspection, empty-message protection, partial commits, staged-file protection, unusual filenames, rename/delete handling, ahead/push, behind/fast-forward pull, divergence blocking and remote-credential redaction.

## Public-source audit

```powershell
.\scripts\public-audit.ps1
```

The audit checks for common accidental secrets, private-key material, personal absolute paths in source, generated build artifacts and previously observed invalid WPF properties.

## CI

The Windows CI workflow builds the full solution, runs smoke tests and runs the public-source audit on trusted branch pushes and manual `workflow_dispatch` runs. It intentionally does not execute fork pull-request code on the persistent self-hosted runner.

## Manual migration and distribution checks

On a second Windows PC with some repositories already configured, import a JSON from the first PC and verify the replacement prompt. Test **Cancelar** (no saved change), **No** (retain local registrations), and **Sí** (replace only registrations, not folders). Verify missing-path notices and **Guardar** semantics. After an approved tag, verify the extracted ZIP can start and run on a normal Windows desktop; CI only inspects the archive structure and payload.

## One-command readiness check

```text
RUN_PUBLIC_READINESS.cmd
```

This performs the Release build, Git smoke tests and public-source audit in sequence.
