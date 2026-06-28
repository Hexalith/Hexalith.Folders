# NFR Traceability Bridge

This document is the release-review bridge that maps every Product Requirements Document (PRD)
non-functional requirement (NFR) bullet to concrete implementation evidence. It exists so MVP acceptance can
prove non-functional coverage from sources rather than rely on narrative claims. Missing or unowned NFR
evidence fails the release-readiness review.

## Source authorities

The traceability table below is re-derived from these authorities by
`NfrTraceabilityConformanceTests`; it is never hand-maintained in isolation:

- PRD NFR inventory: `_bmad-output/planning-artifacts/prd.md` `## Non-Functional Requirements` — 70 bullets
  across nine categories.
- Epics NFR inventory: `_bmad-output/planning-artifacts/epics.md` numbered `NFR1` through `NFR70`,
  intentionally aligned one-for-one with the PRD after the 2026-05-12 readiness-artifact patch.
- Architecture exit criteria: `docs/exit-criteria/c0-c13-governance-evidence.yaml` C0 through C13, including
  owner and `reference_pending` semantics, plus `_bmad-output/planning-artifacts/architecture.md`.
- Epic 7 automated gate reports: `_bmad-output/gates/<gate>/latest.json` and their focused
  `tests/tools/run-*-gates.ps1` runners.
- Release-validation and manual-evidence artifacts: `docs/exit-criteria/*.md`, `docs/operations/*.md`,
  `docs/ux/*.md`, and `docs/runbooks/*.md`.

The PRD bullet text is referenced by a stable 12-character SHA-256 hash (`PRD bullet hash`) to keep rows
compact; the conformance test re-derives each hash from `prd.md` and asserts the PRD bullet and the matching
`epics.md` `NFRn` text are identical, so PRD/epics drift fails closed until this table is updated.

## Status semantics

- `covered` — at least one current automated gate or CI evidence path proves the requirement.
- `release-validation` — proven by release-validation evidence (for example same-commit capacity calibration)
  that is collected at release time rather than on every PR.
- `reference-pending` — an owned gap remains. The row stays visible with owner, gap, consuming story, and
  release-blocking semantics. Reference-pending rows are **not** converted to covered by writing narrative;
  they block the release-readiness review until their owner records the missing evidence.

## NFR traceability table

