# Architecture-to-Delivery Reconciliation — 2026-09-17

Status: architecture decisions resolved; Delivery changes and named approvals pending.

Governing authority: the sponsor-approved
`sprint-change-proposal-2026-09-15.md`. This reconciliation supersedes the routed-open-item list in
`reconcile-architecture-downstream-2026-09-16.md` wherever the 2026-09-17 architecture now decides an item.
It changes no production code and no lifecycle status.

## 1. Resolved architecture blockers

| Blocker | Resolution | Remaining gate |
| --- | --- | --- |
| Stale authority contradiction | Stale, unavailable, conflicting, or incomplete authority evidence → `authority-unavailable-503`; fresh negative facts → `safe-denial-404`; bounded stale is allowed only for non-authority read models | A6b matrix/v2 conformance approval |
| Guarded lifecycle and staged retention | Five guard-discriminated pairs; staging identity; non-destructive recovery clock; separate cleanup epoch starting only at terminal/no-active with a full P7D window; legal-hold/current-state recheck | A7 and A7b |
| PD8 operational boundary | Immutable canonical HMAC token plus rotation aliases; no durable cleartext or reversible ciphertext; only opaque provider handles may survive; cleartext-required operations fail before admission | A5/C9; authority amendment + Legal decision if reversible recovery is demanded |
| Aggregate concurrency | EventStore `AggregateActor` sole writer; transactional actor-state append/advance; 409/CLI 77/MCP `concurrency_conflict`; at most one full re-evaluation, never blind append/effect retry | Story 12.1 evidence |
| Event evolution | Stable URI logical type + envelope `eventPayloadSchemaVersion`; deterministic EventStore upcasters; closed legacy-unversioned registry; immutable historical bytes | `EXT-ES-EVENT-EVOLUTION`, Stories 12.1–12.2 |
| Provider endpoint/SSRF | HTTPS, normalized addresses, all-result validation, connect pin, original-host TLS, no ambient proxy, cross-authority credential redirects rejected, approved private exceptions only | Story 13.1 + OQ12 |
| HTTP protection | Fallback authorization; only liveness public; Dapr callbacks on loopback app listener with app API token, no Service/Ingress exposure, mTLS/access/topic controls | Story 13.2 + OQ12 |
| PD10 migration | Corrected contract ships only at `/api/v2`; v1 remains historical/not production-routed; deployed external-v1 consumer discovery can reopen a migration-window decision | Story 1.17 + A6b |
| Deployment/recovery | Single-region serving Kubernetes; PostgreSQL `state.postgresql` v2; no singleton substrate; cross-region WAL/restore points/key custody/WORM recovery-safety export; regional-loss RPO ≤5m/RTO ≤4h drill | `EXT-ES-RECOVERY`, Stories 13.5/13.7, OQ12/OQ13 |
| Execution ordering | Strict ranks plus exact edge table; OQ8 design/evidence split; Workstream 11 cleanup moved after adoptions; explicit relock-only lane prevents hold deadlock | Manifest regeneration/validation |

## 2. Exact `epics.md` changes

Delivery must add these story definitions without renumbering an existing story and without treating admission as
implementation evidence.

### Story 1.17 — Publish the PD10 v2 authorization Contract Spine

Classification: technical enabler. Canonical story rank: 10.

The relock-only milestone `1.17-GENERATE` runs at rank 4 after A6 and `RELOCK-PLANNING` and produces the final
v2 candidate without closing the story or exposing routes. `DEC-A6B-OQ3` runs at rank 5 over that output;
`RELOCK-SECTION9` and A8 follow at ranks 6 and 7. The canonical Story 1.17 node closes/enters ordinary
consumption at rank 10 only after the generation milestone, accepted A6b, and accepted A8.

