---
project: Folders
date: 2026-09-17
workflow: bmad-correct-course
mode: batch
status: pre-generation-approved-handoff-ready
scope: major
approval_required: true
supersedes: none
continues: sprint-change-proposal-2026-09-15.md
source_artifacts_modified: false
sprint_status_modified: false
general_execution_hold: true
execution_authorization: relock-only
next_workflow: bmad-create-epics-and-stories
---

# Sprint Change Proposal — Planning-Authority Relock Execution Packet

## 1. Issue Summary

The sponsor-approved September 15 correction and the September 17 architecture revision now decide the
Folders-owned mechanisms, story owners, strict execution ranks, and prerequisite edges. Sprint planning still
fails because those decisions are not yet one canonical delivery plan:

- Stories 1.17, 4.22, 12.7, and 13.7 are reserved by architecture but absent from `epics.md` and manifest v1;
- affected existing stories still carry stale or incomplete prerequisites and acceptance criteria;
- `planning-story-manifest.yaml` is the August 4 version-1 snapshot and cannot express the approved lifecycle,
  relock milestones, external releases, ranks, or three-dimensional decision/execution/evidence status;
- pre-generation A1, A2, A4, A5, A6, A7, and A7b attestations are recorded, while post-generation A2b, A3,
  A6b, and A8 still require exact output digests;
- EventStore event evolution and recovery are external prerequisites with no accepted platform release; and
- `sprint-status.yaml` remains intentionally stale while the general execution hold is active.

This packet converts those findings into exact input for `bmad-create-epics-and-stories`. It records Jerome's
explicit named-role approval of the seven pre-generation payloads. It does not claim a post-generation approval,
alter a lifecycle value, regenerate sprint tracking, accept either EventStore dependency, or remove the hold.

## 2. Governing Authority and Digests

| Artifact | SHA-256 | Use |
| --- | --- | --- |
| `implementation-readiness.md` | `1cd954f81ba2551e400c4bbddcc5df48a9961835f5da2dbbef076aca254db690` | Saved blocker inventory and FAIL decision |
| `sprint-change-proposal-2026-09-15.md` | `5d12ae4dd4e8f306b8dde5caf8f187d13e40c1fabc6d8e56c0757e24f2004104` | Sponsor-approved correction and Section 9 gate |
| `architecture.md` | `74ef242f6bfb77458818ab08a8acc25f547d6c891d29d36ab4954f7ca879973e` | Current mechanism authority |
| `reconcile-architecture-downstream-2026-09-17.md` | `788b79be90c9c17606af23d6c436ef67cc272757937bbc47e57d61f1aa63a8cd` | Exact story, rank, edge, and manifest handoff |
| `planning-story-manifest-v2-relock-input.yaml` | `3e869bcc1d543d71254ef0a3848976fea0ee62ed3c5be37583fec2f9e8862ebf` | Approved pre-generation machine-readable regeneration contract |
| `planning-authority-relock-approval-register.yaml` | `9360e4b87a91179a7f7357c58a2d38118b436724a8a5f440fa4e226b048d1b95` | Exact-digest approval records; seven pre-generation gates approved and four post-generation gates pending |

The September 15 sponsor approval only routes the work. In a separate September 17 approval exchange, Jerome
explicitly authorized recording himself as the named approver representing Product, Architecture, Security,
Operations, Test, Delivery, Contract/Delivery, and Legal for the seven pre-generation gates. The approval
register binds each represented authority separately to the applicable exact payload digest. It leaves A2b,
A3, A6b, and A8 pending because their required output digests do not yet exist.

## 3. Impact Analysis and Recommended Approach

The scope remains **Major**. Product authority, acceptance authority, dependency control, security contracts,
retention, lifecycle semantics, and external platform prerequisites all move together. The recommended path is
the already approved direct correction with an authority relock. The pre-generation A1, A2, A4, A5, A6, A7,
and A7b exact-payload decisions are now approved. The remaining path is:

