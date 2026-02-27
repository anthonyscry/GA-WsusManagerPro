# Proposal: Restore Script-Based Parity for Install/HTTPS/Scheduling

## Purpose

Fix deployment-time parity regressions where the C# app cannot run legacy-backed operations (especially WSUS Install) because required PowerShell assets are not packaged with the app output.

## Scope

- Include `Scripts/` and `Modules/` folders in `WsusManager.App` output/publish artifacts.
- Align distribution validation tests with runtime fallback dependencies.
- Add packaging guard tests so regressions are caught before release.

Out of scope:

- Rewriting `Install-WsusWithSqlExpress.ps1` into a full native C# installer.
- Behavior changes inside legacy PowerShell scripts.

## Acceptance Criteria

1. `dotnet` publish output for `WsusManager.App` contains `Scripts/` and `Modules/` alongside `WsusManager.exe`.
2. Install fallback can locate `Install-WsusWithSqlExpress.ps1` from packaged output paths.
3. Validation tests assert required fallback assets are present (not absent).
4. New packaging tests fail if project content entries for Scripts/Modules are removed.

## Risks

- Larger published artifact size.
- Duplicate source-of-truth risk if native implementations diverge from script fallbacks.
- Potential path/quoting issues if downstream packaging tooling overrides publish output.
