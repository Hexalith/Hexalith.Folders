# Downstream Reconciliation Note — Architecture Update 2026-07-19

## Context

On 2026-07-19 `architecture.md` was brought into full alignment with the ratified 2026-07-15
planning authority (`sprint-change-proposal-2026-07-15.md`, amending the 2026-07-14 structural
correction and audit). All five `reconcile-architecture.md` gaps and the seven §3.2 architecture
items were applied: canonical lock identity, all-mutations idempotency + expired-key precedence,
state/disposition vocabulary (five lock states, five operator dispositions, six display
dimensions, `unknown_provider_outcome` = auto-reconciling), F-6 incident dual authorization,
Blazor Web App / `FrontComposerShell` hosting terminology, production read-model ownership
relocation (Story 11.10 → Epics 4/6/10), FR58/UI structure mapping, plus the Epic 12/13
charters, control-plane delivery-posture reframe, durable-state status, and the NOT READY
readiness supersession. Run memlog:
`_bmad-output/planning-artifacts/architecture/architecture-folders-2026-07-19/.memlog.md`.

## Already aligned (no action)

- **PRD** (`prd.md`) — reconciled to the 2026-07-15 authority on 2026-07-15 (its memlog records
  the July reconciliation event); the architecture changes were derived from the same authority,
  so no new PRD edits are implied.
- **sprint-status.yaml** — Epics 12/13 and Stories 4.18–4.21, 6.12–6.14, 10.7–10.9, 13.1–13.6
  were already registered by the ratified structural correction.

## Pending downstream edits implied by this update

### 1. `epics.md` (canonical epic file — STALE, last touched 2026-07-07)

The 2026-07-15 proposal itself records that approved changes were never incorporated here.
Architecture now cites structures epics.md lacks:

- Register **Epic 12** (durable data plane, Stories 12.1–12.5) and **Epic 13** (security/ops
  hardening, Stories 13.1–13.6).
- Add reopened closure stories **4.18–4.21**, **6.12–6.14**, **10.7–10.9**; remove Story 11.10
  as owner/convergence dependency of Epics 4/6/10 (Workstream 11 = platform seams 11.10/11.14/11.15 only).
- Carry the FR58 completion chain (10.7 bridge/registration → 10.8 real round trip = FR58
  completion → 10.9 C9-gated body content) and the Epic 12 dependency spine.

### 2. `ux-design-specification.md` (STALE, last touched 2026-07-07)

- Hosting terminology → **Blazor Web App, Interactive Server through `FrontComposerShell`** (UX-DR2/13/15 per Proposal 4.8).
- Six independent state/disposition display dimensions; five lock states; **five** operator
  dispositions incl. `available`; `unknown_provider_outcome` shown as automatic reconciliation
  in progress (budget/last-check/next-check), `awaiting-human` only at `reconciliation_required`.
- Normative components now pinned by architecture: Authorized Search & Results (safe scope
  establishment first), Access Evidence, Provider Readiness Evidence, Incident-Evidence
  Authorization Gate (UX-DR33 dual authorization), Metadata Folder Tree/Table.

### 3. Contract Spine + C13 oracle (`hexalith.folders.v1.yaml`, `parity-contract.yaml`)

- Extend `x-hexalith-idempotency-*` coverage to **every mutating operation** (folder create,
  configuration, binding, branch/ref policy, ACL, archive, prepare, lock acquire/release, file
  mutation, commit, cleanup); generator/gates must prove complete coverage (OQ8).
- Reads: canonical idempotency-key **rejection before query execution** row per read cell.
- Add stable **`idempotency_key_expired`** error (non-retryable with old key; clientAction
  `refresh_state_then_submit_with_new_key`) to the error inventory, CLI exit-code 76, MCP kind set.
- Lock-conflict semantics keyed on the canonical serializing identity (managed tenant +
  provider/repository identity + normalized ref) with alias-collision parity evidence (OQ7).
- Denominators (operation counts, C13 cells) always from the generated inventory — remove any
  hard-coded 47/49-style counts in consumer docs/tests (audit HXF-INFO-002/HXF-MCP-002).

### 4. Governance / traceability (handle in gate-lockstep — do NOT edit unilaterally)

- Architecture now records **NFR52 + release readiness as open** until production projections
  rebuild from durable events in deployed hosts with populated evidence. Any change to
  `docs/exit-criteria/nfr-traceability.md` rows must follow the decoupling precedent and the
  `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` hard-pin rule (change row + doc section +
  test pin together; never flip cited rows on a cascade).

### 5. Implementation artifacts

- Any story context generated before 2026-07-19 that cites the old lock scope
  (`tenant/folder/workspace`), the idempotency subset, the four-disposition list, "Blazor
  Server", or Story 11.10 product-projection ownership should be regenerated from the updated
  architecture before dev work starts.