1. run `bmad-create-epics-and-stories` against this packet and the version-2 manifest input;
2. bind A2b and A3 to the exact post-planning output digests and request those approvals;
3. run the authorized `1.17-GENERATE` candidate-generation milestone, bind A6b to matrix 2.0.0, and request its approval;
4. run `RELOCK-SECTION9` against all approved digests; and
5. request A8 only on a complete passing result.

Rollback remains rejected because completed evidence is valid. Numeric history remains stable. No future
capability is represented as complete merely because its story or requirement is admitted.

## 4. Exact Epic and Story Changes

### 4.1 Add Story 1.17 without renumbering history

**OLD:** Epic 1 ends at Story 1.16; PD10 has no canonical story owner.

**NEW — Story 1.17: Publish the PD10 v2 Authorization Contract Spine**

- Classification: technical enabler.
- Canonical execution rank: 10.
- Exact prerequisites: `DEC-A8-HOLD`, `1.17-GENERATE`, and `DEC-A6B-OQ3`.
- Relock milestone: `1.17-GENERATE` runs at rank 4 after `RELOCK-PLANNING` and `DEC-A6-PD10`, generates the
  final candidate, and neither exposes v2 nor closes Story 1.17.
- Acceptance: matrix `2.0.0` semantics and fourteen-state denominator; folder-scoped diagnostic/task routes;
  stale-authority 503; one safe 404; required visibility; protected-operation 403 and the three existence-leaking
  codes retired; `/api/v2` only in the supported profile; OpenAPI/server/client/CLI/MCP/UI regeneration; closed
  error-code/client-action/visibility/exit/failure-kind vocabularies; `read_model_unavailable` at CLI 73;
  `concurrency_conflict` at CLI 77/MCP `concurrency_conflict`; status-code and error-vocabulary fingerprints in
  `previous-spine.yaml`; C13 regeneration; consumer discovery; exact-digest A6b approval.
- Preserve v1 as historical evidence. If a deployed external v1 consumer is found, block and escalate rather
  than inventing a migration window.

### 4.2 Add Story 4.22 without renumbering history

**OLD:** Epic 4 ends at Story 4.21; PD11 has no canonical story owner.

**NEW — Story 4.22: Implement the PD11 Guard-Discriminated Lifecycle**

- Classification: product.
- Canonical execution rank: 11.
- Exact prerequisites: `DEC-A8-HOLD`, Story 1.17, `DEC-A7-C6`, and `DEC-A7B-C3`.
- Acceptance: `(state,event,guard)` implementation and gate; all five guarded pairs; explicit rejection for
  every omitted branch; `stagedByTaskId`, `stagedByPrincipal`, `stagedRecoveryStartedAt`,
  `stagedRecoveryDeadline`, `stagedCleanupStartedAt`, and `stagedCleanupNotBefore`; terminal/no-active plus P7D
  and legal-hold recheck; resume cancellation and a fresh terminal epoch; server-authorized task binding;
  `LockLeaseBecameStale`; automatic `unknown_provider_outcome`; reserved operator identifiers absent from the
  MVP enum/diagram and negatively tested; C3 cleanup predicates; mapping/diagram/code/UI disposition lockstep;
  v2 enum regeneration; A7 and A7b evidence.

### 4.3 Add Story 12.7 without renumbering history

**OLD:** Epic 12 ends at Story 12.6; PD8 has no canonical story owner.

**NEW — Story 12.7: Protect Confidential Operational Values**

- Classification: product.
- Canonical execution rank: 22.
- Exact prerequisites: `DEC-A8-HOLD`, Story 12.1, and `DEC-A5-C9`.
- Acceptance: one event-write tenant HMAC tokenizer across events, evidence, lock identity, Memories, and
  exports; immutable canonical token per binding/value generation; atomic active-key aliases during rotation;
  retirement only after live alias coverage and old lease/retry closure; held-lock/restart/replay/collision
  tests; no durable cleartext or reversible ciphertext in EventStore, secret stores, working copies, retries, or
  backups; provider-operation classification as `opaque-handle-restartable` or `cleartext-required`; pre-admission
  422 `confidential_operation_not_durable` for the latter; transient-memory and sentinel tests; distinct
  `withheld` rendering and corrected `redacted` copy; A5/C9 evidence.