Acceptance must require: candidate matrix `2.0.0` semantics and 14-state denominator; explicit folder-scoped diagnostic/task routes;
stale-authority 503; one safe 404; required visibility; retirement of protected-operation 403 and the three
existence-leaking codes; `/api/v2` only in the supported profile; OpenAPI/server/client/CLI/MCP/UI regeneration;
closed error-code/client-action/visibility/exit/failure-kind vocabularies, including `read_model_unavailable`
at CLI 73 and `concurrency_conflict` at CLI 77; status-code and error-vocabulary
fingerprints in `previous-spine.yaml`; C13 regeneration; consumer discovery; and A6b approval. It must preserve
v1 as historical evidence and block, rather than invent a window, if a deployed external v1 consumer is found.

### Story 4.22 — Implement the PD11 guard-discriminated lifecycle

Classification: product. Rank: 11. Prerequisite: Story 1.17 for the v2 published enum/error surface.

Acceptance must require: `(state,event,guard)` implementation and gate; all five guarded pairs; explicit
rejection for every omitted branch; `stagedByTaskId`, `stagedByPrincipal`, `stagedRecoveryStartedAt`, and
`stagedRecoveryDeadline`; cleanup-workflow `stagedCleanupStartedAt`/`stagedCleanupNotBefore`; terminal/no-active
+ P7D and legal-hold recheck; resume cancellation/fresh terminal epoch; server-authorized task binding;
`LockLeaseBecameStale`; automatic `unknown_provider_outcome`; reserved operator identifiers absent from the MVP
enum/diagram and negatively tested; C3 cleanup predicates;
mapping/diagram/code/UI disposition lockstep; v2 enum regeneration; A7 and A7b evidence.

### Story 12.7 — Protect confidential operational values

Classification: product. Rank: 22. Prerequisite: Story 12.1.

Acceptance must require: one event-write tenant HMAC tokenizer for events, evidence, lock identity, Memories,
and exports; immutable canonical token per binding/value generation; active-key aliases resolving atomically to
that token during rotation; retirement only after live-value alias coverage and old lease/retry closure;
held-lock/restart/replay/collision tests; no durable cleartext or reversible ciphertext in EventStore, secret
stores, working copies, retries, or backups; provider-operation classification as opaque-handle-restartable or
cleartext-required; pre-admission 422 `confidential_operation_not_durable` for the latter; transient-memory and
sentinel tests; distinct `withheld` rendering and corrected `redacted` copy; and A5/C9 evidence.

### Story 13.7 — Deliver the supported production profile and recovery drill

Classification: security/operations hardening. Rank: 40. Prerequisites: Stories 13.1–13.6, 12.1–12.2, and `EXT-ES-RECOVERY`.

Acceptance must require: production/preproduction Kubernetes manifests; stable app IDs; Dapr mTLS/default-deny
and app-token callback listener isolation; topology-spread/PDB plus no singleton Dapr control-plane, ingress,
database endpoint/pooler, or durable broker; PostgreSQL `state.postgresql` v2; resource envelopes; fail-start
configuration; cross-region WAL/PITR and 35-day points; separately failed key custody; EventStore-owned signed
recovery-safety export with KMS signer, monotonic watermark, idempotent replay, ≤5-minute lag alert, active-hold
retention, corruption/loss failure, and control-state recovery; isolated regional-loss restore; projection
rebuild/reconciliation; first drill meeting RPO/RTO; and extension of the ADR/runbook gate inventory to include
`backup-restore.md`, ADR 0007 decision IDs I-10/I-11, its required sections, and metadata-only negative controls.
Record `OQ12-EVIDENCE` approval by Product + Architecture +
Security + Operations + Test and `OQ13-EVIDENCE` by Product + Architecture + Operations + Test separately.

### Amend existing stories

