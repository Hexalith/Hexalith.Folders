# Reconciliation Runbook

This runbook is the operator decision tree for ambiguous provider outcomes. It is metadata-only and uses synthetic examples only. It addresses a genuine operator gap that the existing operations docs do not yet cover.

## Purpose

Give operators a decision tree for the two ambiguous-outcome states - `unknown_provider_outcome` and `reconciliation_required` - with an explicit no-silent-retry rule. The canonical category mappings and the C6 transition matrix live in the cross-linked sources and are not duplicated here.

## Preconditions

- The acting principal is authorized for the managed tenant before any tenant-scoped reconciliation evidence is read; tenant authority comes from authenticated context, never from a query parameter.
- The operations console is read-only: it exposes no repair, unlock, retry, discard, or reconcile mutation; reconciliation decisions are recorded out-of-band, not executed from the console.
- No reconciliation step performs cross-tenant search, provider payload inspection, raw file inspection, or credential review.

## Procedure

Decision tree:

1. Read the workspace state. If it is `unknown_provider_outcome` (code `71`) or `reconciliation_required` (code `72`), its operator disposition is `awaiting_human`; it is deliberately not retryable.
2. Do not retry the mutation. There is no silent retry: retrying a mutation whose outcome is unknown could duplicate a repository, a file change, a commit, an audit record, or an idempotency record.
3. Establish the true upstream outcome out-of-band using the bounded provider correlation identifiers only (no raw payloads).
4. If the upstream mutation did complete, record the reconciled outcome so the idempotency ledger reflects reality; if it did not, record that the operation may be safely re-issued under a new decision.
5. Confirm the resulting state follows the C6 transition matrix: every (state, event) pair has a defined positive transition or an explicit `state_transition_invalid` rejection with state unchanged.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The category mappings are validated by the provider and error docs gate. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`.

## Escalation and handoff

- An `awaiting_human` outcome that cannot be reconciled from correlation evidence escalates to the on-call operator with the tenant-scoped synthetic identifiers and correlation ID only.
- A reconciliation that appears to need a code-level fix hands off to engineering; never apply a silent retry as a stopgap.

## Related evidence

- `../operations/canonical-error-catalog.md` - the `unknown_provider_outcome` and `reconciliation_required` categories and client actions.
- `../../_bmad-output/planning-artifacts/architecture.md` - the C6 workspace state-transition matrix.

## Forbidden evidence

Reconciliation evidence is metadata-only. It must not include credentials, tokens, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