### 4.4 Add Story 13.7 without renumbering history

**OLD:** Epic 13 ends at Story 13.6; the supported production/recovery profile has no canonical story owner.

**NEW — Story 13.7: Deliver the Supported Production Profile and Recovery Drill**

- Classification: security/operations hardening.
- Canonical execution rank: 40.
- Exact prerequisites: `DEC-A8-HOLD`, `EXT-ES-RECOVERY`, Stories 12.1–12.2, and Stories 13.1–13.6.
- Acceptance: production/preproduction Kubernetes manifests; stable app IDs; Dapr mTLS/default-deny and app-token
  callback-listener isolation; topology spread and disruption budgets; no singleton Dapr control plane,
  ingress, database endpoint/pooler, or durable broker; PostgreSQL `state.postgresql` v2; resource envelopes;
  fail-start configuration; cross-region WAL/PITR and 35-day recovery points; separately failed key custody;
  EventStore-owned signed recovery-safety export with KMS signer, monotonic watermark, idempotent replay,
  five-minute lag alert, active-hold retention, corruption/loss failure, and control-state recovery; isolated
  regional-loss restore; projection rebuild/reconciliation; first drill meeting RPO/RTO; ADR/runbook inventory
  extended for `backup-restore.md`, ADR 0007 I-10/I-11, required sections, and metadata-only negative controls.
- Record `OQ12-EVIDENCE` and `OQ13-EVIDENCE` as separate approvals with their distinct role sets.

### 4.5 Amend existing stories exactly

| Story | Required replacement or addition |
| --- | --- |
| 12.1 | Add `EXT-ES-EVENT-EVOLUTION`; D-11 sole-writer/transactional append; one bounded full re-evaluation and never blind append/effect retry; 409/CLI 77/MCP `concurrency_conflict`; stable URI logical types; positive payload versions distinct from `MetadataVersion`; multi-replica conflict evidence; exact closed legacy `EventTypeName` alias inventory with one historical byte fixture per alias; no unregistered missing-version alias defaults to v1. |
| 12.2 | Add `EXT-ES-EVENT-EVOLUTION`; empty-checkpoint replay across every retained upcaster chain and the closed missing-version registry; current-semantic rebuild without historical-byte rewrite. |
| 12.3 | Confidential operational values are tokens or non-reversible opaque provider handles only; forbid direct Dapr/database domain writes, sealed reversible values, and durable working-copy cleartext. |
| 12.4 | Add Story 12.7; use the immutable canonical token for writer identity; execute confidential targets only through capability-proven opaque provider handles and fail before admission otherwise. |
| 12.5 | Add Story 12.7 tokenizer use for Memories egress; cleartext confidential values never enter outbox, broker, bridge, or retry evidence. |
| 12.6 | Use accepted-terminal `OQ8-DESIGN`, not `OQ8-EVIDENCE`, as prerequisite; rank 21. OQ8 evidence follows Story 12.6. |
| 13.1 | Apply one S-9 policy to Octokit and Forgejo readiness and later calls: normalized all-address checks, connect pin, original-host TLS, `UseProxy=false`, credential redirect rejection, approved private exceptions, and credential sentinels. |
| 13.2 | Install fallback authorization; expose only liveness; isolate Dapr callbacks on loopback. Bind the same per-workload Kubernetes secret to `dapr.io/app-token-secret` and application `APP_API_TOKEN`; constant-time compare `dapr-api-token`; prove public/direct-pod denial and absent/wrong/missing/correct secret behavior plus mTLS/app/topic controls. |
| 13.5 | Add `EXT-ES-RECOVERY`; use HA PostgreSQL `state.postgresql` v2 plus separate durable replicated pub/sub and governed resiliency; no Redis authoritative state. |
| 13.6 | Own I-8 tenant/global provider token buckets and the bounded 429 chaos test in addition to existing limits/filter scope. |
| 4.18 | Add Story 4.22; rank 40. |
| 4.19 | Add Stories 4.18, 4.22, 12.6 and `DEC-A7-C6`; `OQ7-EVIDENCE` follows the story; rank 41. |
| 4.20 | Add Stories 4.18, 12.6, 12.7, 1.17 and `DEC-A6B-OQ3`; rank 41. |
| 4.21 | Add Story 4.22 while retaining 12.4/4.19/4.20; rank 42. |
| 5.8, 5.10 | Follow Story 4.19; rank 42. |
| 5.9, 5.11 | Follow Stories 4.20, 4.21, and 12.6; rank 43. |
| 6.12 | Follow 12.1, 12.2, and 4.22; rank 40. |
| 6.13 | Follow 3.14, 12.4, and 12.5; rank 40. |
| 6.14 | Follow 6.12, 6.13, 4.18, and 4.21; it produces and does not depend on `OQ9-EVIDENCE`; rank 43. |
| 10.8 | Follow accepted-terminal 10.7, 12.1–12.3, 12.5, 4.20, and 11.15; rank 42. |
| 3.14 | Follow accepted-terminal 3.10/3.12 plus 12.1, 12.2, and 12.4; rank 31. |
| 11.12 | Follow Story 1.17 so modernization targets v2; retain accepted 11.2; rank 12. |
| 11.13 | Replace “applicable adoption stories” with exact prerequisites 11.8–11.12 and 11.14; rank 13. |
| 11.20 | Retain 11.9 and 11.13; rank 14. |
| 11.21 | Retain 11.13–11.20, including rank-30 Story 11.15; rank 31. |

