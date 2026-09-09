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

It covers clean/dirty inspection, empty-message protection, partial commits, staged-file protection, unusual filenames, rename/delete handling, ahead/push, behind/fast-forward pull, divergence blocking and remote-credential redaction.

## Public-source audit

```powershell
.\scripts\public-audit.ps1
```

The audit checks for common accidental secrets, private-key material, personal absolute paths in source, generated build artifacts and previously observed invalid WPF properties.

## CI

The Windows CI workflow builds the full solution, runs the smoke tests and runs the public-source audit on every push and pull request.

## One-command readiness check

```text
RUN_PUBLIC_READINESS.cmd
```

This performs the Release build, Git smoke tests and public-source audit in sequence.
