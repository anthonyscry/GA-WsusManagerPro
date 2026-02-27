# Proposal: Client Tools Subtabs and Card Iteration Cleanup

## Purpose

Improve Client Tools panel readability by organizing cards into subtabs and rendering card blocks through foreach-style iteration instead of a long static stack.

## Scope

- Add Client Tools subtabs to group related card content.
- Replace static card stacking with `ItemsControl`-driven card iteration per subtab.
- Keep existing commands, bindings, automation IDs, and behavior unchanged.

Out of scope:

- Service-layer behavior changes for client operations.
- Command renaming or workflow logic changes.
- New backend features.

## Acceptance Criteria

1. Client Tools panel shows at least two subtabs with grouped content.
2. Cards in each subtab are rendered via `ItemsControl` (foreach-style binding).
3. Existing Client Tools command bindings still execute as before.
4. Existing automation IDs in card controls remain present.

## Risks

- XAML refactor risk could break bindings or layout.
- Tabbed grouping could hide controls users are used to seeing at once.

Mitigations:

- Keep all existing control bindings and command references unchanged.
- Maintain current card content and only reorganize structure.
