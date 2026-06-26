# Incident-Mode Runbook

This runbook is the operator-facing procedure for the last-resort incident-mode read path. It is metadata-only and uses synthetic examples only. It addresses a genuine operator gap that the existing operations docs do not yet cover.

## Purpose

Give operators a procedure for when to use the `/_admin/incident-stream` last-resort read path, how to interpret operator-disposition labels, and how to escalate and hand off. The console route inventory and the recovery boundary live in the cross-linked sources and are not duplicated here.

## Preconditions

- The acting principal is authorized for the managed tenant; tenant authority always comes from the authenticated context and never from the `?folder=` query value, which is a comparison input only.
- Incident mode is a read path. Redaction does not relax in incident mode, and the console exposes no mutation, repair, unlock, retry, discard, credential reveal, file browsing, or raw-diff display.
- Use incident mode only when the normal status and timeline views are stale or unavailable.

## Procedure

1. Open the last-resort read path `/_admin/incident-stream?folder={folderId}`. As shipped in MVP this is a degraded-projection-tolerant read over the existing operation-timeline projection (the same `ListOperationTimelineAsync` read), used only when the normal status and timeline views are stale; a truly projection-independent authoritative event-stream read remains `reference_pending` and is not shipped in MVP.
2. Read the persistent degraded-mode banner: it shows the last projection checkpoint as a UTC timestamp, rendered `unknown` when absent (never `0001-01-01`).
3. Interpret the operator-disposition label shown beside the raw technical state transition:
   - `auto_recovering` - in flight; no operator action needed yet.
   - `available` - healthy with no projection-lag evidence.
   - `degraded_but_serving` - serving under projection lag; watch, do not intervene.
   - `awaiting_human` - an ambiguous outcome needing a reconciliation decision (see `./reconciliation.md`); never retried silently.
   - `terminal_until_intervention` - a categorized failure that will not self-recover.
4. Use one-click copy of the `correlationId` and the UTC timestamp window for the handoff record; copy metadata only.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The incident-mode contract is validated by `pwsh ./tests/tools/run-operations-audit-docs-gates.ps1`. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants`.

## Escalation and handoff

- An `awaiting_human` or `terminal_until_intervention` disposition escalates to the on-call operator; hand off the `correlationId` and UTC window only, never raw event bodies.
- A degraded mode that persists after projections should have recovered hands off to the alerts runbook (`./alerts.md`) and, if rollback is implicated, the rollback runbook (`./rollback.md`).

## Related evidence

- `../operations/incident-alerting-and-recovery.md` - the incident-mode read contract and the recovery boundary.
- `../operations/operations-console.md` - the read-only console routes, disposition vocabulary, and no-mutation guarantees.

## Forbidden evidence

Incident-mode evidence is metadata-only. It must not include credentials, tokens, raw event bodies, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