<!-- nfr-traceability-table -->
| NFR | Category | PRD bullet hash | Status | Stories | Automated gates | Architecture / exit criteria | Release-validation artifacts | Owner | Release-blocking note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| NFR1 | Security & Tenant Isolation | f33274b9da03 | covered | `7-6` | `tests/tools/run-safety-invariant-gates.ps1` | `C8` `C9` | — | Safety Invariants | Not release-blocking; automated evidence current. |
| NFR2 | Security & Tenant Isolation | 2897c4c5712f | covered | `7-6` | `tests/tools/run-safety-invariant-gates.ps1` | `C9` | — | Safety Invariants | Not release-blocking; automated evidence current. |
| NFR3 | Security & Tenant Isolation | c1e203a32fb3 | covered | `7-6` | `tests/tools/run-safety-invariant-gates.ps1` | `C9` | — | Safety Invariants | Not release-blocking; automated evidence current. |
| NFR4 | Security & Tenant Isolation | 8dcefb5d7669 | covered | `7-6` | `tests/tools/run-security-redaction-ci-gates.ps1` `tests/tools/run-safety-invariant-gates.ps1` | `C9` | — | Security | Not release-blocking; automated evidence current. |
| NFR5 | Security & Tenant Isolation | b3b6047dbf5b | covered | `7-6` | `tests/tools/run-security-redaction-ci-gates.ps1` | `C9` | — | Security | Not release-blocking; automated evidence current. |
| NFR6 | Security & Tenant Isolation | 4980431b2d0b | covered | `7-6` | `tests/tools/run-safety-invariant-gates.ps1` | `C8` | — | Safety Invariants | Not release-blocking; automated evidence current. |
| NFR7 | Security & Tenant Isolation | 0d7ced37babf | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/production-identity-and-secrets.md` | — | Security | Not release-blocking; automated evidence current. |
| NFR8 | Security & Tenant Isolation | 856688113814 | covered | `7-6` `7-15` | `tests/tools/run-security-redaction-ci-gates.ps1` `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Security | Not release-blocking; automated evidence current. |
| NFR9 | Security & Tenant Isolation | 1c2567dc7169 | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR10 | Security & Tenant Isolation | 3268e76539e5 | covered | `7-9` | `tests/tools/run-release-package-gates.ps1` | `C0` | `_bmad-output/gates/release-packages/latest.json` | Release Readiness | Not release-blocking; automated evidence current. |
| NFR11 | Reliability, Idempotency & Failure Visibility | b9ce8167f2af | covered | `4-1` | `tests/tools/run-contract-spine-gates.ps1` | `C6` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR12 | Reliability, Idempotency & Failure Visibility | b77e1a838a28 | covered | `4-1` | `tests/tools/run-contract-spine-gates.ps1` | `C6` | `docs/exit-criteria/c6-transition-matrix-mapping.md` | Lifecycle | Not release-blocking; automated evidence current. |
| NFR13 | Reliability, Idempotency & Failure Visibility | 962788e1f638 | covered | `4-13` | `tests/tools/run-contract-spine-gates.ps1` | `C6` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR14 | Reliability, Idempotency & Failure Visibility | 836184d672cd | covered | `4-11` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR15 | Reliability, Idempotency & Failure Visibility | c122096887a2 | covered | `4-11` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR16 | Reliability, Idempotency & Failure Visibility | 72b863dac04f | covered | `4-11` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR17 | Reliability, Idempotency & Failure Visibility | 896a912e6268 | covered | `4-3` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR18 | Reliability, Idempotency & Failure Visibility | 529873496722 | reference-pending | `4-3` | `tests/tools/run-governance-completeness-gates.ps1` | `C7` | — | Architecture | Release-blocking: C7 lock revalidation budget + mid-task revocation evidence deferred. |
| NFR19 | Reliability, Idempotency & Failure Visibility | f0aa0ecd8f0e | covered | `4-4` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR20 | Reliability, Idempotency & Failure Visibility | 18996c70babc | covered | `4-13` | `tests/tools/run-contract-parity-ci-gates.ps1` | `docs/operations/canonical-error-catalog.md` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR21 | Performance & Query Bounds | 5dcf13f87ec6 | release-validation | `7-10` | `tests/tools/run-capacity-calibration-gates.ps1` | `C1` | `_bmad-output/gates/capacity-calibration/latest.json` | Release Readiness | Release-validation: 1s p95 ack baseline pinned by same-commit capacity calibration. |
| NFR22 | Performance & Query Bounds | ce049eb4961a | release-validation | `7-10` | `tests/tools/run-capacity-calibration-gates.ps1` | `C2` | `docs/exit-criteria/c2-freshness.md` | Release Readiness | Release-validation: 500ms p95 status/audit baseline pinned by same-commit calibration. |
| NFR23 | Performance & Query Bounds | c59956e01a76 | release-validation | `7-10` | `tests/tools/run-capacity-smoke-ci-gates.ps1` `tests/tools/run-capacity-calibration-gates.ps1` | `C2` | `docs/exit-criteria/c2-freshness.md` | Release Readiness | Release-validation: 2s p95 context-query baseline pinned by same-commit calibration. |
| NFR24 | Performance & Query Bounds | db90b938e079 | release-validation | `7-10` | `tests/tools/run-capacity-calibration-gates.ps1` | `C1` `C5` | `docs/exit-criteria/c1-capacity.md` `docs/exit-criteria/c5-scalability-quantifiers.md` | Release Readiness | Release-validation: targets recalibrated before release. |
| NFR25 | Performance & Query Bounds | 55b450f868de | covered | `4-12` | `tests/tools/run-contract-spine-gates.ps1` | `C0` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR26 | Performance & Query Bounds | 84e1fdefb7f8 | reference-pending | `4-8` | `tests/tools/run-contract-spine-gates.ps1` | `C4` | `docs/exit-criteria/c4-input-limits.md` | PM | Release-blocking: C4 PM approval of OpenAPI maxItems/maxLength/maxBytes/maxResultCount pending. |
| NFR27 | Performance & Query Bounds | 413fc41476f4 | covered | `4-8` | `tests/tools/run-contract-spine-gates.ps1` | `C11` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR28 | Performance & Query Bounds | 4278f2be8378 | reference-pending | `4-8` | `tests/tools/run-contract-spine-gates.ps1` | `C4` | `docs/exit-criteria/c4-input-limits.md` | PM | Release-blocking: C4 PM approval of large-file/payload limits pending. |
| NFR29 | Performance & Query Bounds | 17a95286cc6f | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR30 | Performance & Query Bounds | e5af07b5a94b | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/canonical-error-catalog.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR31 | Performance & Query Bounds | 500d33e43643 | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/canonical-error-catalog.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR32 | Scalability & Capacity | 28e831a8e08e | covered | `7-10` | `tests/tools/run-safety-invariant-gates.ps1` `tests/tools/run-capacity-calibration-gates.ps1` | `C1` `C5` | — | Release Readiness | Not release-blocking; automated evidence current. |
| NFR33 | Scalability & Capacity | 7829eff54ee7 | covered | `7-10` | `tests/tools/run-capacity-calibration-gates.ps1` | `C5` | — | Release Readiness | Not release-blocking; automated evidence current. |
| NFR34 | Scalability & Capacity | 428a57294347 | release-validation | `7-10` | `tests/tools/run-capacity-smoke-ci-gates.ps1` | `C5` | `_bmad-output/gates/capacity-smoke-ci/latest.json` | Release Readiness | Release-validation: projection query bounds proven by capacity smoke evidence. |
| NFR35 | Scalability & Capacity | 8618b3457abe | release-validation | `7-7` | `tests/tools/run-capacity-smoke-ci-gates.ps1` | `C5` | `_bmad-output/gates/capacity-smoke-ci/latest.json` | Release Readiness | Release-validation: large-batch traceability proven by capacity smoke evidence. |
| NFR36 | Scalability & Capacity | 393775d8f99d | release-validation | `7-10` | `tests/tools/run-capacity-calibration-gates.ps1` | `C1` `C5` | `docs/exit-criteria/c1-capacity.md` `docs/exit-criteria/c5-scalability-quantifiers.md` | Release Readiness | Release-validation: capacity targets pinned by same-commit calibration. |
| NFR37 | Integration & Contract Compatibility | df78ab7f3fdd | covered | `5-5` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | C13 Parity Oracle | Not release-blocking; automated evidence current. |
| NFR38 | Integration & Contract Compatibility | ddf0535112fe | covered | `1-6` | `tests/tools/run-contract-spine-gates.ps1` | `C0` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR39 | Integration & Contract Compatibility | 89ab6f37f995 | covered | `1-14` | `tests/tools/run-contract-spine-gates.ps1` | `C0` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR40 | Integration & Contract Compatibility | 2b7bb19e73e1 | covered | `5-5` | `tests/tools/run-contract-parity-ci-gates.ps1` | `C13` | — | C13 Parity Oracle | Not release-blocking; automated evidence current. |
| NFR41 | Integration & Contract Compatibility | d4cd55522f9a | covered | `1-12` | `tests/tools/run-contract-spine-gates.ps1` | `C0` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR42 | Integration & Contract Compatibility | 5caafe2bc9d7 | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR43 | Integration & Contract Compatibility | 223b2798ee1e | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR44 | Integration & Contract Compatibility | b8a0cf3f2853 | reference-pending | `7-8` | `tests/tools/run-nightly-drift-gates.ps1` | `C12` | `docs/operations/provider-integration-and-testing.md` | Provider Readiness | Release-blocking: C12 live provider drift checks require provider credentials absent in CI. |
| NFR45 | Integration & Contract Compatibility | eb5f2d757da8 | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/provider-integration-and-testing.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR46 | Integration & Contract Compatibility | 3babf7ce08ff | covered | `7-15` | `tests/tools/run-provider-error-docs-gates.ps1` | `docs/operations/canonical-error-catalog.md` | — | Provider Readiness | Not release-blocking; automated evidence current. |
| NFR47 | Observability, Auditability & Replay | 1fbdb3264cbd | covered | `7-12` | `tests/tools/run-operations-audit-docs-gates.ps1` `tests/tools/run-production-observability-gates.ps1` | `docs/operations/production-observability.md` | — | Observability | Not release-blocking; automated evidence current. |
| NFR48 | Observability, Auditability & Replay | cef18e07d812 | covered | `7-14` | `tests/tools/run-safety-invariant-gates.ps1` `tests/tools/run-operations-audit-docs-gates.ps1` | `C9` | — | Audit | Not release-blocking; automated evidence current. |
| NFR49 | Observability, Auditability & Replay | 132143e79723 | covered | `7-14` | `tests/tools/run-operations-audit-docs-gates.ps1` | `docs/operations/audit-and-redaction.md` | — | Audit | Not release-blocking; automated evidence current. |
| NFR50 | Observability, Auditability & Replay | 95f25de2c562 | covered | `7-6` | `tests/tools/run-security-redaction-ci-gates.ps1` `tests/tools/run-operations-audit-docs-gates.ps1` | `C9` | `docs/operations/audit-and-redaction.md` | Audit | Not release-blocking; automated evidence current. |
| NFR51 | Observability, Auditability & Replay | 746cd2c73653 | covered | `6-11` | `tests/tools/run-operations-audit-docs-gates.ps1` | `docs/operations/operations-console.md` | — | Observability | Not release-blocking; automated evidence current. |
| NFR52 | Observability, Auditability & Replay | 145faedebc7b | covered | `4-15` | `tests/tools/run-contract-spine-gates.ps1` | `C6` | — | Lifecycle | Not release-blocking; automated evidence current. |
| NFR53 | Observability, Auditability & Replay | 75999519d2f2 | covered | `7-12` | `tests/tools/run-production-observability-gates.ps1` | `C2` | `docs/exit-criteria/c2-freshness.md` | Observability | Not release-blocking; automated evidence current. |
| NFR54 | Observability, Auditability & Replay | 4ff7a29e44ff | reference-pending | `7-17` | `tests/tools/run-production-observability-gates.ps1` | `docs/operations/production-observability.md` | `docs/operations/production-observability.md` | Operations Runbook (Story 7.17) | Release-blocking: alert-rule intent covered; live alert delivery tooling deferred to Story 7.17. |
| NFR55 | Observability, Auditability & Replay | cb53be1e3646 | reference-pending | `7-17` | `tests/tools/run-production-observability-gates.ps1` | `docs/operations/production-observability.md` | `docs/operations/production-observability.md` | Operations Runbook (Story 7.17) | Release-blocking: backup/restore tooling + recovery-drill evidence deferred to Story 7.17. |
| NFR56 | Data Retention & Cleanup | dd3d708f80df | covered | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` | `C3` | `docs/exit-criteria/c3-retention.md` | Tech Lead | Not release-blocking; automated evidence current. |
| NFR57 | Data Retention & Cleanup | 8024208f74c4 | reference-pending | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` | `C3` | `docs/exit-criteria/c3-retention.md` | Legal + PM | Release-blocking: C3 Legal + PM approval of retention durations pending. |
| NFR58 | Data Retention & Cleanup | 4d4eb929d8b6 | covered | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` | `docs/runbooks/tenant-deletion.md` | — | Tech Lead | Not release-blocking; automated evidence current. |
| NFR59 | Data Retention & Cleanup | 1c992d43fe37 | covered | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` | `docs/operations/retention-and-tenant-deletion.md` | — | Tech Lead | Not release-blocking; automated evidence current. |
| NFR60 | Data Retention & Cleanup | e17d52781d8a | covered | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` | `docs/operations/retention-and-tenant-deletion.md` | — | Tech Lead | Not release-blocking; automated evidence current. |
| NFR61 | Data Retention & Cleanup | 256c0e6fde19 | covered | `7-11` | `tests/tools/run-retention-deletion-gates.ps1` `tests/tools/run-safety-invariant-gates.ps1` | `C9` | — | Audit | Not release-blocking; automated evidence current. |
| NFR62 | Operations-Console Accessibility | ff52de1235b4 | release-validation | `6-11` `7-17` `8-4` | `tests/tools/run-accessibility-ci-gates.ps1` | `docs/operations/accessibility-ci-gates.md` | `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | UX / Release Validation | Release-validation: axe / WCAG 2.2 AA CI gate (Story 8.4) automates contrast, structure, keyboard/focus, and zoom; manual screen-reader review remains owned release-validation evidence. |
| NFR63 | Operations-Console Accessibility | 3b162432839f | covered | `6-11` `8-4` | `tests/tools/run-accessibility-ci-gates.ps1` | `docs/operations/accessibility-ci-gates.md` | `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | UX / Release Validation | Not release-blocking; keyboard-navigation operability automated by the axe gate's Playwright keyboard/focus assertions (Story 8.4). |
| NFR64 | Operations-Console Accessibility | 3781a887fa7e | covered | `6-3` | `tests/tools/run-operations-audit-docs-gates.ps1` | `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | — | UX / Release Validation | Not release-blocking; automated evidence current. |
| NFR65 | Operations-Console Accessibility | cb8877f00337 | covered | `6-11` `8-4` | `tests/tools/run-accessibility-ci-gates.ps1` | `docs/operations/accessibility-ci-gates.md` | `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | UX / Release Validation | Not release-blocking; focus-visible, heading structure, and contrast automated by the axe gate (Story 8.4). |
| NFR66 | Operations-Console Accessibility | 5cde1ea41661 | covered | `6-10` `8-4` | `tests/tools/run-accessibility-ci-gates.ps1` | `docs/operations/accessibility-ci-gates.md` | `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | UX / Release Validation | Not release-blocking; 125/150/200% browser-zoom + dense-identifier no-clipping automated by the axe gate (Story 8.4). |
| NFR67 | Verification Expectations | f6704879b0f5 | covered | `7-16` | `tests/tools/run-nfr-traceability-gates.ps1` | `C0` | `_bmad-output/gates/nfr-traceability/latest.json` | Release Readiness | Not release-blocking; automated evidence current. |
| NFR68 | Verification Expectations | fc40fb7b47cc | covered | `5-5` | `tests/tools/run-contract-parity-ci-gates.ps1` `tests/tools/run-safety-invariant-gates.ps1` | `C13` | — | Contracts | Not release-blocking; automated evidence current. |
| NFR69 | Verification Expectations | e924c41216c4 | release-validation | `7-10` `7-11` | `tests/tools/run-capacity-calibration-gates.ps1` | `C1` `C3` | `docs/exit-criteria/c1-capacity.md` `docs/exit-criteria/c3-retention.md` `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` | Release Readiness | Release-validation: perf/a11y/retention/backup evidence consolidated; backup remains reference-pending; a11y now automated by the axe / WCAG 2.2 AA CI gate (Story 8.4). |
| NFR70 | Verification Expectations | e0b208ef5e3d | covered | `7-9` | `tests/tools/run-release-package-gates.ps1` `tests/tools/run-security-redaction-ci-gates.ps1` | `C0` | — | Security | Not release-blocking; automated evidence current. |
<!-- /nfr-traceability-table -->

