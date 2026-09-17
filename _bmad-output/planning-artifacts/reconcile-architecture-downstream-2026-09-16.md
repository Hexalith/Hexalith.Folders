# Downstream Reconciliation — Architecture Update 2026-09-16

**Source run:** `_bmad-output/planning-artifacts/architecture/architecture-folders-2026-07-19/` (Update intent)
**Trigger:** `validation-report-2026-09-16.md` — Reviewer Gate **FAIL**, 96 findings (12 critical / 37 high / 34 medium / 13 low) across six lenses.
**Scope ratified by Jerome (2026-09-16):** apply all four disposition tiers to `architecture.md`; route Tier 3's six undecided dimensions as owned open items rather than deciding them unilaterally; lockstep bounded to **architecture.md + co-normative docs artifacts**, with **no production code** and **no `epics.md` edits**.

This note is the record of what the architecture now asserts and **what lockstep it owes to artifacts this run deliberately did not touch**. Nothing in the "Owed" sections has been applied.

---

## 1. What changed in this run

### Applied to `_bmad-output/planning-artifacts/architecture.md`

| Tier | Change |
| --- | --- |
| **T1** | **S-7 denial envelopes restated verbatim from `docs/contract/authorization-matrix.md` §"Canonical Outcomes"**, with that matrix named owner-on-disagreement. The invented category `authorization`, the invented category `availability` and the invented code `authority_unavailable` are gone; the 404 collapses to one code chosen by no predicate; the previously-missing **401** outcome is restored, so absent or malformed authentication no longer routes to a retryable 503; envelope identity is declared to extend past `category` to code, message, `type` URI, detail keys, `retryReasonCode`, `layer` and `timingBucket`. |
| **T2** | `(state, event, guard)` keying propagated from the single paragraph that held it to the matrix preamble, the C6 CI-gate bullet and the PR-review rule, each carrying an explicit **default rule: an unenumerated guard branch rejects and never falls through to its sibling**. Durable **`stagedByTaskId`** declared as the field the `dirty` + `WorkspaceLocked` guard reads, under **one two-conjunct predicate** stated identically in both artifacts (staged changes present AND `stagedByTaskId` equals the *server-resolved* task) — explicitly **not** the caller-supplied `X-Hexalith-Task-Id` header; `stagedByPrincipal` is audit evidence read by no guard. Cleanup trigger restated in C3's task-closure vocabulary (`inaccessible` is a workspace state, not a task closure). Reality table corrected in the **strict** direction. |
| **T3** | Six dimensions recorded as routed `Open —` items with named owners: provider-endpoint destination policy; deny-by-default HTTP authorization binding; aggregate write concurrency; event-payload schema evolution; deployment profile / environment inventory / replica-scaling; disaster recovery / backup / restore. The PD8 operational carve-out was sharpened — it is now stated plainly that the carve-out **falsifies S-6's headline guarantee** and that S-6 must be read as scoped to evidence and observability surfaces until a control set is chosen. |
| **T4** | Version pins **removed** rather than refreshed, with the **repo-root `Directory.Packages.props`** cited as the entry point (it imports the shared Builds catalog), plus the two things it does not own: the `Aspire.AppHost.Sdk` pin in `Hexalith.Folders.AppHost.csproj` (CPM cannot version a project-SDK resolver) and its exception record — **the same SDK-vs-Hosting axis as the Epic 9 DCP blocker**. A constraint-vs-current-version rule was added so genuine constraints survive the removal, and two are recorded: the Fluent UI **prerelease** underwriting F-3's WCAG claim, and `LibGit2Sharp`'s native **libgit2 ABI** with its Alpine/musl smoke lane. `Testcontainers` (unused) removed; `oasdiff` corrected here and in three published docs. Package-management corrected — the selector is **`Configuration`** (Release → NuGet), not an explicit flag. `Hexalith.Folders.EventStore` added to boundaries, app-ids and both trees. The `✅ Requirements Coverage Validation` heading de-checkmarked and the nine-of-eleven gap stated. Story 10.9 re-framed per A4/PD5. |
| **Strata** | Banners on §"Complete Project Directory Structure" (TARGET layout; `Hexalith.Folders.slnx` authoritative for inventory, filesystem authoritative for paths) and §"Requirements to Structure Mapping" (target routing, not an as-built index), plus per-entry `NOT BUILT` / `AS-BUILT` markers and honest as-built/target splits on the **I-3**, **I-8** and **C10** enforcement rows. |

### Applied to co-normative docs (the ratified lockstep bound)

- **`docs/exit-criteria/c6-transition-matrix-mapping.md`** — re-keyed to `(state, event, guard)`; new **Guard Discriminators** table naming all four pairs, both branch outcomes, and the durable field each guard reads; the three pair-keyed Mapping-Rules rows rewritten with guard-branch approval status.
- **`docs/diagrams/workspace-lifecycle.md`** — no edit needed; it already carried the guard note from the 2026-09-15 lockstep.

