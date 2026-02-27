# Tasks: Install Parity Scripts Packaging

## Checkpoints

1. Verify root-cause evidence: install/native fallback depends on external scripts.
2. Add failing packaging guard tests (project file content requirements).
3. Update app project to publish/copy `Scripts/` and `Modules/`.
4. Update distribution package validation tests for required assets.
5. Run targeted tests and confirm pass.
6. Document parity status and rollback plan.

## Verification per task

- Task 2: test run fails before csproj change.
- Task 3: test run passes after csproj change.
- Task 4: distribution validation logic aligns with fallback architecture.
- Task 5: targeted `dotnet test` evidence captured.
