# Finalize HTTPS Install Documentation Alignment

## Purpose
Complete interrupted documentation work so install-time HTTPS options are consistently described for operators.

## Scope
- Update operator-facing docs to document optional installer HTTPS flags.
- Document the non-interactive guardrail that requires a certificate thumbprint.
- Keep behavior unchanged (documentation only).

## Acceptance Criteria
- `README-CONFLUENCE.md` documents `-EnableHttps` and `-CertificateThumbprint`.
- `docs/WSUS-Manager-SOP.md` documents the same flags and guardrail.
- Both docs include non-interactive HTTP and HTTPS examples.
- HTTPS optional section in SOP references install-time flag usage.

## Risks
- Low risk: documentation-only change.
- Main risk is operator confusion if examples drift from script behavior.