Also replace `oasdiff` at `AR-PROVIDER-04` with `tests/tools/run-nightly-drift-gates.ps1` driving
`tests/tools/forgejo-drift/`, and keep request-side retry transport metadata distinct from D-9 response header
`X-Hexalith-Retry-Transport`.

### 4.6 Other canonical artifact corrections

- In `prd.md`, replace the stale statement that the strict rank model remains open/unsatisfiable with the
  validated strict ranks and exact edges from `planning-story-manifest-v2-relock-input.yaml`. Preserve the
  decision history that explains why the earlier wave table was superseded.
- In `epics.md`, mirror exact FR1–FR58 and NFR1–NFR84 identities; update traceability to 84 without treating
  admission as coverage; apply A4’s Story 10.9 title and negative-guard scope; preserve the Story 10.7 done-bar.
- In `ux-design-specification.md`, retain the current `withheld`, `redacted`, `unavailable`, `absent`, automatic
  unknown-outcome recovery, and non-disclosing error behavior; update only provenance to the final approved
  digests if the content remains unchanged.
- `architecture.md` is current mechanism authority. Do not rewrite it unless a human approver changes a
  decision payload.

## 5. Complete Version-2 Manifest Contract

`planning-story-manifest-v2-relock-input.yaml` is the machine-readable source for the next workflow. It defines:

- manifest version 2, full-file regeneration, counts, lifecycle semantics, classification totals, and
  provenance;
- all ranks 0–60 and every exact prerequisite edge, including the expanded `DEC-A8-HOLD` edge on every ordinary
  story and delivery-evidence node at rank 10 or later;
- `RELOCK-PLANNING`, `1.17-GENERATE`, `RELOCK-SECTION9`, `DEC-A2B`, `DEC-A3`, `DEC-A6B-OQ3`, and `DEC-A8-HOLD`;
- accepted-terminal evidence references and their required paths;
- the OQ8 design/evidence split and OQ3 superseded-pending-reapproval status;
- the two external EventStore nodes, their owners, release identifiers, evidence paths, and pending state;
- separate decision, execution-authorization, and delivery-evidence dimensions;
- relock-only allow-lists and prohibitions; and
- 25 fail-closed validation rules for uniqueness, inventory counts, DAG shape, strict ordering, terminal
  exemptions, external acceptance, approval state, generated-surface parity, and the sprint-status prohibition.

The next workflow regenerates the whole manifest. It must not patch only disputed rows or copy stale counters.

## 6. External EventStore Ownership and Release Boundaries

### EXT-ES-EVENT-EVOLUTION