## Nine-category coverage rollup

Every PRD/architecture NFR category is represented; the counts sum to the full 70-bullet inventory.

<!-- nfr-category-rollup -->
| Category | NFR range | Count | Representative evidence |
| --- | --- | --- | --- |
| Security & Tenant Isolation | NFR1–NFR10 | 10 | `tests/tools/run-safety-invariant-gates.ps1` `tests/tools/run-security-redaction-ci-gates.ps1` |
| Reliability, Idempotency & Failure Visibility | NFR11–NFR20 | 10 | `tests/tools/run-contract-spine-gates.ps1` `tests/tools/run-contract-parity-ci-gates.ps1` |
| Performance & Query Bounds | NFR21–NFR31 | 11 | `tests/tools/run-capacity-calibration-gates.ps1` `tests/tools/run-provider-error-docs-gates.ps1` |
| Scalability & Capacity | NFR32–NFR36 | 5 | `tests/tools/run-capacity-calibration-gates.ps1` `tests/tools/run-capacity-smoke-ci-gates.ps1` |
| Integration & Contract Compatibility | NFR37–NFR46 | 10 | `tests/tools/run-contract-parity-ci-gates.ps1` `tests/tools/run-contract-spine-gates.ps1` |
| Observability, Auditability & Replay | NFR47–NFR55 | 9 | `tests/tools/run-production-observability-gates.ps1` `tests/tools/run-operations-audit-docs-gates.ps1` |
| Data Retention & Cleanup | NFR56–NFR61 | 6 | `tests/tools/run-retention-deletion-gates.ps1` `docs/exit-criteria/c3-retention.md` |
| Operations-Console Accessibility | NFR62–NFR66 | 5 | `tests/tools/run-accessibility-ci-gates.ps1` `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` |
| Verification Expectations | NFR67–NFR70 | 4 | `tests/tools/run-nfr-traceability-gates.ps1` `tests/tools/run-capacity-calibration-gates.ps1` |
<!-- /nfr-category-rollup -->