**Reviewer Gate:** six lenses ran against the amended document; all six returned **FAIL**, and a second fix pass was applied in response. Closure against the prior gate was nonetheless substantial — **28 of 49** criticals+highs closed, 12 routed, **0 criticals left un-disposed**, the A6b-blocking defect closed, and the S-7 row verified **byte-exact on all 18 cells** of the approved matrix. The residue was concentrated in text the *first* pass added: three lenses independently hit the same two S-7 sentences, two hit the `stagedBy*` guard, and the code-reality lens found eight new false claims, all corrected in pass two. Gate reports: `reviews/review-{authority-conformance,adversarial,technology,rubric,code-reality,security}-update-2026-09-16.md`.

**Two findings worth surfacing on their own**, because both mean the codebase moved ahead of the document rather than behind it:
- **The "Deployed-Server limitation" was stale, not unlabelled.** Stories **10.7 and 10.8 are `done`**, and the Server now registers `EventStoreSemanticIndexingBridgeStore`. The documented deployed effect — context-search returning zero items — has not described HEAD for some time.
- **Story 12.4's seam is fail-closed, not missing.** `IWorkspaceCommitExecutor` exists and the Server registers `UnavailableWorkspaceCommitExecutor`; the story replaces a registration, not a throwing stub.

**Verification:** `Hexalith.Folders.Contracts.Tests` **314/314 pass, 0 failed** (`ConsumerDocsConformanceTests` 22/22) — unchanged from the pre-edit baseline. The mapping-doc additions do not move any pinned count: `ParseC6EventVocabulary` is anchored to the "copied for drift checking" line and bounded by the next blank line, and `C6StateCatalogRow` requires an `Architecture C6 state catalog` provenance column the new table does not carry.

---

## 2. Owed lockstep — **NOT applied**, by ratified scope

### 2.1 `epics.md` — story admission (owner: PM + Delivery)

`epics.md` carries **zero** occurrences of PD8, PD10 or PD11, re-verified 2026-09-16. Every mechanism this relock adds is unowned at every execution rank while rank-30 stories already list that work as a prerequisite. Three owning stories are owed:

- **PD10 — authorization-spine correction.** Spine amendment; regeneration of OpenAPI, the generated client, CLI/MCP parity fixtures, `previous-spine.yaml`, the C13 inventory and docs; the status-code / error-vocabulary surface the drift fixture currently lacks; OQ3 re-approval (A6b). **Collision to sequence:** Story 13.2 owns `NFR76` deny-by-default and will build a second, divergent shape if it lands first.
- **PD11 — guard-discriminated lifecycle.** Re-key `FolderStateTransitions.cs` and its CI gate to `(state, event, guard)`; declare the durable `stagedByTaskId` / `stagedByPrincipal` fields; update `DispositionLabelMapper.cs` for the conditional `dirty` disposition and the `unknown_provider_outcome` divergence; add `LockLeaseBecameStale` to the spine. **This is a live defect, not a latent one** — the pair-keyed gate currently passes the implementation that destroys staged work.
- **PD8 — confidentiality tier.** A tokenizer component (which has **no owning component anywhere today**; `FolderAuditSanitizer.cs` is explicitly not it), its per-tenant key management and rotation policy, the `confidential` tier, and the `withheld` render state.

(The earlier draft of this note claimed `NFR74` has no owning Epic 13 story. That was **wrong** — `nfr-traceability.md:120` assigns it to `13-2`, which `sprint-status.yaml` carries. Corrected in both this note and the architecture.)

### 2.2 `planning-story-manifest.yaml` — regeneration (owner: Delivery)

Still `generated_on: '2026-08-04'`, with zero occurrences of `execution_rank`, `execution_waves`, `story_lifecycle_status` or OQ11–OQ13. Unchanged by this run and still owed.

### 2.3 Governance vocabulary (owner: Governance)

The supersession task recorded on 2026-09-15 is unchanged and still owed: extend the governance vocabulary with `superseded-pending-reapproval` and teach both `ApprovalBackedCriteriaCarryFreshExactApprovalRecords` and `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` to accept it for a criterion carrying a supersession record — YAML, schema and tests in one commit. Until then **architecture.md is the record of supersession** and the YAML keeps its historical values.

### 2.4 Approval re-review (owner: Architecture team, with A7b)

`docs/exit-criteria/c6-transition-matrix-mapping.md` is an **approved** artifact (`last reviewed: 2026-05-11`). The guard dimension added this run is marked **approval-pending under A7b** in each affected row rather than silently re-approved. A7b must sign the guard keying, the guard-branch default-rejection rule, and the guard-branch coverage obligation.