| Story | Required amendment |
| --- | --- |
| 12.1 | Depend on `EXT-ES-EVENT-EVOLUTION`; add D-11 sole-writer/transactional append, one bounded full re-evaluation (never blind append/effect retry), 409/CLI 77/MCP `concurrency_conflict`, stable URI logical types, positive envelope payload versions distinct from `MetadataVersion`, and multi-replica conflict evidence. Register the exact closed inventory of legacy `EventTypeName` aliases with one retained historical byte fixture per alias; no unregistered alias may default to v1. |
| 12.2 | Depend on `EXT-ES-EVENT-EVOLUTION`; add empty-checkpoint replay across every retained upcaster chain, including the closed legacy-missing-version registry, and prove current-semantic rebuild without rewriting bytes. |
| 12.3 | State that confidential operational values are tokens or non-reversible provider opaque handles only; no direct Dapr/database domain write, sealed reversible value, or durable working-copy cleartext is permitted. |
| 12.4 | Require Story 12.7; use the immutable canonical token for writer identity; execute confidential targets only through a capability-proven opaque provider handle and fail before admission otherwise. |
| 12.5 | Require the Story 12.7 tokenizer for Memories egress; plaintext confidential values never enter outbox, broker, bridge, or retry evidence. |
| 13.1 | Apply S-9 through one policy used by Octokit and Forgejo for readiness and later calls: all-address/normalized-IP checks, connect pin, original-host TLS, `UseProxy=false`, cross-authority credential redirect rejection, private-exception approval, and credential-sentinel tests. |
| 13.2 | Install framework fallback authorization; expose only liveness publicly; isolate Dapr callbacks on a loopback listener. Each callback workload must reference the same per-workload Kubernetes secret through `dapr.io/app-token-secret` for the sidecar and application-container `env.valueFrom.secretKeyRef`/`APP_API_TOKEN` for the validator; constant-time compare the incoming `dapr-api-token` header and never log either value. Prove public/direct-pod denial, absent application secret, wrong/missing/correct header behavior, and mTLS/app/topic controls. |
| 13.5 | Depend on `EXT-ES-RECOVERY`; replace generic state with HA PostgreSQL `state.postgresql` v2 plus separate durable/replicated pub/sub and governed resiliency; no Redis authoritative state. |
| 13.6 | Explicitly own I-8 tenant/global provider token buckets and the bounded 429 chaos test in addition to its existing limits/filter scope. |
| 4.18 | Add Story 4.22 prerequisite. Rank 40. |
| 4.19 | Add Stories 4.18, 4.22, and 12.6 plus accepted `DEC-A7-C6`; `OQ7-EVIDENCE` follows the story. Rank 41. |
| 4.20 | Add Stories 4.18, 12.6, 12.7, and 1.17 plus accepted `DEC-A6B-OQ3`. Rank 41. |
| 4.21 | Add Story 4.22; retain 12.4/4.19/4.20. Rank 42. |
| 5.8, 5.10 | Follow Story 4.19. Rank 42. |
| 5.9, 5.11 | Follow Stories 4.20, 4.21, and 12.6. Rank 43. |
| 6.12 | Follow 12.1, 12.2, and 4.22. Rank 40. |
| 6.13 | Follow 3.14, 12.4, and 12.5. Rank 40. |
| 6.14 | Follow 6.12, 6.13, 4.18, and 4.21; it produces `OQ9-EVIDENCE`, which is not a prerequisite. Rank 43. |
| 10.8 | Follow 12.1–12.3, 12.5, 4.20, 11.15, and accepted-terminal 10.7. Rank 42. |
| 3.14 | Follow 12.4 plus accepted-terminal 3.10/3.12. Rank 31. |
| 11.12 | Follow Story 1.17 so client modernization uses v2 rather than regenerating v1. Rank 12. |
| 11.13 | Replace “applicable adoption stories” with exact prerequisites 11.8–11.12 and 11.14. Rank 13. |
| 11.20 | Retain 11.9 and 11.13. Rank 14. |
| 11.21 | Retain 11.13–11.20 dependencies, including rank-30 Story 11.15. Rank 31. |
| 12.6 | Depend on accepted-terminal `OQ8-DESIGN`, not rank-50 `OQ8-EVIDENCE`; the latter follows 12.6 and gates release evidence, not story closure. Rank 21. |