## BDD-required release-review evidence rollup

The Story 7.16 acceptance BDD names six release-review evidence classes. Each is mapped to concrete evidence
below; the conformance test asserts all six classes are present and each cites at least one existing path.

<!-- nfr-bdd-evidence-rollup -->
| Evidence class | Status | Evidence |
| --- | --- | --- |
| `tenant-isolation-security-gates` | covered | `tests/tools/run-safety-invariant-gates.ps1` `tests/tools/run-security-redaction-ci-gates.ps1` `tests/tools/run-dapr-policy-conformance-gates.ps1` |
| `audit-completeness` | covered | `tests/tools/run-operations-audit-docs-gates.ps1` `docs/operations/audit-and-redaction.md` |
| `workspace-and-context-query-performance-baselines` | release-validation | `tests/tools/run-capacity-calibration-gates.ps1` `docs/exit-criteria/c2-freshness.md` |
| `cli-mcp-smoke-parity` | covered | `tests/tools/run-contract-parity-ci-gates.ps1` `tests/fixtures/parity-contract.yaml` |
| `console-accessibility-responsive-validation` | release-validation | `tests/tools/run-accessibility-ci-gates.ps1` `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` `docs/ux/ops-console-performance-budget.md` |
| `operational-runbook-proof` | reference-pending | `docs/runbooks/tenant-deletion.md` `docs/operations/incident-alerting-and-recovery.md` |
<!-- /nfr-bdd-evidence-rollup -->