### 2.4b Contract-surface work the S-7 correction newly exposes (owner: Contract + Delivery)

The Reviewer Gate established that the HTTP envelope fix does not reach the surfaces below it, and these are now named in the architecture as owed:

- **The C13 parity oracle has no rows** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`. The shipped `parity-contract.yaml` maps them to CLI exit **65** and **72**, and the architecture's own table defines **72** as `reconciliation_required`, *"not retryable until cleared"* — contradicting the 503's `retryable: true`. Until those rows exist **CLI and MCP can still disagree about an authority outage**, which is the divergence S-7 exists to close.
- **`not_found` → exit 73 survives** on `parity-contract.yaml`'s `not_found` rows, alongside `auth_outcome_class` values (`folder_acl_denied`, `audit_access_denied`) that reproduce the existence oracle one surface below the HTTP envelope.
- **`canonical_error_category` and `mcp_failure_kind` still enumerate the three codes PD10 removes**, and no closed vocabulary exists anywhere for the `code`, `clientAction`, or `visibility` axes. Creating one is part of the correction, not a property it can assume.
- **`withheld` and top-level `visibility` are proposed, not approved** — both are A6b material, and neither may be cited as current contract.

### 2.4c The approved matrix contradicts itself on `stale` (owner: Security + Architecture, with A6b)

`docs/contract/authorization-matrix.md:76` routes the `stale` access state to `safe-denial-404`; `:146` routes stale authority evidence to `authority-unavailable-503`. A 404 and a retryable 503 are opposite instructions to a client. This must be resolved **in the matrix** before A6b, and it is why the architecture now records degraded mode as an open item rather than claiming the matrix settles it.

### 2.5 `ux-design-specification.md` (owner: UX + Security)

The `confidential` tier and the **`withheld`** render state — distinct from `redacted`, `unavailable` and `absent` — still do not reach the UX contract or the F-5 redaction affordance. Owed whenever PD8 gets an owning story.

### 2.5b `epics.md` still names `oasdiff` (owner: Contract + Delivery)

`epics.md` carries the retired `oasdiff` name at `AR-PROVIDER-04`. The architecture and all three published `docs/` artifacts were corrected in this change set; `epics.md` is out of the ratified scope and is owed the same one-line correction to the as-built lane (`tests/tools/run-nightly-drift-gates.ps1` driving `tests/tools/forgejo-drift/`).

### 2.6 `epics.md` header-name collision (owner: Contract + Delivery)

`epics.md` encodes the **request-side** spelling for what D-9 defines as the response-side `X-Hexalith-Retry-Transport`. The architecture keeps the two names disjoint deliberately; `epics.md` does not. One-line fix, outside this run's scope.

---

## 3. Open items now carried by the architecture

Eleven routed open questions, each naming an owner. Five pre-date this run (`:214` rank rule, `:276` unowned corrections, `:432` staged-content window, the PD8 boundary, the A-11 migration/rollout decision); six are new (§T3 above). None is a blocker to *this* document; several are blockers to the work downstream of it:

| Open item | Blocks | Owner |
| --- | --- | --- |
| PD8 operational boundary — durable cleartext | C9 relock, Story 12.4 | Security + Architecture (A7b) |
| Provider-endpoint destination policy (SSRF) | OQ12 | Security + Architecture |
| Deny-by-default HTTP authorization binding | Story 13.2 | Security + Architecture |
| Aggregate write concurrency | Epic 12, NFR80 | Architecture |
| Event-payload schema evolution | Epic 12, `LockLeaseBecameStale` | Architecture |
| Deployment profile / environment inventory | OQ12, OQ13 | Architecture + Delivery |
| Disaster recovery / backup / restore | Epic 13 | Architecture + Operations |
| A-11 migration & rollout for the PD10 breaking change | A6b | Contract + Delivery |
| Staged-content window unreachable under the C3 trigger | PD11 rule 3 | Legal + Product + Security (A7b) |
| §7.1 rank rule not satisfiable by its own table | Wave scheduling | Delivery + PM |
| No story owns PD8 / PD10 / PD11 | Everything above | PM + Delivery |

---

## 4. Explicitly preserved

The prior gate verified these as strong and the remediation did not touch them: the 2026-09-15 honesty convention and its *Current reality* table; the planning-consistency invariant and "admission is not implementation evidence"; every byte-exact digest, count and ownership cell (OQ3 `5ffabd71…`, C12 `5799e090…`, C7 30/15/60/60, NFR 84/11, C6 41 edges / 24 events / 11 states); the `{C3, C4, C7, C12}` hard-pin; the NFR row statuses; and the governance YAML statuses. The execution freeze and `implementationReadiness: not-ready` are retained.