Also make the two already-routed one-line corrections: replace `oasdiff` at `AR-PROVIDER-04` with
`tests/tools/run-nightly-drift-gates.ps1` driving `tests/tools/forgejo-drift/`; keep request-side retry transport
metadata distinct from D-9's response header `X-Hexalith-Retry-Transport`.

## 3. Exact execution ranks

The regenerated manifest must use these ranks for every nonterminal story after the September 15 lifecycle
reconciliation. Accepted-terminal prerequisites are rankless and carry their evidence reference.

| Rank | Story IDs |
| ---: | --- |
| 0 | Pre-generation decision nodes `DEC-A1`, `DEC-A2`, `DEC-A4`, `DEC-A5-C9`, `DEC-A6-PD10`, `DEC-A7-C6`, `DEC-A7B-C3`; accepted design records OQ1/OQ2/OQ4/OQ8-DESIGN |
| 1 | `EXT-ES-EVENT-EVOLUTION` |
| 2 | `EXT-ES-RECOVERY` |
| 3 | `RELOCK-PLANNING` |
| 4 | `DEC-A2B`; `DEC-A3`; `1.17-GENERATE` |
| 5 | `DEC-A6B-OQ3` |
| 6 | `RELOCK-SECTION9` |
| 7 | `DEC-A8-HOLD` |
| 10 | 1.17 |
| 11 | 4.22; 11.4–11.11; 11.14; 11.16; 13.1–13.3; 13.6 |
| 12 | 11.12; 11.17–11.19 |
| 13 | 11.13 |
| 14 | 11.20 |
| 20 | 12.1 |
| 21 | 12.2; 12.3; 12.6 |
| 22 | 12.7 |
| 30 | 11.15; 12.4; 12.5; 13.4; 13.5 |
| 31 | 3.14; 11.21 |
| 40 | 4.18; 6.12; 6.13; 13.7 |
| 41 | 4.19; 4.20 |
| 42 | 4.21; 5.8; 5.10; 10.8 |
| 43 | 5.9; 5.11; 6.14 |
| 50 | `OQ8-EVIDENCE`; `OQ9-EVIDENCE`; `OQ11-EVIDENCE`–`OQ13-EVIDENCE` |
| 51 | `OQ5-EVIDENCE`; `OQ6-EVIDENCE`; `OQ7-EVIDENCE` |
| 60 | `OQ10-EVIDENCE` release calibration and readiness rerun |

Validation rejects a cycle, missing rank on a scheduled nonterminal node, equal/later-rank unresolved
prerequisite, unranked nonterminal prerequisite, or terminal exemption without accepted evidence. Separate
`decision_status` from `execution_authorization_status` and `delivery_evidence_status`, so an approved design never masquerades as permission or completed evidence.
Every ordinary story and delivery-evidence node at rank 10 or later has the exact prerequisite
`DEC-A8-HOLD`; this universal edge is expanded and validated in the manifest rather than left implicit.

### Exact prerequisite edges

`accepted:` denotes an accepted-terminal, rankless row that must carry an evidence reference. A decision node
uses the rank shown above even while pending and must be accepted before its dependent may start. Every row at
rank 10 or later also has the universal `DEC-A8-HOLD` prerequisite in addition to its row-specific edges below.
These are the complete edges after expanding that universal rule;
Delivery must not derive extra prerequisites from phrases such as “applicable adoption stories” or “owning flows.”