## Reference-pending release-blocking gaps

These owned gaps remain open and block the release-readiness review until their owner records the missing
evidence. They are kept honest here and in `_bmad-output/gates/nfr-traceability/latest.json`
(`release_blocking_gaps`); narrative cannot convert them to covered. Story 7.17 owns the ADR set and the
maintenance runbooks for alerts, rollback, provider drift, reconciliation, and incident-mode operations — this
bridge only cross-links to existing evidence and does not author those artifacts.

- `C7` — lock revalidation budget and mid-task revocation evidence. Owner: Architecture. Consuming story:
  `4-3`. Surfaced by NFR18.
- `C4` — PM-approved context-query input bounds and large-file/payload limits are recorded in
  `docs/exit-criteria/c4-input-limits.md`. This row remains reference-pending only for downstream
  evidence and conformance-guard visibility. Owner: PM. Consuming story: `4-8`. Surfaced by NFR26
  and NFR28.
- `C12` — live provider drift checks requiring provider credentials absent in CI. Owner: Provider Readiness.
  Consuming story: `7-8`. Surfaced by NFR44.
- `C3` — Legal + PM-approved retention durations and tenant-deletion dispositions are recorded in
  `docs/exit-criteria/c3-retention.md`. This row remains reference-pending only for downstream
  evidence and conformance-guard visibility. Owner: Legal + PM. Consuming story: `7-11`. Surfaced
  by NFR57.
