# UI Bug-Bash Quick Check (Install/About/Updates)

## Scope

This checklist validates the standardized UI fixes for:
- Install dialog title bar theming
- About panel GA icon visibility
- Updates table header and selection colors

## Preconditions

- Build and run `WsusManager.App` as Administrator.
- Use any dark theme (DefaultDark recommended).
- Ensure sample updates are present so the Updates list has rows.

## Test Cases

### 1) Install Dialog Title Bar Theme

Repro:
1. Open `Diagnostics` or `Setup` panel.
2. Click `Install WSUS` to open the modal.

Expected:
- Dialog caption background and text match the active theme (no light/white system caption).

Actual before fix:
- Caption rendered with light system colors.

Acceptance:
- Caption stays themed while active/inactive and after refocus.

### 2) About Panel GA Icon

Repro:
1. Open left nav `About`.
2. Check the branding card icon area left of `WSUS Manager`.

Expected:
- GA icon is visible and not broken/missing.

Actual before fix:
- Icon area appears blank.

Acceptance:
- Icon appears in both `dotnet run` and published output.

### 3) Updates Table Header + Selection Colors

Repro:
1. Open left nav `Updates`.
2. Click any update row.
3. Hover another row.

Expected:
- Header row uses dark theme colors.
- Selected and hover rows use dark themed highlight colors with readable text.

Actual before fix:
- White header and white selection highlight leaked from system defaults.

Acceptance:
- No white system highlight/header remains in the Updates table.

## Pass/Fail

- Pass: all three test cases meet expected behavior and acceptance criteria.
- Fail: any one case regresses or uses system-default white table/selection colors.