| Node(s) | Exact prerequisites |
| --- | --- |
| `EXT-ES-EVENT-EVOLUTION`, `EXT-ES-RECOVERY` | none; external owner/version/evidence required before acceptance |
| `RELOCK-PLANNING` | `DEC-A1`, `DEC-A2`, `DEC-A4`, `DEC-A5-C9`, `DEC-A6-PD10`, `DEC-A7-C6`, `DEC-A7B-C3` |
| `DEC-A2B`, `DEC-A3` | `RELOCK-PLANNING` |
| `1.17-GENERATE` | `RELOCK-PLANNING`, `DEC-A6-PD10` |
| `DEC-A6B-OQ3` | `1.17-GENERATE` |
| `RELOCK-SECTION9` | `RELOCK-PLANNING`, `DEC-A2B`, `DEC-A3`, `1.17-GENERATE`, `DEC-A6B-OQ3` |
| `DEC-A8-HOLD` | `RELOCK-SECTION9` |
| every node at rank 10 or later | `DEC-A8-HOLD`, plus the row-specific prerequisites below |
| 1.17 | `1.17-GENERATE`, `DEC-A6B-OQ3` |
| 4.22 | 1.17, `DEC-A7-C6`, `DEC-A7B-C3` |
| 11.4, 11.6 | 1.17 |
| 11.5, 11.8–11.11, 11.14 | `accepted:11.2` |
| 11.7, 13.3 | none beyond the universal A8 edge |
| 11.16 | `accepted:11.3` |
| 13.1, 13.2, 13.6 | 1.17 |
| 11.12 | 1.17, `accepted:11.2` |
| 11.17, 11.18 | 11.7 |
| 11.19 | 11.11 |
| 11.13 | 11.8, 11.9, 11.10, 11.11, 11.12, 11.14 |
| 11.20 | 11.9, 11.13 |
| 12.1 | `EXT-ES-EVENT-EVOLUTION`, `accepted:OQ1`, `accepted:OQ2`, `DEC-A6B-OQ3`, `accepted:OQ4` |
| 12.2 | 12.1, `EXT-ES-EVENT-EVOLUTION` |
| 12.3 | 12.1 |
| 12.6 | 12.1, `accepted:OQ8-DESIGN` |
| 12.7 | 12.1, `DEC-A5-C9` |
| 11.15 | `accepted:11.2` |
| 12.4 | `accepted:3.11`, `accepted:3.13`, 12.1, 12.2, 12.3, 12.7 |
| 12.5 | `accepted:10.6`, 12.1, 12.2, 12.3, 12.7 |
| 13.4 | 12.1, 12.2, 13.2 |
| 13.5 | `EXT-ES-RECOVERY`, 12.1, 12.2 |
| 3.14 | `accepted:3.10`, `accepted:3.12`, 12.1, 12.2, 12.4 |
| 11.21 | 11.13, 11.14, 11.15, 11.16, 11.17, 11.18, 11.19, 11.20 |
| 4.18 | 4.22, 12.1, 12.2 |
| 6.12 | 4.22, 12.1, 12.2 |
| 6.13 | 3.14, 12.4, 12.5 |
| 13.7 | `EXT-ES-RECOVERY`, 12.1, 12.2, 13.1, 13.2, 13.3, 13.4, 13.5, 13.6 |
| 4.19 | `DEC-A7-C6`, 4.18, 4.22, 12.1, 12.2, 12.3, 12.6 |
| 4.20 | `accepted:OQ2`, `DEC-A6B-OQ3`, 1.17, 4.18, 12.1, 12.2, 12.3, 12.6, 12.7 |
| 4.21 | 4.18, 4.19, 4.20, 4.22, 12.4 |
| 5.8, 5.10 | 4.19 |
| 10.8 | `accepted:10.7`, 4.20, 11.15, 12.1, 12.2, 12.3, 12.5 |
| 5.9, 5.11 | 4.20, 4.21, 12.6 |
| 6.14 | 4.18, 4.21, 6.12, 6.13 |
| `OQ5-EVIDENCE` | 10.8, `OQ11-EVIDENCE` |
| `OQ6-EVIDENCE` | 4.18, 6.12, 6.13, 6.14, `OQ11-EVIDENCE` |
| `OQ7-EVIDENCE` | 4.19, 4.22, `OQ11-EVIDENCE` |
| `OQ8-EVIDENCE` | 12.6 |
| `OQ9-EVIDENCE` | 6.14 |
| `OQ11-EVIDENCE` | 3.14, 4.18, 4.19, 4.20, 4.21, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7 |
| `OQ12-EVIDENCE` | 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7 |
| `OQ13-EVIDENCE` | 13.7, `accepted:C1`, `accepted:C4`, `accepted:C5` |
| `OQ10-EVIDENCE` | `OQ5-EVIDENCE`, `OQ6-EVIDENCE`, `OQ7-EVIDENCE`, `OQ8-EVIDENCE`, `OQ9-EVIDENCE`, `OQ11-EVIDENCE`, `OQ12-EVIDENCE`, `OQ13-EVIDENCE` |

