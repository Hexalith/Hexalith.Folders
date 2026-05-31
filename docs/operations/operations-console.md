# Operations Console — Operator Workflows

The Hexalith.Folders operations console (`src/Hexalith.Folders.UI`) is a **read-only, projection-backed,
metadata-only** diagnostic surface for operators and audit reviewers. This document **summarizes and
cross-links** the Epic 6 UX sources; it does not re-author them. The authoritative UX specifications are:

- [`docs/ux/ops-console-wireflows.md`](../ux/ops-console-wireflows.md) — console views, critical journeys, incident mode, perceived-wait.
- [`docs/ux/ops-console-accessibility-and-no-mutation-verification.md`](../ux/ops-console-accessibility-and-no-mutation-verification.md) — WCAG 2.2 AA structural conformance and the no-mutation sweep.
- [`docs/ux/ops-console-performance-budget.md`](../ux/ops-console-performance-budget.md) — perceived-wait bands and release-validation page-load budgets.

Consumer API/SDK/CLI/MCP references and lifecycle diagrams are published separately under
[`docs/sdk/`](../sdk/) and [`docs/diagrams/`](../diagrams/) (Story 7.13) — cross-link, do not duplicate.

## Console host shape

The console is a **Blazor Web App host using Interactive Server rendering** (`AddInteractiveServerRenderMode()`),
titled `Hexalith.Folders Operations Console`, with Dapr app id `folders-ui`. It is deliberately minimal:

- It references **only `Hexalith.Folders.Client`** plus `Hexalith.FrontComposer.Shell`.
- It reads **only read-model projections** through `services.AddFoldersClient` (plus a
  `BearerTokenDelegatingHandler` that attaches the operator's bearer token to outbound reads).
- It **never calls `AddHexalithEventStore`**, and there is **no `/api/v1/commands` endpoint** in the host.
- It is **filter-rejection-only**: every console read passes `filter: null`. The server rejects any non-null
  metadata filter with `validation_error` / `filter_not_yet_supported` (todoRef `C4`). The console never
  offers a filter affordance, so the rejection path is a server contract, not a console feature.

All examples below use opaque synthetic identifiers (for example `folder_01HZY7Z6N7J4Q2X8Y9V0FLD001`,
`tenant-001`, `operation-001`) and placeholder hosts only.

## Routed page inventory

The console exposes **12 routed pages**: **10 operator pages** plus **2 dev-only galleries**. Each page renders
exactly one `<h1>` and a `data-testid="console-page-{name}-root"` root marker.

<!-- routed-page-inventory -->

### Operator pages (10)

| Page | Route | data-testid |
|---|---|---|
| Home | `/` | `console-page-home-root` |
| Tenants | `/tenants` | `console-page-tenants-root` |
| Folders | `/folders` | `console-page-folders-root` |
| FolderDetail | `/folders/{folderId}` | `console-page-folder-detail-root` |
| Workspace | `/folders/{folderId}/workspaces/{workspaceId}` | `console-page-workspace-root` |
| AuditTrail | `/folders/{folderId}/audit-trail` | `console-page-audit-trail-root` |
| OperationTimeline | `/folders/{folderId}/operation-timeline` | `console-page-operation-timeline-root` |
| Provider | `/folders/{folderId}/provider` | `console-page-provider-root` |
| ProviderSupport | `/providers/support` | `console-page-provider-support-root` |
| IncidentStream | `/_admin/incident-stream` | `console-page-incident-stream-root` |

The IncidentStream page scopes its folder through a `?folder=` **query parameter**
(`/_admin/incident-stream?folder={folderId}`), **not** a path segment. Tenant authority always comes from the
authenticated context, never from the `?folder=` query value.

### Dev-only galleries (2, non-production)

| Gallery | Route | data-testid |
|---|---|---|
| RedactionGallery | `/dev/redaction-gallery` | `console-page-redaction-gallery-root` |
| StateLabelGallery | `/dev/state-label-gallery` | `console-page-state-label-gallery-root` |

Both galleries are **non-production**: they render **only under `Env.IsDevelopment()`** and are never routed in
a production deployment. They exist as visual fixtures for redaction affordances and disposition labels.

## Critical operator journeys

The UX wireflows define **three critical operator journeys**. Each ends at a terminal evidence surface that
answers the **five trust questions**: (1) **what happened**, (2) **who or what caused it**, (3) **when** it
happened, (4) **from which surface** it came, and (5) **whether the evidence can be trusted**.

<!-- operator-journeys -->

| Journey | Name | Route path | Terminal evidence surface |
|---|---|---|---|
| J1 | Find-Workspace-and-Inspect-Trust-State | `/folders` → `/folders/{folderId}` → `/folders/{folderId}/workspaces/{workspaceId}` | workspace trust summary + trust matrix |
| J2 | Prove-Tenant-Isolation-and-Safe-Folder-Visibility | `/folders/{folderId}` (tenant-scope banner + metadata-only folder tree) → `/providers/support` | tenant-scoped folder visibility + provider readiness |
| J3 | Diagnose-Workspace-Failure-from-Evidence | `/folders/{folderId}/audit-trail` + `/folders/{folderId}/operation-timeline` → `/_admin/incident-stream?folder={folderId}` | incident-mode last-resort read |

Each journey is read-only: it inspects projection evidence and never mutates state. J3's escalation to the
incident stream is the documented last-resort path when projections are degraded (see
[`incident-alerting-and-recovery.md`](incident-alerting-and-recovery.md)).

## Operator-disposition vocabulary (F-4)

The console renders an **operator-disposition** as the primary lifecycle vocabulary. The F-4 rule is that
**disposition is the primary** vocabulary and the technical state name is **secondary, muted metadata** shown
beside it. Every status indicator is **non-color-only** —
text plus icon/shape plus an accessible label — so disposition is legible without relying on color. The
disposition is resolved by `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs`
(`ResolveDisposition` / `ResolveLabel`); drift against the server-side state machine is guarded by
`DispositionLabelParityTests`.

The vocabulary has **exactly 5 members** (wire form / operator label):

<!-- disposition-vocabulary -->

| Disposition (wire) | Operator label | Badge slot |
|---|---|---|
| `auto_recovering` | Auto-recovering | info |
| `available` | Available | success |
| `degraded_but_serving` | Degraded but serving | warning |
| `awaiting_human` | Awaiting human | warning |
| `terminal_until_intervention` | Terminal until intervention | danger |

### Technical lifecycle state catalog (C6)

The disposition derives from the **11 C6 technical lifecycle states** per `ResolveDisposition`. The technical
state name is the secondary metadata rendered beside the disposition badge.

<!-- technical-state-catalog -->

| Technical state (C6) | Operator disposition |
|---|---|
| `requested` | Auto-recovering |
| `preparing` | Auto-recovering |
| `ready` | Available |
| `locked` | Degraded but serving |
| `changes_staged` | Degraded but serving |
| `dirty` | Awaiting human |
| `committed` | Auto-recovering |
| `failed` | Terminal until intervention |
| `inaccessible` | Terminal until intervention |
| `unknown_provider_outcome` | Awaiting human |
| `reconciliation_required` | Awaiting human |

**Projection-lag rule:** `ready` maps to **`available` ONLY when there is no projection-lag evidence**. With
projection-lag evidence present, `ready` maps to **`degraded_but_serving`** instead
(`ResolveDisposition(state, hasProjectionLagEvidence)`). No other state is conditional.

## Seven no-mutation guarantees

The console is read-only / projection-backed / metadata-only. The UX accessibility spec verifies **seven
no-mutation guarantees** via `NoMutationConsoleSweepTests` (the five-selector sweep `form`, `fluentinputform`,
`fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]` renders empty on all twelve surfaces) and
`NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest`:

<!-- no-mutation-guarantees -->

1. **No mutation path** — no form, command manifest, or mutation affordance is registered or rendered.
2. **No credential reveal** — bearer tokens and provider credentials are never displayed.
3. **No file-content browsing** — file bytes and contents are never fetched or shown.
4. **No file editing** — there is no edit surface for any file or metadata.
5. **No raw-diff display** — diffs and patches are never rendered.
6. **No hidden repair action** — no unlock, retry, discard, or reconcile action exists.
7. **No unrestricted filesystem browsing** — there is no arbitrary path or directory explorer.

## Perceived-wait UX and accessibility (release-validation evidence, not CI gates)

The figures below are **release-validation evidence, not CI gates**. Measured numbers are `reference_pending`;
this story does **not** fabricate measured values, add an NBomber console harness, or introduce a new console
performance or accessibility CI gate.

**Perceived-wait (F-7):** a layout-stable **skeleton** appears at **400 ms**; a **"still loading…" + Cancel**
affordance appears at **2 s**. **Cancel** returns to the prior stable view — it never shows an error state and
never cancels a mutation (the console has none); it only cancels an in-flight read.

**Release-validation page-load budgets (targets; measured numbers `reference_pending`):**

| Flow class | Target |
|---|---|
| Primary diagnostic page load | `< 1.5 s p95` / `< 3 s p99` |
| Degraded / incident flows | `≤ 5 s p95` |
| Status / audit summary reads | `< 500 ms p95` |

**Accessibility (WCAG 2.2 AA):** single `<h1>` per page, `<html lang="en">`, `FocusOnNavigate` to the heading,
captioned tables with scoped headers, English-only for MVP. Automated structural conformance is verified by
`AccessibilityContractSweepTests`. Manual checks — keyboard journeys, screen-reader, 125/150/200 % zoom,
forced-colors, and color-blindness — are `reference_pending` with the method defined in the UX spec. No new
accessibility or performance CI gate is introduced by this story.

## Metadata-only policy

Every example in this document uses opaque synthetic identifiers and placeholder hosts only. The console,
its projections, and this documentation are output channels subject to the metadata-only invariant: no
secrets, bearer tokens, credential material, raw file contents, diffs, provider payloads, real issuer or
audience values, production URLs, environment dumps, stack traces, tenant data, or host-absolute paths.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-operations-audit-docs-gates.ps1
```

The gate runs the `OperationsAuditDocsConformanceTests` and writes a metadata-only report to
`_bmad-output/gates/operations-audit-docs/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`) when the
shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate falls back to the
xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm
`_bmad-output/gates/operations-audit-docs/latest.json` reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the routed-page inventory, disposition vocabulary, and
technical-state catalog stay synchronized with `DispositionLabelMapper`. Rerun the gate after any change to
the console routes, the disposition mapper, or the cross-linked UX sources. The static gate runs in the
`contract-spine` CI lane and through the baseline CI Contracts.Tests filter; it is not promoted to a new
top-level `ci.yml` lane, to `release-packages.yml`, or to scheduled workflows.
