# Architecture Decision Records

This index enumerates the accepted Architecture Decision Records (ADRs) under `docs/adrs/`. It is metadata-only. The reusable template `0000-template.md` is a non-policy placeholder and is intentionally excluded from this index of accepted decisions.

<!-- adr-index -->

| ADR | Title | Decision identifiers |
|---|---|---|
| `0001-folder-domain-processor-persistence.md` | Folder domain processor persistence ownership | Story 2.8b |
| `0002-contract-spine-single-source-of-truth.md` | Contract Spine as the single source of truth | `C0`, `A-1`, `A-2`, `A-3`, `C13` |
| `0003-provider-abstraction-and-capability-model.md` | Provider abstraction and capability model | `A-6`, `A-7`, `C12` |
| `0004-per-command-canonical-idempotency.md` | Per-command canonical idempotency hashing | `A-9`, `D-7` |
| `0005-layered-authorization-and-oidc.md` | Layered authorization and frozen OIDC validation | `S-4`, `S-2`, `S-6`, `C9` |
| `0006-observability-and-operational-signals.md` | OpenTelemetry observability and operational signals | `I-6`, `I-7`, `C2` |
| `0007-container-deployment-with-dapr.md` | Container deployment with Dapr sidecars and stable app IDs | `I-2`, `I-3`, `I-4`, `I-1` |

<!-- /adr-index -->

## Maintenance

This index is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The gate fails closed if the index drifts from the files on disk (a missing entry or an orphan entry). CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