## 4. Exact manifest regeneration

Regenerate the whole file; do not patch only disputed rows.

1. Set `manifest_version: 2`, add a unique revision for this authority relock, and use
   `generated_on: '2026-09-17'`. Record the September 15 proposal, its digest, this architecture revision, this
   reconciliation, and OQ1–OQ4 evidence in provenance; identify the August 4 file as superseded snapshot.
2. Use `story_lifecycle_status` as the only current lifecycle field. Historical specification `status` is
   `workflow_snapshot_status`. Apply the proposal's Section 6/sprint reconciliation, including 3.10–3.13 and
   10.7 as accepted terminal evidence, 10.8 `in-progress`, 10.9 `done` only under its narrowed title, 11.2/11.3
   `done`, and 11.4 `review`. Do not rewrite frozen specifications.
3. Add the four story rows above as `backlog`, producing 158 canonical story identities and 159 inventory rows
   including alias 2.8b. Expected classifications become product 86, technical-enabler 41,
   release-governance 24, and security-operations-hardening 7. Recompute lifecycle counts from authoritative
   rows; do not carry the stale counters or the duplicate 3.2 row.
4. Update the requirement inventory to exact NFR1–NFR84 and retain exact FR1–FR58. Add the four stories to
   requirement/completion ownership without marking any requirement covered merely by admission.
5. Add typed control/milestone records for `RELOCK-PLANNING`, `1.17-GENERATE`, `RELOCK-SECTION9`, the
   post-planning `DEC-A2B`/`DEC-A3`, post-generation `DEC-A6B-OQ3`, and post-conformance `DEC-A8-HOLD`. Add `execution_waves`, each row's
   `execution_rank`, the universal A8 edge on every rank-10-or-later node, and exactly the remaining prerequisite
   edges above. Add external
   node type records for `EXT-ES-EVENT-EVOLUTION` and `EXT-ES-RECOVERY` with repository owner, issue/story or
   escalation reference, release version/digest, evidence path, and acceptance status. The EventStore owner and
   release IDs are unresolved human/platform escalation fields; validation must reject placeholder acceptance.
   `EXT-ES-EVENT-EVOLUTION` evidence must include the envelope/registry API, stable logical-type rules, payload
   version distinct from `MetadataVersion`, and the accepted legacy-alias fixtures consumed by Stories 12.1–12.2.
   Validate uniqueness, acyclicity, strict ordering, and accepted-terminal exemptions.
6. Add OQ11–OQ13 with the PRD evidence paths and approvers. OQ11: Product + Architecture + Security + Test;
   OQ12: Product + Architecture + Security + Operations + Test; OQ13: Product + Architecture + Operations +
   Test. Record their decisions as admitted/approved and their delivery evidence as open.
7. Record OQ3 `decision_status: superseded-pending-reapproval` until A6b signs matrix `2.0.0`; keep runtime
   evidence open. Split OQ8 into accepted-terminal `OQ8-DESIGN` (approved digest) and rank-50
   `OQ8-EVIDENCE` following 12.6. Give every decision/evidence node separate `decision_status`,
   `execution_authorization_status`, and `delivery_evidence_status`; reject the reverse OQ8 cycle.
