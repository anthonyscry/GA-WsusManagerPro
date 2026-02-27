# Proposal: Harden Legacy Fallback Script Resolution and Install Safety Checks

## Purpose

Improve runtime reliability for script-backed fallback operations (Install, HTTPS, Schedule) so packaged and portable deployments consistently locate required PowerShell scripts and safely clean up install credentials after failures.

## In Scope

- Standardize fallback file search paths across Installation, Scheduled Task, and HTTPS fallback services.
- Expand script search to include bounded app-base parent traversal for packaged/portable launches.
- Improve error reporting to include full searched path list.
- Add tests for fallback path search behavior and environment-variable credential restoration during install failures.

## Out of Scope

- Rewriting legacy PowerShell install/HTTPS/maintenance scripts into native C#.
- Changing install dialog UX or adding new settings toggles.
- CI workflow redesign.

## Acceptance Criteria

1. `InstallationService`, `ScheduledTaskService`, and `LegacyHttpsConfigurationFallback` use the same path search strategy.
2. All three services search at least: app base `Scripts/`, app base root, plus bounded app-base parents.
3. Failure messages include full searched path list for easier troubleshooting.
4. Automated tests verify:
   - install fallback search paths exclude current directory candidates,
   - scheduled task search paths include app-parent candidates and exclude current directory candidates,
   - HTTPS fallback search paths include app-parent candidates and exclude current directory candidates,
   - install password environment variable is restored even when process execution throws.

## Non-Functional Requirements

- Keep behavior backward-compatible for existing extracted package layout.
- Avoid introducing new runtime dependencies.
- Keep path search bounded to avoid unbounded traversal and avoid current-directory hijack risks.

## Risks + Mitigations

- **Risk:** Search order changes could pick an unexpected script copy.
  - **Mitigation:** Preserve existing precedence first (app base first), then add fallbacks.
- **Risk:** More search paths may complicate support logs.
  - **Mitigation:** Return deterministic, de-duplicated path list and include in error output.
- **Risk:** Credential cleanup regressions.
  - **Mitigation:** Add explicit negative test for environment restoration on exceptions.