- Live alert delivery tooling and backup/restore + recovery-drill tooling. Owner: Operations Runbook (Story
  7.17). Consuming story: `7-17`. Surfaced by NFR54 and NFR55.

Operations-console accessibility (NFR62, NFR63, NFR65, NFR66) is no longer a release-blocking gap: Story 8.4
wired the automated axe / WCAG 2.2 AA CI gate (`accessibility-gates`) covering contrast, structure,
keyboard/focus, and 125/150/200 % zoom + dense-identifier no-clipping. The genuinely-manual residuals —
screen-reader, forced-colors, and color-blindness review — remain owned release-validation evidence in
`docs/ux/ops-console-accessibility-and-no-mutation-verification.md`, not release-blocking gaps.

## Metadata-only policy

This document, the gate report, and the conformance diagnostics are output channels subject to the
metadata-only invariant: no secrets, bearer tokens, credential material, raw file contents, diffs, provider
payloads, raw provider responses, real repository URLs, embedded-credential URLs, production hosts,
environment dumps, stack traces, tenant data, or host-absolute paths. Evidence is cited by repository-relative
paths, opaque criterion identifiers, status values, and stable text hashes only.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-nfr-traceability-gates.ps1
```

The gate runs `NfrTraceabilityConformanceTests` and writes a metadata-only report to
`_bmad-output/gates/nfr-traceability/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`) when the
shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate falls back to
the xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm the report reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the traceability table still contains exactly 70 rows that
align one-for-one with the PRD and `epics.md` `NFR1` through `NFR70`. Confirm every reference-pending row keeps
an owner and a release-blocking note, and that the report `release_blocking_gaps` stay in sync with the
reference-pending rows. Rerun the gate after any change to the PRD NFR inventory, the `epics.md` NFR
inventory, the architecture exit criteria, or any cited gate, exit-criteria, or release-validation artifact.
The static gate runs in the `contract-spine` CI lane, through the baseline CI Contracts.Tests filter, and as a
release-readiness prerequisite in `release-packages.yml`; it is not promoted to a new top-level `ci.yml` lane
or to scheduled workflows.