8. Extend governance status vocabulary to `superseded-pending-reapproval` and update YAML schema plus
   `ApprovalBackedCriteriaCarryFreshExactApprovalRecords` and
   `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` in the same Delivery change. Record proposal path,
   superseded digest, final digest when available, and exact approvers.
9. Add status/error fingerprints to the symmetric Contract Spine drift fixture and regenerate the v2 OpenAPI,
   client, CLI/MCP parity schema/oracle, `previous-spine.yaml`, C13 inventory, docs, and tests. Remove the
   protected `not_found`/authorization-outcome oracle; map `read_model_unavailable` to CLI 73 and
   `concurrency_conflict` to CLI 77/MCP `concurrency_conflict`.
10. Keep `general_execution_hold: true`. Encode `RELOCK-PLANNING`, `1.17-GENERATE`, and `RELOCK-SECTION9` as the
    proposal-routed work package with `execution_authorization_status: relock-only`, their exact allowed artifacts
    (planning/provenance, v2 generated surfaces, C3/C6/C9 governance, and corresponding Section 9 conformance
    code/tests), and `delivery_evidence_status: open`. Every ordinary nonterminal row remains `held`.
    `1.17-GENERATE` produces the final input to A6b; it does not require A6b. The lane cannot expose v2, publish,
    close lifecycle status, or produce OQ5–OQ13 runtime evidence. `RELOCK-SECTION9` requires accepted A6b; only
    A8 after that gate may set the hold false and close the recovery action. Every ordinary node then consumes
    A8 through the universal prerequisite.

## 5. Human approvals still required

| Gate | Required roles | Evidence |
| --- | --- | --- |
| A1/PD1/OQ11 | Product, Architecture, Security, Test | Epic 12/OQ11 admission and durable-slice ownership |
| A2/PD3 | Product, Architecture, Security, Operations, Test | Epic 13 and OQ12/OQ13 admission; no Story 13.7 evidence required for this decision |
| A2b/PD6 | Product, Architecture, Security, Operations, Test | Exact NFR74–NFR84 PRD/epics/traceability digest |
| A3/lifecycle | Product, Architecture, Delivery | Proposal Section 6 reconciliation records; no frozen-spec rewrite |
| A4/PD5 | Product, Security, Architecture | Narrowed metadata-only Story 10.9 acceptance definition |
| A5/C9/PD8 | Product, Architecture, Security | Final token-only C9 digest, including immutable token rotation and fail-before-admission limitation |
| A6/PD10 | Product, Architecture, Security, Contract/Delivery | All v2 rules and generated-surface scope |
| A6b/OQ3/PD10 | Product, Architecture, Security | Matrix `2.0.0` digest; generated conformance is separate Section 9 evidence |
| A7/C6/PD11 | Product, Architecture, Security | Final transition/disposition/guard digest; implementation tests are separate Section 9 evidence |
| A7b/C3 | Legal, Product, Security, Architecture | Final P7D post-terminal/no-active cleanup digest, separate non-destructive recovery clock, resume behavior, legal hold |
| A8/freeze | Product, Architecture, Delivery | Regenerated manifest and complete proposal Section 9 result |
| OQ12-EVIDENCE | Product, Architecture, Security, Operations, Test | Supported-profile security/recovery evidence after Story 13.7 |
| OQ13-EVIDENCE | Product, Architecture, Operations, Test | Supported-profile capacity evidence after Story 13.7 and C1/C4/C5 |

The sponsor approval does not substitute for these role attestations. Legal is required for A7b. Governing PD8
forbids reversible recovery; any future ciphertext/unsealing exception needs a revised Product + Architecture +
Security decision and an explicit Legal classification/retention determination. Platform/Delivery must also name
the EventStore owners and release evidence for both `EXT-ES-*` nodes. Until the Section 9 gates pass, the hold and
`implementationReadiness: not-ready` remain unchanged; only the explicit relock-only package may proceed.