- Repository owner: Hexalith.EventStore platform maintainers.
- Accountable role: EventStore Platform Architect.
- Delivery role: EventStore Epic 6 owner.
- Source work: EventStore Stories 6.5 and 6.6.
- Stable release identifier: `hexalith-eventstore-event-evolution-v1`.
- Specification evidence: `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md`.
- Acceptance record: `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml`.
- Status: **pending**. At observed commit `b5541259058320a0a7f1db19038709cbd02dad85`, both stories are backlog,
  the specification is absent, and EventStore documents that no formal upcaster pipeline exists.

### EXT-ES-RECOVERY

- Repository owner: Hexalith.EventStore platform maintainers.
- Accountable role: EventStore Platform Operations Lead.
- Delivery role: EventStore recovery release owner.
- Escalation reference: `EXT-ES-RECOVERY/2026-09-17`.
- Stable release identifier: `hexalith-eventstore-production-recovery-v1`.
- Platform profile evidence: `references/Hexalith.EventStore/deploy/dapr/statestore-postgresql.yaml`.
- Restored-backup contract evidence:
  `references/Hexalith.EventStore/docs/guides/payload-protection-and-crypto-shredding.md`.
- Acceptance record: `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-recovery-v1.yaml`.
- Status: **pending**. The observed EventStore authority deliberately selects PostgreSQL component v1, while
  Folders requires v2, and backup creation, validation, and restore remain deferred.

The stable release identifiers are planning identities, not claims that a semantic version or immutable commit
has shipped. Manifest validation must reject either node as accepted until its acceptance record carries the
immutable released version or commit, exact evidence digests, named approvers, and approval dates.

## 7. Exact-Digest Approval Records

`planning-authority-relock-approval-register.yaml` contains eleven fail-closed records: A1, A2, A2b, A3, A4,
A5, A6, A6b, A7, A7b, and A8. Each references a separate immutable candidate payload under
`authority-relock/2026-09-17/` and records its SHA-256 digest. A1, A2, A4, A5, A6, A7, and A7b contain 26
separate exact-role signatures by Jerome dated September 17, one for each required authority.

A2b and A3 remain empty and cannot be approved until `RELOCK-PLANNING` produces the final output digests. A6b
remains empty and cannot be approved until `1.17-GENERATE` produces authorization matrix 2.0.0 and the
generated-conformance candidate set. A8 remains empty and cannot be approved until `RELOCK-SECTION9` passes
and sprint planning later produces the tracker digest.

No approval is inferred for a post-generation payload from the pre-generation attestation, a role name, a
passing test, or a generated artifact.

## 8. Lifecycle and Evidence Preservation

- Keep completed evidence and immutable workflow snapshots byte-stable.
- Preserve 3.10–3.13, 10.7, 11.2, and 11.3 as accepted terminal evidence.
- Preserve Story 10.8’s completed increments while its canonical lifecycle remains `in-progress`.
- Reconcile Story 10.9 to `done` only after A4 and only under the exact metadata-only safety title/scope.
- Keep Story 11.4 in `review` until a separate explicit review acceptance.
- Add the four new story rows as `backlog`.
- Do not alter `sprint-status.yaml` in `bmad-create-epics-and-stories`; the later deterministic sprint-planning
  run owns tracker reconciliation and its journal.

## 9. Execution Hold and Freeze-Removal Gate

`general_execution_hold` remains `true`. Only `RELOCK-PLANNING`, `1.17-GENERATE`, and `RELOCK-SECTION9` receive
`relock-only` authorization for their exact allow-listed artifacts. Every ordinary nonterminal story and every
delivery-evidence node remains held and consumes `DEC-A8-HOLD` as an explicit prerequisite.

The relock lane cannot expose v2, publish a release, close a story lifecycle, or produce OQ5–OQ13 runtime
evidence. A8 remains post-conformance. If any September 15 Section 9 check fails, the hold and planning-recovery
action remain active.

## 10. Approval Status and Human Decisions Still Required

