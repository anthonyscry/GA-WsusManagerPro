# CI Quick Guide

## CI entry point

The C# workflow now uses a single verification command:

```powershell
./Scripts/verify.ps1
```

`verify.ps1` runs (in order): restore, build (with binlog), format/lint (`dotnet format`), tests (TRX + coverage), and optional packaging.

## Reproduce CI locally

Use the local-friendly wrapper first:

```powershell
./Scripts/verify-local.ps1
```

For full CI parity (including format checks):

```powershell
./Scripts/verify.ps1 -Configuration Release -RestoreRetryCount 3 -RestoreRetryDelaySeconds 5
```

To include packaging in the same run:

```powershell
./Scripts/verify.ps1 -RunPackaging
```

## Notes

- SDK is pinned by `global.json`.
- CI diagnostics are written to `.ci-artifacts/` and uploaded as artifacts on every run.
- Restore retries are limited to transient network/feed failures only.
- Legacy PowerShell workflow diagnostics are written to `.ci-artifacts/legacy/` and uploaded as `code-review-diagnostics`, `test-diagnostics`, and `build-diagnostics`.
