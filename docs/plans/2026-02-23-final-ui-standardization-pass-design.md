# Final UI Standardization Pass Design

Date: 2026-02-23
Status: Approved
Owner: WSUS Manager team

## Objective

Complete one final UI consistency pass across Client Tools, Computers, and Updates while fixing remaining usability regressions in Script Generator, Error Code Lookup, and About branding.

## Scope

1. Standardize control heights and row spacing for key form inputs.
2. Re-layout Script Generator into a compact single row.
3. Restore Error Code Lookup to dropdown + manual typing with stable selected text.
4. Add missing common error code definitions currently shown but unrecognized.
5. Restore GA icon reliability on About panel.

## User-Approved UX Decisions

- Script Generator must remove the "Operation:" label.
- Script operation dropdown moves left, and "Generate Script" sits on the right in the same row.
- Error Code Lookup must support both dropdown selection and manual typing.
- Last two common error codes must be recognized.
- About panel must reliably show GA icon.
- Entire pass should improve visual uniformity/standardization.

## Approaches Considered

### A) Targeted UI + Data Reliability Fixes (Selected)

- Reuse shared input styles and apply them consistently.
- Keep current MVVM bindings/commands.
- Fix known binding and dictionary gaps directly.

Pros: Fast, low risk, minimal churn.
Cons: Not a full template-level redesign.

### B) Full Component Template Overhaul

- Rebuild all controls around one custom control/template set.

Pros: Maximum long-term consistency.
Cons: High risk/regression surface for a final polish pass.

### C) Bug-only patch

- Fix only error lookup + icon issues.

Pros: Quick.
Cons: Fails the requested uniformity pass.

## Selected Design

### 1) Standardized Control Rhythm

- Enforce one baseline input height for TextBox/ComboBox rows in:
  - Client Tools form rows,
  - Computers filter/search row,
  - Updates filter/search row.
- Keep consistent vertical spacing scale (tight but readable) between labels, inputs, and action buttons.
- Ensure mixed rows (input + button) align on center/baseline.

### 2) Script Generator Row Layout

- Remove `Operation:` text label from Script Generator card.
- Build one row:
  - left: compact-width operation dropdown,
  - right: `Generate Script` button.
- Keep existing command and selected-operation binding behavior.

### 3) Error Code Lookup Interaction

- Use editable/searchable ComboBox design that supports:
  - selecting common codes,
  - typing arbitrary codes.
- Prevent value disappearance after selection by stabilizing binding (ComboBox text/selection coordination).
- Preserve explicit `Lookup` button behavior.

### 4) Missing Error Code Definitions

- Add definitions for the currently listed but unresolved trailing codes in `WsusErrorCodes` so UI choices are valid.
- Keep description/fix format aligned with existing dictionary entries.

### 5) About GA Icon Reliability

- Replace fragile relative source usage with a reliable resource/pack URI strategy in About panel image binding.
- Keep current icon asset and dimensions unless asset-specific scaling issues appear.

## Testing Strategy

1. XAML structure tests:
   - Script Generator single-row structure and no `Operation:` label.
   - Error lookup combo supports editable/searchable behavior.
   - About panel image source uses resilient resource path.
2. Service tests:
   - Newly added error codes resolve via lookup API.
3. Build + focused regression tests for keyboard/XAML and lookup behavior.
4. Manual smoke checks:
   - Script Generator row alignment,
   - Error code typed/selected value visible before and after lookup,
   - About icon visible,
   - visual uniformity across Client Tools/Computers/Updates rows.

## Acceptance Criteria

1. Script Generator has no "Operation:" label and uses one compact row.
2. Error lookup supports both dropdown and typing without disappearing input.
3. Previously unrecognized trailing common codes are recognized.
4. About panel consistently displays GA icon.
5. Form controls in Client Tools, Computers, and Updates share consistent sizing and spacing.
