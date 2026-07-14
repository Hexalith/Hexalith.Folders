# Provider Drift Runbook

This runbook is the operator-facing response to provider schema drift detected by the nightly oasdiff lane. It is metadata-only and uses synthetic examples only. It cross-links the authoritative drift sources rather than restating their classification tables.

## Purpose

Give operators a decision procedure for additive, breaking, and unknown provider drift, so a drift signal becomes an action rather than a mystery. The drift-detection workflow and the supported-version catalog live in the cross-linked sources and are not duplicated here.

## Preconditions

- The acting principal is authorized to review provider readiness evidence; tenant authority comes from authenticated context, never from a query parameter.
- The nightly drift lane has produced a sanitized, metadata-only classification report; raw schema diffs are never retained.
- No triage step performs cross-tenant search, provider payload inspection, raw file inspection, or credential review.

## Procedure

Operator response by drift classification:

1. Additive drift - warning-class evidence that remains visible. No immediate action; record the classification and continue. Additive changes do not fail the workflow.
2. Breaking drift - classified `SchemaDriftBreaking`; this maps to `ReconciliationRequired` and fails the workflow. Pin the affected provider version, open a provider-adapter change, and do not promote the drifted version.
3. Version-incompatible drift - classified `VersionIncompatible`; this maps to `ReconciliationRequired`. An unsupported or failing provider version cannot report ready; readiness fails closed.
4. Unknown or unclassified drift - treated as failing. Do not guess; escalate for classification before any provider version is promoted.

The supported Forgejo versions are pinned by the catalog and backed by per-version snapshots; a missing snapshot or stale integrity hash fails the workflow.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The drift detection is validated by `pwsh ./tests/tools/run-nightly-drift-gates.ps1` and `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1`. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.

## Escalation and handoff

- Breaking or unknown drift escalates to the Provider Readiness owner with the classification token and the affected version only.
- A drift that has already produced an ambiguous in-flight outcome hands off to the reconciliation runbook (`./reconciliation.md`); never silently retry a mutation whose outcome is unknown.

## Related evidence

- `../operations/scheduled-drift-and-policy-conformance.md` - the nightly drift detection and classification workflow.
- `../operations/provider-integration-and-testing.md` - the provider abstraction, supported versions, and failure mapping.

## Forbidden evidence

Provider-drift evidence is metadata-only. It must not include credentials, tokens, raw schemas, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