| Gate | Required authorities | Current readiness |
| --- | --- | --- |
| A1 / PD1 / OQ11 | Product, Architecture, Security, Test | Approved by Jerome on 2026-09-17; four exact-role signatures recorded. |
| A2 / PD3 | Product, Architecture, Security, Operations, Test | Approved by Jerome on 2026-09-17; five exact-role signatures recorded. |
| A2b / PD6 | Product, Architecture, Security, Operations, Test | Wait for exact post-planning PRD/epics/traceability digests. |
| A3 / PD4 | Product, Architecture, Delivery | Wait for exact post-planning lifecycle/manifest digests. |
| A4 / PD5 | Product, Security, Architecture | Approved by Jerome on 2026-09-17; three exact-role signatures recorded. |
| A5 / C9 / PD8 | Product, Architecture, Security | Approved by Jerome on 2026-09-17; three exact-role signatures recorded. |
| A6 / PD10 | Product, Architecture, Security, Contract/Delivery | Approved by Jerome on 2026-09-17; four exact-role signatures recorded. |
| A6b / OQ3 / PD10 | Product, Architecture, Security | Wait for matrix 2.0.0 and generated-set digests. |
| A7 / C6 / PD11 | Product, Architecture, Security | Approved by Jerome on 2026-09-17; three exact-role signatures recorded. |
| A7b / C3 | Legal, Product, Security, Architecture | Approved by Jerome on 2026-09-17; four exact-role signatures recorded. |
| A8 / freeze | Product, Architecture, Delivery | Wait for a passing Section 9 result and all final digests. |

The unresolved human decisions are A2b, A3, A6b, and A8, each at its stated post-generation gate. Jerome's
current approval is intentionally not carried forward to those future digest-bound records.

OQ12-EVIDENCE and OQ13-EVIDENCE remain later delivery/release approvals after Story 13.7; they are not A2
admission approvals and are not inputs to this planning relock.

## 11. Implementation Handoff

Classification: **Major — Product Manager and Solution Architect coordination with Security, Legal, Operations,
Test, Contract/Delivery, and Delivery.**

The seven pre-generation decision payloads now have the required named-role attestations, so the next workflow
may run. Its scope is PRD/epics/traceability correction plus version-2 manifest regeneration. It must stop before
sprint-status regeneration, ordinary story execution, v2 exposure, or hold removal.

Exact next command after the required pre-generation attestations are recorded:

```text
$bmad-create-epics-and-stories
```

Use this proposal and `planning-story-manifest-v2-relock-input.yaml` as governing correction inputs; do not
regenerate `_bmad-output/implementation-artifacts/sprint-status.yaml`.

## Change Navigation Checklist Record

- [x] 1.1–1.3 Trigger and evidence are the September 17 readiness FAIL plus the approved September 15 relock.
- [x] 2.1–2.5 Epic impact is bounded to stable IDs, four admissions, affected-story amendments, and strict ranks.
- [x] 3.1 PRD conflicts and the stale schedule statement are identified.
- [x] 3.2 Architecture is current; external EventStore gaps are assigned without being declared accepted.
- [x] 3.3 UX-visible changes are already defined; only final provenance may change if content remains stable.
- [x] 3.4 Manifest, approvals, traceability, generated contracts, governance, and lifecycle evidence are covered.
- [x] 4.1 Direct adjustment with an authority relock remains viable; effort and risk remain major.
- [x] 4.2 Rollback remains rejected to preserve truthful completed evidence.
- [x] 4.3 MVP remains durable and metadata-only; no further scope reduction is authorized.
- [x] 4.4 Exact sequencing, external nodes, relock lane, and hold controls are defined.
- [x] 5.1–5.5 The execution packet, machine-readable manifest input, approval register, and handoff are complete.
- [x] 6.1–6.2 The packet is internally checkable and names every remaining human/output dependency.
- [x] 6.3 All seven pre-generation named-role approvals are recorded against exact payload digests; the four
  post-generation gates remain pending without inferred approval.
- [N/A] 6.4 Sprint status regeneration is explicitly prohibited in this workflow.
- [x] 6.5 Handoff scope and exact next command are recorded.
