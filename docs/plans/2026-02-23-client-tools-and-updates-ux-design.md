# Client Tools and Updates UX Design

Date: 2026-02-23
Status: Approved
Owner: WSUS Manager team

## Objective

Improve operator usability in the C# app by removing placeholder update rows, reducing visual bulk in Client Tools, improving Error Code lookup ergonomics, and aligning Updates column headers with theme styling.

## Scope

1. Remove mock data from Updates panel and use real update metadata only.
2. Make Client Tools panel more compact, especially Target Host input area.
3. Replace Error Code free-text-only entry with a dropdown + typeahead workflow.
4. Apply non-glaring theme-consistent styling to Updates GridView headers.

## Non-Goals

- Large client tools feature expansion.
- New backend services unrelated to current operations.
- Rewriting existing operation execution logic.

## User-Validated Requirements

- Updates should not display mock rows.
- Target Host input box is currently too large and should be tightened.
- Error Code entry should support a common-code dropdown with search/typeahead.
- Client Tools layout should be reorganized to be denser and easier to scan.
- Updates column headers should match app theme (not bright white).

## Approaches Considered

### A) Targeted In-Place UX and Data Cleanup (Selected)

- Keep current panel structure and command bindings.
- Remove mock update feed and use existing dashboard service real-data API path.
- Compact layout using existing style resources and spacing adjustments.
- Introduce editable ComboBox for Error Code lookup.
- Apply existing virtualized list header theming to Updates list.

Pros: Low risk, fast delivery, minimal churn.
Cons: Less dramatic visual redesign.

### B) Full Client Tools Layout Redesign

- Convert section cards into a new two-column dashboard-like composition.

Pros: Potentially best long-term information density.
Cons: Higher regression risk and larger test surface.

### C) Styling-Only Changes

- Keep behavior mostly unchanged, adjust control sizes and colors only.

Pros: Fastest.
Cons: Does not solve mock data requirement.

## Selected Design

### 1) Updates Data Source

Use real update metadata path in `IDashboardService.GetUpdatesAsync(AppSettings,...)` from `MainViewModel.LoadUpdatesAsync()`.

Design notes:

- Eliminate mock row generation path from `DashboardService.GetUpdatesAsync(CancellationToken)`.
- Keep panel lazy-load semantics (load on first navigation) and existing empty-state behavior.
- Preserve resilient behavior on failures (log + empty list, no crash).

### 2) Client Tools Compact Layout

Client Tools panel remains card-based but with denser vertical rhythm.

Changes:

- Target Host card: reduce explanatory text footprint and constrain hostname input width.
- Remote Operations card: reduce margins/padding between controls; preserve wrapping for smaller widths.
- Script Generator and Error Lookup cards: keep functionality, tighten spacing and labels.
- Keep current command bindings and automation IDs where possible.

### 3) Error Code Lookup UX

Replace `TextBox` input with editable `ComboBox` bound to a `CommonErrorCodes` list.

Behavior:

- Operator can choose common codes from dropdown.
- Operator can type custom codes directly (editable ComboBox).
- Existing `LookupErrorCodeCommand` continues as explicit action trigger.
- Optional placeholder helper text remains outside control for discoverability.

### 4) Updates Header Theming

Apply themed column header style via existing `VirtualizedListView` style and/or explicit `GridView.ColumnHeaderContainerStyle` reference.

Visual goal:

- Header background and text use theme resources (`NavBackground`, `TextSecondary`, `BorderPrimary`).
- No bright/default white header surface.

## Testing Strategy

1. ViewModel/service tests verifying Updates path no longer depends on mock list generation.
2. XAML structural test asserting no invalid trigger structures and style application for Updates list.
3. Build verification for app and targeted tests for keyboard/XAML structure and client tools bindings.
4. Manual smoke check:
   - Navigate to Updates: no sample rows, no crashes.
   - Client Tools: compact layout, narrower hostname field.
   - Error lookup: dropdown selection and typed custom code both work.

## Acceptance Criteria

1. Updates panel shows only real WSUS-backed update data or empty state.
2. Client Tools panel is visibly more compact and scan-friendly.
3. Error Code input supports dropdown + typeahead while retaining manual entry.
4. Updates column headers follow theme and are no longer glaring white.
5. Existing operation commands and accessibility automation IDs remain functional.
