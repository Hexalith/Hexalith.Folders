# Reviewer Gate — Authority Conformance and Lockstep (2026-09-16)

- **Reviewed artifact:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, committed at `de281e7`; working tree == HEAD for this file)
- **Authority under review:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md` (`status: approved`, sponsor Jerome 2026-09-15T16:08:21+02:00, A1–A8; `required_role_signoffs: pending`, `execution_freeze: retained`)
- **Lens:** authority conformance and lockstep — is `reconciledTo:` true, and is the document consistent with every sibling artifact it must move with
- **Reviewer:** BMad architecture Reviewer Gate (read-only; no artifact other than this report was written)
- **Prior report:** `reviews/review-authority-conformance-2026-09-15.md` (PASS WITH FINDINGS, F1 CRITICAL)
- **Date:** 2026-09-16

## Verdict

**PASS-WITH-FINDINGS — 1 critical (must-fix before the PD10 spine regeneration), 5 high, 5 medium, 3 low.**

The `reconciledTo` claim is **substantially true**. All six §5.2 edits landed; §7.1 reproduces semantically edge-for-edge; §7.2's ten rules and §7.3's eight rules all have a faithful home; every digest, timing value, NFR total, NFR row-ownership assignment and C6 count I recomputed reproduces byte-exact. **The prior run's CRITICAL (F1) is CLOSED and verified by execution.**

The residue is of one kind: places where the document *over-specifies* past its authority, or where an older sentence was not swept when the relock landed. The single critical is one such over-specification — S-7 invents a denial vocabulary that contradicts the approved OQ3 matrix and the PRD — and it matters because S-7 is the text the A6/A6b spine regeneration is meant to consume.

**Verified by execution:** `dotnet run --project tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` → `Total: 314, Errors: 0, Failed: 0, Skipped: 0`. **No CI conformance gate is red because of this document today.**

## Prior-gate closure

| Prior | Severity | Status now | Evidence |
| --- | --- | --- | --- |
| **F1** — C6 table reddens `WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix`, gate report lies "passed" | CRITICAL | **CLOSED** | `de281e7` landed the lockstep: `docs/diagrams/workspace-lifecycle.md` (41 edges, 24 events, `dirty` disposition row + state label), `docs/exit-criteria/c6-transition-matrix-mapping.md` (`dirty` row, `LockLeaseBecameStale` added), `ConsumerDocsConformanceTests.cs:542` `ShouldBe(24)` and `:557` `ShouldBe(41)`. I re-ran the gate's own parsers: 41 matrix edges == 41 diagram edges (symmetric difference empty), 24 == 24 events, 11 states. 314/314 green. `_bmad-output/gates/consumer-docs/latest.json` `"status": "passed"` is now honest. |
| F2 — two §7.3 rules not expressible from the table | HIGH | **CLOSED** | `architecture.md:436` now specifies a switch over `(currentState, eventType, resolution)` and names all four guard-discriminated pairs; `:437` keys the CI gate on `(state, event, guard)`. `FolderStateTransitions.cs:50` already carries the third tuple element. |
| F3 — `LockLeaseBecameStale` presented as a matrix-only rule | HIGH | **CLOSED** | `architecture.md:649` "**`LockLeaseBecameStale` is a Contract Spine change too**" names spine, client, `previous-spine.yaml`, C13 inventory. |
| F4 — governance directive would redden two gates, silently | HIGH | **CLOSED** | `architecture.md:329` now names both tests by name, states that `pending` is not in the vocabulary, prescribes a `superseded-pending-reapproval` status as lockstep YAML+schema+test work, and says the YAML keeps historical values until then. Honest. |
| F4b — overlay asserts a regenerated manifest that does not exist | HIGH | **CLOSED** | `architecture.md:206` now reads "**regeneration owed (§5.5); still `generated_on: '2026-08-04'`**"; `:210` adds the interim-authority caveat; `:243` records the reality. |
| F5 — blank line breaks the S-decision table | MEDIUM | **CLOSED** | S-6/S-7/S-8 are contiguous at `architecture.md:639–641`. |
| F6 — two unauthorised events on `changes_staged → inaccessible` | MEDIUM | **OPEN** → **AUTH-9** (and now pinned by the 41-edge count) |
| F7 — `dirty` disposition became a predicate, diverging downstream | MEDIUM | **PARTIAL** | `c6-transition-matrix-mapping.md:24` and `workspace-lifecycle.md:22` now carry the predicate. `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` and `FolderStateTransitions.cs:157` still map `Dirty => AwaitingHuman`, and `architecture.md` does not disclose that in its "Current reality" table (`:240`). See AUTH-4 note. |
| F8 — S-7 removes the post-authentication 403 that §7.2 rule 1 does not name | LOW | **CLOSED (now sourced)** | `docs/contract/authorization-matrix.md:150` "Post-authentication 403 is retired from the canonical design." The removal has an authority. |
| F9 — NFR81/NFR82 paraphrases dropped their qualifiers | LOW | **CLOSED** | `architecture.md:272` restores "being unable to report ready from configuration or seed data alone" and "every release-significant metric and alert". |
| F10 — Epic 12 ownership row not updated to 12.6 | LOW | **OPEN** → **AUTH-12** |
| F11 — NFR79/NFR80 mechanism-allocation clarification | LOW | acceptable; retained at `architecture.md:274` and matches `nfr-traceability.md:125–126` exactly |
| F12 — decoupling-precedent paragraph stale about C3 | LOW | **OPEN** → **AUTH-8** |

---

## Findings

### AUTH-1 — CRITICAL — S-7 names a denial vocabulary that contradicts the approved OQ3 matrix and the PRD

- **Location:** `architecture.md:640` (S-7):
  > "**Both envelopes are fully named so the C13 `cli_exit_code` / `mcp_failure_kind` columns stay derivable:** the 404 carries category `authorization`, code `tenant_access_denied` (resource-shaped denials use code `resource_unavailable` under the same category), `retryable: false`, client action `verify_tenant_context_and_authorization`; the 503 carries category `availability`, code `authority_unavailable`, `retryable: true`, client action `retry_after_backoff`, and `details.visibility: redacted`."
- **Contradicting source 1 — the approved OQ3 matrix.** `docs/contract/authorization-matrix.md:145`:
  > "| `safe-denial-404` | 404 | `tenant_access_denied` | `resource_unavailable` | false | `no_action` | `redacted` | …"

  and `:146`:
  > "| `authority-unavailable-503` | 503 | `read_model_unavailable` | `projection_unavailable` | true | `retry` | `redacted` | …"
- **Contradicting source 2 — the PRD.** `_bmad-output/planning-artifacts/prd.md:611`: "The Spine carries the safe denial today as category `tenant_access_denied` with code `resource_unavailable`". `prd.md:1156` (PD10, sponsor-approved): "one 404 `tenant_access_denied`/`resource_unavailable` safe-denial shape".
- **Contradicting source 3 — the ratified proposal.** `sprint-change-proposal-2026-09-15.md:321–323` (§7.2 rule 1): "one 404 `tenant_access_denied/resource_unavailable` shape".
- **Contradicting source 4 — the canonical vocabulary itself.** `tests/fixtures/parity-contract.schema.json` `$defs/canonical_error_category` enumerates 50 values and `$defs/mcp_failure_kind` 49; `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:13324` enumerates 49. **None of them contains `authorization`, `availability`, or `authority_unavailable`.** `tenant_access_denied` and `read_model_unavailable` are *categories*; `resource_unavailable` and `projection_unavailable` are the *codes*.
- **Why critical.** The architecture inverts the category/code pairing, invents two categories that do not exist in the closed vocabulary, and invents one code. It also contradicts itself: its own CLI exit-code table (`architecture.md:691`) maps exit 66 to **canonical category `tenant_access_denied`**, and A-8 (`:664`) declares the RFC 9457 field set. `architecture.md:645` then says "Release generation and parity gates consume **only** the corrected, reapproved matrix" — so if an implementer takes S-7's named values as the correction, the regenerated surface will not match the matrix A6b must reapprove, and `CanonicalErrorCategory` parity will break. This is the one place in the amendment where the mechanism authority contradicts the product and contract authority on a wire-visible value.
- **Correction.** Replace the named values with the matrix's: 404 → category `tenant_access_denied`, code `resource_unavailable`, `retryable: false`, client action `no_action`, `details.visibility: redacted`; 503 → category `read_model_unavailable`, code `projection_unavailable`, `retryable: true`, client action `retry`, `details.visibility: redacted`. If the architect believes `no_action`/`retry` are the wrong client actions, that is a matrix amendment routed through A6/A6b, not a divergent restatement in S-7.

### AUTH-2 — HIGH — "49 of 50 protected operations" reproduces from nothing; every authority says 49

- **Location:** `architecture.md:241` (Release Authority Overlay → "Current reality"):
  > "| **PD10** authorization spine | HTTP 403 is live on 49 of 50 protected operations. …"
- **Contradicting sources:**
  - `docs/contract/authorization-matrix.md:15`: "Denominator scope: 12 canonical access states, 11 protected operation families, **49 Contract Spine operations**, and 8 FR8 scope dimensions."
  - `docs/contract/authorization-matrix.md:341` (gap `G1`): "The Contract Spine declares two status-distinct safe-denial envelopes after authentication: **403 on 49 of 49 operations** and 404 on 46 of 49."
  - `docs/contract/oq3-authorization-evidence.yaml` `denominator.spine_operations: 49`.
  - `prd.md:1156` (PD10): "authority evaluated before resource lookup on **all 49 protected operations**".
  - `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs:77` `spineOps.Count.ShouldBe(49)`, `:131` `expectedTools.Count.ShouldBe(49)`, `:568` `spineOps.Count.ShouldBe(49, "the spine must carry the canonical 49-operation surface.")`.
  - `tests/fixtures/parity-contract.yaml` — 49 `operation_id` rows (counted).
- **Reproduction:** I searched `docs/`, `src/`, `tests/` for any "50 protected"/"50 operations" denominator. **There is none.** The architecture's own S-7 at `:640` says "All **49** protected Contract Spine operations", so the document contradicts itself two sections apart.
- **Correction.** "HTTP 403 is live on 49 of 49 protected operations (404 on 46 of 49) — `authorization-matrix.md` gap `G1`."

### AUTH-3 — HIGH — `architecture.md:181` still frames Story 10.9 as the body-content follow-up, which PD5/A4 abolished

- **Location:** `architecture.md:181`:
  > "… **Story 10.8** proves the non-empty authorized round trip … and is the FR58 completion story; **Story 10.9** is the separately C9-gated body-content follow-up."
- **Contradicting authority:** `sprint-change-proposal-2026-09-15.md:162` (§5.1 item 6, PD5): "metadata-only search is the MVP behavior; the story's completion bar is proof that unauthorized or unapproved body content cannot be indexed, hydrated, or returned. **A future body-content capability needs a new stable requirement and story after C9 approval.**" `:214` (§5.4): "retitle Story 10.9 to **'Preserve Metadata-Only Indexing Until C9 Body-Content Approval'**". `prd.md:1154` records PD5 as sponsor-approved in those terms.
- **Contradicting sibling in the same document:** `architecture.md:225`: "Story 10.9 is narrowed to the metadata-only safety guard (PD5) and is **not** a forward capability dependency. A future approved body-content capability requires a new requirement, story, rank, and dependency path."
- **Also stale at:** `architecture.md:286` ("Stories 10.7–10.9" as the search-bridge/live-proof ownership row), `:1292`, `:1580`.
- **Note:** `epics.md:2327` still reads "### Story 10.9: Authorized body-content materialization — C9 gated", i.e. the architecture at `:181` is in lockstep with the *stale* epics text and out of lockstep with the ratified decision. The epics retitle is owed under §5.4 and is recorded as owed in `reconcile-architecture-downstream-2026-09-15.md:94`.
- **Correction.** Rewrite `:181`'s last clause to the PD5 scope and point the future capability at a new, unnumbered requirement/story; sweep `:286`.

### AUTH-4 — HIGH — `unknown_provider_outcome` disposition still diverges across the three artifacts the architecture says "express one model"

- **Location:** `architecture.md:373` (C6 state catalog):
  > "| `unknown_provider_outcome` | `auto-recovering` (automatic bounded reconciliation in progress; `awaiting-human` begins only at `reconciliation_required` — 2026-07-15) | …"

  and `:426` (PD11 rule 5, normative): "**`unknown_provider_outcome` is `auto-recovering`** … Operator disposition becomes `awaiting-human` at `reconciliation_required`, never before."
- **Contradicting sources:**
  - `docs/exit-criteria/c6-transition-matrix-mapping.md:29`: "| `unknown_provider_outcome` | `awaiting-human` | Architecture C6 state catalog | approved | … | 2026-05-11 |" — the provenance column claims it *came from* the architecture catalog it now contradicts.
  - `docs/diagrams/workspace-lifecycle.md:26` and `:41` (`state "awaiting-human · unknown_provider_outcome"`).
  - `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` / `FolderStateTransitions.cs:157` carry the pre-PD11 model for `dirty` as well.
- **Why this is a lockstep defect, not just code debt:** `architecture.md:420` states "This matrix, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, and the lifecycle tests express **one** model; a divergence is a defect in the code or the test, never an alternative reading." The `de281e7` C6 lockstep commit edited exactly this table and corrected the `dirty` row while leaving `unknown_provider_outcome` on the superseded value. `prd.md:729` and `prd.md:1157` name this disposition explicitly as an item PD11 resolves. `ux-design-specification.md` UX-DR35 requires the canonical `auto-recovering`.
- **The gate cannot see it.** `ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramDispositionTableMatchesC6StateCatalog` (`:526`) compares the *mapping doc* to the *diagram* — both say `awaiting-human` — so it passes while the architecture disagrees with both. Verified green in my run.
- **Correction.** Either land `unknown_provider_outcome → auto-recovering` in the mapping doc, the diagram, `DispositionLabelMapper.cs` and its tests in one commit, or record it explicitly in `architecture.md:240`'s "Current reality" table as a known PD11 target-state gap. Do not leave the "one model" sentence asserting something demonstrably false.

### AUTH-5 — HIGH — "Requirements Coverage Validation ✅" still asserts nine NFR categories and full gate coverage after the 84-row relock

- **Location:** `architecture.md:1708`:
  > "**Non-Functional Requirements Coverage:** Every NFR category (security/tenant isolation, reliability/idempotency/failure visibility, performance/query bounds, scalability/capacity, integration/contract compatibility, observability/auditability/replay, data retention and cleanup, operations console accessibility, verification expectations) is bound to specific architectural decisions and at least one CI gate or runbook."

  and `:1718`: "**Verification Expectations:** every NFR has at least one CI gate, lint, codegen rule, or release-validation evidence path".
- **Contradicting sources:**
  - `architecture.md:58` (same document): "Eleven NFR categories drive architecture (nine original, plus Edge Security & Deployment Hardening and Durable Operation & Release Evidence admitted 2026-09-15 as NFR74–NFR84)".
  - `docs/exit-criteria/nfr-traceability.md:120–130` — all eleven of NFR74–NFR84 carry `—` in the **Automated gates** column and status `reference-pending`, "Release-blocking".
  - `tests/.../NfrTraceabilityConformanceTests.cs:67–68` pins the two new category bands `(74, 78, "Edge Security & Deployment Hardening")` and `(79, 84, "Durable Operation & Release Evidence")`; `:249` requires the category counts to sum to `NfrTotal = 84`.
- **Why high:** the section carries a ✅ and the sentence at `:1718` is now false for eleven requirements. The document's own planning-consistency invariant (`:253`) forbids exactly this: "any completion claim backed only by no-op/unavailable/seed-only/fake-only evidence fails the planning-consistency gate" — and here the claim is backed by *no* evidence. `architecture.md:278` correctly says "no NFR74–NFR84 row may be marked implemented merely because it was admitted"; `:1708`/`:1718` undo that 1400 lines later.
- **Correction.** Add the two categories to the enumeration and to the bullet list, and qualify `:1718` — "every NFR1–NFR73 has at least one … path; NFR74–NFR84 are admitted and owned but have no automated gate yet (`nfr-traceability.md`, eleven release-blocking reference-pending rows)."

### AUTH-6 — HIGH — Epic 13 is credited with HXF-SEC-001 / NFR74, but no Epic 13 story owns it

- **Location:** `architecture.md:270`:
  > "Epic 13 (Stories 13.1–13.6, ratified in `sprint-status.yaml`) … Scope: **HXF-SEC-001 (bearer JWT over plaintext HTTP in CLI+MCP → require HTTPS-or-loopback)**, HXF-SEC-002 …"

  and `:274`: "Epic 13 owns **NFR74** and NFR76 (`13-2`) …"
- **Contradicting sources:**
  - `_bmad-output/implementation-artifacts/sprint-status.yaml:242–247` — Epic 13 has exactly six story keys: `13-1` Forgejo SSRF, `13-2` fail-safe fallback authorization + sidecar-only app port, `13-3` credential file 0600, `13-4` readiness snapshot + UI health, `13-5` alert instruments + Dapr statestore/resiliency, `13-6` rate limiting/timeouts/body caps + sensitive-value filter. **None is the HTTPS-or-loopback bearer-transport story.**
  - `epics.md:2731–2742` — Story 13.2's acceptance criteria are entirely about absent/malformed route authorization metadata, fallback deny, and app-port reachability through the sidecar. There is no HTTPS/loopback transport clause, so NFR74 has an owner of record with no acceptance criterion behind it.
  - `docs/exit-criteria/nfr-traceability.md:120` does assign NFR74 to `13-2` — so the architecture faithfully mirrors its stated authority; the break is between that authority and `epics.md`.
- **Why high:** the architecture's own rule at `:274` is "Per-row ownership is the traceability table, not the band … it — not this paragraph — is authoritative." That makes NFR74 formally owned and substantively unowned. `architecture.md:276` already flags the analogous PD10 gap ("No story owns the PD10 spine correction yet"); the NFR74 gap deserves the same routing.
- **Correction.** Route to PM + Delivery alongside the PD10 gap: either add an Epic 13 story for HXF-SEC-001, or extend Story 13.2's acceptance criteria to carry the NFR74 transport obligation, and say so in `architecture.md:274`.

### AUTH-7 — MEDIUM — A-8 declares `visibility` required on every error; the normative error example omits it

- **Location:** `architecture.md:664` (A-8, amended by the relock): "**`visibility` is a required field on every error (PD10, 2026-09-15);**". Repeated at `:640` ("`visibility` is a required field on every error").
- **Contradicting sibling in the same document:** `architecture.md:887–905`, "**Error response format (per A-8):**", whose JSON carries `type`, `title`, `status`, `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details` — **no `visibility`**, at any level.
- **Why it matters:** §"Format Patterns" is the section implementers copy. A required field that is absent from the canonical example is how a second, divergent error shape gets built — the same failure mode `architecture.md:276` warns about for deny-by-default.
- **Correction.** Add `"visibility": "metadata_only"` to the example and a one-line note that the S-7 envelopes use `redacted`, and that `visibility ∈ {metadata_only, redacted, withheld}` is enumerated (per `:640`).

### AUTH-8 — MEDIUM — the decoupling-precedent paragraph still says C3 is approved, three paragraphs after declaring it superseded (prior F12, not fixed)

- **Location:** `architecture.md:331`:
  > "**C3, C4, and C7 are approved** while NFR60, NFR30/NFR33, and NFR7/NFR21 respectively remain reference-pending for downstream implementation evidence."
- **Contradicting siblings in the same document:** `:232` ("C3 retention … **Superseded — approval-pending.**"), `:315` ("**SUPERSEDED, approval-pending (A7b, 2026-09-15)**"), `:329` ("changes the content behind C3, C6, and C9 … each is approval-pending until re-signed").
- **Mirrored, unchanged, in two siblings:** `docs/exit-criteria/nfr-traceability.md:39–40` ("`C3` with `NFR60` … are approved criteria whose cited rows stay reference-pending below"); `docs/exit-criteria/c0-c13-governance-evidence.yaml:57–63` (comment block, "C3, C4, and C7 are approved below").
- **Assessment:** the sentence is literally true *of the governance YAML* (`c0-c13-governance-evidence.yaml:87` still `status: approved` for C3), and `:329` is explicit and honest that the YAML keeps historical values until the `superseded-pending-reapproval` vocabulary lands. So this is **a tracked gap the document is honest about** — but the precedent paragraph reads as a current statement of criterion status and does not carry the qualifier.
- **Correction.** "C3 (relocking under A7b), C4, and C7 are approved in the governance YAML while …". Do **not** touch the `{C3, C4, C7, C12}` hard-pin or any NFR row status — the amendment gets that right and `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` / `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` both pass.

### AUTH-9 — MEDIUM — `changes_staged → inaccessible` asserts three triggering events where §7.3 authorises one, and the deviation is now CI-pinned (prior F6, not fixed)

- **Location:** `architecture.md:400`:
  > "| `changes_staged` → `inaccessible` | `AuthRevocationDetected` / `TenantRevoked` / `RepositoryDeletedAtProvider` | Staged changes preserved intact; …"
- **Contradicting authority:** `sprint-change-proposal-2026-09-15.md:352` (§7.3 rule c): "`changes_staged + AuthRevocationDetected -> inaccessible` while preserving staged changes." One event. `prd.md:1157` (PD11) likewise: "`changes_staged` plus detected authorization revocation goes to `inaccessible`".
- **What changed since the prior gate:** the two extra edges are now **pinned** — they are 2 of the 41 in `ConsumerDocsConformanceTests.cs:557` `matrixEdges.Count.ShouldBe(41)` and are present in `docs/diagrams/workspace-lifecycle.md`. Removing them later is now a three-artifact lockstep change. Under the pre-PD11 aggregate gate these pairs rejected with `state_transition_invalid`; they now transition.
- **Assessment:** defensible design (symmetrical with the pre-existing `ready → inaccessible` row at `:390`) but unauthorised scope in a matrix the proposal calls "the exact rules", and it is now locked in without an approval record.
- **Correction.** Add a one-line note under the PD11 block naming the two extra events as an architect-proposed extension routed to the A7 approvers, or restrict the row to rule c and re-pin 39.

### AUTH-10 — MEDIUM — "43 post-SDK members" does not reproduce from any source

- **Location:** `architecture.md:704`: "the authoritative `kind` vocabulary is the full `CanonicalErrorCategory` enum (**43 post-SDK members**) as published in `tests/fixtures/parity-contract.yaml` …"
- **Reproduced from source:** `tests/fixtures/parity-contract.schema.json` `$defs/canonical_error_category` = **50** members; `$defs/mcp_failure_kind` = **49** (48 excluding `none`; **46** excluding `none`, `usage_error`, `credential_missing`, i.e. the pre-SDK classes). Distinct `mcp_failure_kind` values actually used in `tests/fixtures/parity-contract.yaml` = **47**. `HexalithFoldersClient.g.cs:13324` enum = **49**. No reading yields 43.
- **Assessment:** low blast radius (the same sentence says "Assert against the oracle file, not this prose" and "never hard-coded counts") — but a hard-coded count inside the sentence that forbids hard-coded counts is exactly the drift this project reddens on.
- **Correction.** Delete the parenthetical, or state the reproducible figure with its source.

### AUTH-11 — MEDIUM — the validation sections still describe the C6 matrix as "~30 transitions"

- **Location:** `architecture.md:1752` ("Edit E enumerated the 11-state, **~30-transition** matrix") and `:1797` ("11 states × disposition labels × **~30 transitions** × default-rejection rule").
- **Reproduced from source:** 41 positive edges (recomputed with the gate's own `ArchitectureTransitionRow` regex and slice bounds), pinned at `ConsumerDocsConformanceTests.cs:557`; 24-event vocabulary pinned at `:542`.
- **Correction.** "41 transitions" in both places, or drop the count and cite the pinned constant.

### AUTH-12 — LOW — Epic 12 ownership row still says "Stories 12.1–12.5" (prior F10, not fixed)

- **Location:** `architecture.md:287`: "| **Epic 12** | Durable source events, file content/state, restart replay, task completion, real Git persistence (**Stories 12.1–12.5**) |"
- **Contradicting siblings:** `architecture.md:257–264` charters 12.1–**12.6**; `sprint-change-proposal-2026-09-15.md:306` places 12.6 at rank 10; `sprint-status.yaml:238` `12-6-…: in-progress`.
- **Correction.** "Stories 12.1–12.6".

### AUTH-13 — LOW — "12 capability blocks" enumerates 11

- **Location:** `architecture.md:54` ("58 functional requirements across **12 capability blocks**: …") and `:1706` ("FR1–FR58 across **12 capability groups**").
- **Reproduced from source:** the same sentence lists 11 blocks (Capability Contract Terms … Authorized Search Facade). `prd.md` §Functional Requirements carries 12 `###` subsections, of which one is "Glossary and State Vocabulary" — **11 capability blocks**. PRD FR bullets = 58 (counted), matching `NfrTraceabilityConformanceTests`-adjacent FR inventories.
- **Correction.** "11 capability blocks".

### AUTH-14 — LOW — frontmatter readiness date is older than the readiness section it summarises

- **Location:** `architecture.md:41` `implementationReadiness: 'not-ready (2026-07-14/15)'` vs `:1789` "**Overall Status (updated 2026-09-12): NOT READY**".
- **Correction.** Align the frontmatter to `not-ready (updated 2026-09-12)`.

---

## Reproduction table

Every value the architecture states that has a machine-checkable source. "Reproduces" means I recomputed it from the source artifact in this session, not that I compared two prose statements.

| # | Claim (architecture line) | Doc value | Source | Source value | Verdict |
| --- | --- | --- | --- | --- | --- |
| 1 | OQ3 authorization-matrix digest (`:231`) | `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7` | `sha256sum docs/contract/authorization-matrix.md`; `oq3-authorization-evidence.yaml:8` | identical | **reproduces byte-exact** |
| 2 | OQ3 matrix version (`:266`, `:231`) | `1.0.0` | `authorization-matrix.md:5`; `oq3-authorization-evidence.yaml:6` | `1.0.0` | **reproduces** |
| 3 | C12 provider-catalog digest (`:329`) | `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a` | `sha256sum docs/contract/provider-compatibility-catalog.md`; `oq4-provider-compatibility-evidence.yaml:8` | identical | **reproduces byte-exact** |
| 4 | C12 catalog version + approval date (`:329`) | `1.0.0`, 2026-09-15 | `oq4-…-evidence.yaml:6,9` | `1.0.0`, `2026-09-15` | **reproduces** |
| 5 | C7 lock-renewal interval (`:319`) | 30 s | `c7-lock-authorization-timing.md:9,25` | 30 | **reproduces** |
| 6 | C7 authorization-revalidation interval (`:319`, `:118`) | 15 s | `c7-…:10,26` | 15 | **reproduces** |
| 7 | C7 revocation-effect SLO (`:319`, `:118`) | 60 s | `c7-…:11,27` | 60 | **reproduces** |
| 8 | C7 expired-to-stale threshold (`:319`, `:428`) | 60 s | `c7-…:12,28` | 60 | **reproduces** |
| 9 | C7 decision version (`:319`) | `1.0.0` | `c7-…:6` | `1.0.0` | **reproduces** |
| 10 | Protected-operation denominator (`:640`) | 49 | `authorization-matrix.md:15,102`; `ConsumerDocsConformanceTests.cs:568`; `parity-contract.yaml` | 49 | **reproduces** |
| 11 | Protected-operation denominator (`:241`) | **50** ("49 of 50") | same as #10; `authorization-matrix.md:341` says "403 on 49 of 49" | 49 | **MISMATCH — AUTH-2** |
| 12 | S-7 404 category (`:640`) | `authorization` | `authorization-matrix.md:145`; `prd.md:611,1156` | `tenant_access_denied` | **MISMATCH — AUTH-1** |
| 13 | S-7 404 code (`:640`) | `tenant_access_denied` | `authorization-matrix.md:145` | `resource_unavailable` | **MISMATCH — AUTH-1** |
| 14 | S-7 404 client action (`:640`) | `verify_tenant_context_and_authorization` | `authorization-matrix.md:145` | `no_action` | **MISMATCH — AUTH-1** |
| 15 | S-7 503 category (`:640`) | `availability` | `authorization-matrix.md:146` | `read_model_unavailable` | **MISMATCH — AUTH-1** |
| 16 | S-7 503 code (`:640`) | `authority_unavailable` | `authorization-matrix.md:146` | `projection_unavailable` | **MISMATCH — AUTH-1** |
| 17 | S-7 503 client action (`:640`) | `retry_after_backoff` | `authorization-matrix.md:146` | `retry` | **MISMATCH — AUTH-1** |
| 18 | S-7 503 `details.visibility` (`:640`) | `redacted` | `authorization-matrix.md:146`; proposal `:325` | `redacted` | **reproduces** |
| 19 | S-7 retired caller-visible codes (`:640`) | `not_found`, `cross_tenant_access_denied`, `audit_access_denied` | proposal `:322`; `prd.md:611`; `authorization-matrix.md:342` (`G2`) | identical three | **reproduces** |
| 20 | S-7 retires post-authentication 403 (`:640`) | retired | `authorization-matrix.md:150` | "Post-authentication 403 is retired from the canonical design." | **reproduces (F8 closed)** |
| 21 | S-8 operation list (`:641`) | `GetReadinessDiagnostics`, `GetProjectionFreshness` (folder scope); `ListFolderAclEntries` (folder `administer`); `GetEffectivePermissions` (self-inspect + optional task); `GetTaskStatus` (tenant + bound-folder read + task); `ValidateProviderReadiness` (tenant-level, folder-create) | proposal `:326–334` (§7.2 rules 3–7); `prd.md:1156` | identical, all six, same qualifiers | **reproduces (6/6)** |
| 22 | S-8 gap correspondence | — | `authorization-matrix.md:345 (G5), :348 (G8), :349 (G9), :350 (G10)` | every S-8 correction maps to a recorded gap | **consistent** |
| 23 | C6 positive transition edges (`:380–418`) | 41 (implicit) | recomputed with `ArchitectureTransitionRow` regex + gate slice bounds; `ConsumerDocsConformanceTests.cs:557` | 41 | **reproduces** |
| 24 | C6 diagram/matrix edge equality | — | `docs/diagrams/workspace-lifecycle.md` | 41 diagram edges; symmetric difference with matrix = ∅ | **reproduces** |
| 25 | C6 event vocabulary (`:428`, mapping doc) | 24 (incl. `LockLeaseBecameStale`) | `c6-transition-matrix-mapping.md:36`; `ConsumerDocsConformanceTests.cs:542` | 24 | **reproduces** |
| 26 | C6 state catalog (`:360`) | 11 states | `c6-transition-matrix-mapping.md:19–29`; `ConsumerDocsConformanceTests.cs:528` | 11 | **reproduces** |
| 27 | Operator-disposition vocabulary (`:106`, `:376`, `:720`) | 5: `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention` | `workspace-lifecycle.md` disposition table; `FolderOperatorDisposition` | 5, same names | **reproduces** |
| 28 | Lock-state vocabulary (`:106`, `:376`) | 5: `unlocked`, `locked`, `expired`, `stale`, `revoked` | `c7-lock-authorization-timing.md:28` boundary semantics | consistent | **reproduces** |
| 29 | `unknown_provider_outcome` disposition (`:373`, `:426`) | `auto-recovering` | `c6-transition-matrix-mapping.md:29`; `workspace-lifecycle.md:26,41` | `awaiting-human` | **MISMATCH — AUTH-4** |
| 30 | `dirty` disposition predicate (`:369`) | conditional (`degraded-but-serving` / `awaiting-human`) | `c6-transition-matrix-mapping.md:24`; `workspace-lifecycle.md:22` | identical wording | **reproduces (F7 doc half closed)** |
| 31 | C6 transitions "~30" (`:1752`, `:1797`) | ~30 | as #23 | 41 | **MISMATCH — AUTH-11** |
| 32 | NFR total (`:58`, `:278`) | 84 | `prd.md` §NFR bullet count = 84; `nfr-traceability.md` rows = 84; `NfrTraceabilityConformanceTests.cs:25` `NfrTotal = 84` | 84 | **reproduces** |
| 33 | NFR category count (`:58`) | 11 | `nfr-traceability.md` distinct categories = 11; `prd.md` §NFR `###` headings = 11; `run-nfr-traceability-gates.ps1` `category_total` | 11 | **reproduces** |
| 34 | New category names (`:58`, `:272`) | "Edge Security & Deployment Hardening", "Durable Operation & Release Evidence" | `NfrTraceabilityConformanceTests.cs:67–68` | identical strings | **reproduces byte-exact** |
| 35 | New category ranges (`:58`) | NFR74–NFR78, NFR79–NFR84 | `NfrTraceabilityConformanceTests.cs:67–68`; `prd.md:1066,1076` | `(74,78)`, `(79,84)` | **reproduces** |
| 36 | NFR category enumeration in validation (`:1708`) | 9 categories | as #33 | 11 | **MISMATCH — AUTH-5** |
| 37 | NFR74 owner (`:274`) | `13-2` | `nfr-traceability.md:120` | `13-2` | **reproduces** (but see AUTH-6) |
| 38 | NFR75 owner (`:274`) | `13-1` | `nfr-traceability.md:121` | `13-1` | **reproduces** |
| 39 | NFR76 owner (`:274`) | `13-2` | `nfr-traceability.md:122` | `13-2` | **reproduces** |
| 40 | NFR77 owner (`:274`) | `13-3` | `nfr-traceability.md:123` | `13-3` | **reproduces** |
| 41 | NFR78 owner (`:274`) | `13-6` | `nfr-traceability.md:124` | `13-6` | **reproduces** |
| 42 | **NFR79 owner (`:274`)** | **`12-1`** | `nfr-traceability.md:125` | `12-1` | **reproduces — prior Epic 13 mis-assignment stayed fixed** |
| 43 | **NFR80 owner (`:274`)** | **`12-2`** | `nfr-traceability.md:126` | `12-2` | **reproduces — stayed fixed** |
| 44 | NFR81 owner (`:274`) | `13-4` | `nfr-traceability.md:127` | `13-4` | **reproduces** |
| 45 | NFR82 owner (`:274`) | `13-5` | `nfr-traceability.md:128` | `13-5` | **reproduces** |
| 46 | **NFR83 owner (`:274`)** | **`7-16`** | `nfr-traceability.md:129` | `7-16` | **reproduces — stayed fixed** |
| 47 | NFR84 owner (`:274`) | `13-6` | `nfr-traceability.md:130` | `13-6` | **reproduces** |
| 48 | All eleven rows `reference-pending` + release-blocking (`:274`) | yes | `nfr-traceability.md:120–130` | all eleven | **reproduces** |
| 49 | "every NFR has ≥1 gate" (`:1718`) | yes | `nfr-traceability.md:120–130` Automated-gates column | `—` for all eleven | **MISMATCH — AUTH-5** |
| 50 | FR denominator (`:54`, `:1706`) | 58 | `prd.md` §FR bullet count | 58 | **reproduces** |
| 51 | FR capability blocks (`:54`, `:1706`) | 12 | `prd.md` §FR capability `###` sections (excl. Glossary); architecture's own list | 11 | **MISMATCH — AUTH-13** |
| 52 | Execution-wave rank 0 (`:218`) | approve proposal; OQ1–OQ4; PD8/PD10/PD11; reapprove C3/C6/C9/OQ3 | proposal `:305` | identical | **reproduces (punctuation only)** |
| 53 | Rank 10 (`:219`) | 12.1 first; 12.2/12.3 follow 12.1; 12.6 after 12.1, not closing before OQ8 | proposal `:306` | identical | **reproduces** |
| 54 | Rank 20 (`:220`) | 12.4 ← 12.1–12.3 + 3.11 + 3.13; 12.5 ← 12.1–12.3 + 10.6 | proposal `:307` | identical | **reproduces** |
| 55 | Rank 30 (`:221`) | 8 story rows, all prerequisite edges | proposal `:308` | identical, 0 dropped / 0 added / 0 altered | **reproduces** |
| 56 | Rank 40 (`:222`) | OQ5/OQ6/OQ7/OQ8/OQ9/OQ11/OQ12/OQ13 edges | proposal `:309` | identical, 7 clauses | **reproduces** |
| 57 | Rank 50 (`:223`) | OQ10 last, then rerun readiness | proposal `:310` | identical | **reproduces** |
| 58 | Wave-table story IDs exist | 3.11, 3.13, 4.18–4.21, 6.12–6.14, 10.6, 10.7, 11.15, 12.1–12.6 | `sprint-status.yaml:99,101,123–126,155–157,202,203,223,233–238` | all present | **reproduces** |
| 59 | Strictly-lower-rank rule unsatisfiable (`:214`) | 6 equal-rank prerequisites; 3.11/3.13/10.6/10.7/11.15 and all Epic 13 unranked | proposal `:306–309,316` | confirmed by inspection | **reproduces — correctly flagged open** |
| 60 | Epic 12 story list (`:257–264`) | 12.1–12.6 | `sprint-status.yaml:233–238`; `epics.md:595` | 12.1–12.6 | **reproduces** |
| 61 | Epic 12 ownership row (`:287`) | 12.1–12.5 | as #60 | 12.1–12.6 | **MISMATCH — AUTH-12** |
| 62 | Epic 13 story list (`:270`) | 13.1–13.6 "ratified in sprint-status.yaml" | `sprint-status.yaml:242–247`; `epics.md:2718–2795` | six stories | **reproduces** |
| 63 | Epic 13 defect-ID scope (`:270`) | includes HXF-SEC-001 | `epics.md:2718–2795` ACs | no story covers HTTPS-or-loopback bearer transport | **MISMATCH — AUTH-6** |
| 64 | OQ2 file-policy version (`:266`) | `1.1.0` | `prd.md:1089`-region OQ2 record | `1.1.0` | **reproduces** |
| 65 | OQ4 catalog version (`:266`) | `1.0.0` | `oq4-…-evidence.yaml:6` | `1.0.0` | **reproduces** |
| 66 | OQ3 gap IDs (`:266`, `:645`) | `G1`–`G11` | `oq3-authorization-evidence.yaml` `recorded_gap_ids`; `authorization-matrix.md:341–351` | G1–G11 | **reproduces** |
| 67 | OQ4 ceiling/gap IDs (`:266`) | `CC1`–`CC12`, `PG1`–`PG3` | `provider-compatibility-catalog.md` | consistent | **reproduces** |
| 68 | D-7 commit TTL (`:625`, `:1698`) | `P7Y` / "commit=C3" | `c3-retention.md:27,51` | "Same as audit metadata: 7 years" | **reproduces** |
| 69 | C3 window (`:430`) | 7 days, "superseded trigger" | `c3-retention.md:22,46` ("7 days after terminal workspace state"; trigger "commit, failure, cancellation, or lock expiry") | window matches; trigger superseded, as the doc says | **reproduces + honestly flagged** |
| 70 | `CanonicalErrorCategory` "43 post-SDK members" (`:704`) | 43 | `parity-contract.schema.json` `$defs` (50 categories / 49 kinds); generated enum 49; 46 post-SDK; 47 used | none = 43 | **MISMATCH — AUTH-10** |
| 71 | CLI exit-code table size (`:688–702`) | 15 rows | `ConsumerDocsConformanceTests.cs:102` `codes.Count.ShouldBe(15)` | 15 | **reproduces** |
| 72 | Proposal digest (cited by UX/PRD, not architecture) | `5d12ae4d…4104` | `sha256sum sprint-change-proposal-2026-09-15.md` | identical | **reproduces byte-exact** |
| 73 | Architecture digest recorded in UX provenance | `f7b5a8d2a503bdee37f9833724a8896289930f1d8ecfc16453efa60b139900b1` | `sha256sum architecture.md` (working tree == `HEAD:`) | identical | **reproduces byte-exact** |
| 74 | PRD digest recorded in UX provenance | `0d6f1ab858e5774a87e0a639ceb521b0fc7e57a93f48297bf69ec53fd5788158` | `sha256sum prd.md` (working tree == `HEAD:`) | identical | **reproduces byte-exact** |
| 75 | "Current reality: `FolderStateTransitions.cs` rejects four of five added transitions; three operator events accepted; no `LockLeaseBecameStale`" (`:240`) | as stated | `FolderStateTransitions.cs:38–44,84–120,157` | operator events accepted; `Dirty => AwaitingHuman`; no `LockLeaseBecameStale`; `ChangesStaged+CommitFailed` single-outcome | **reproduces — honest** |
| 76 | "`visibility` exists only as `details.visibility=\"metadata_only\"`" (`:241`) | as stated | spine + `FolderProblemDetailsFactory.cs` / `AuditEndpoints.cs:461–462` | the three codes are live and emitted | **reproduces — honest** |
| 77 | Manifest `generated_on` (`:206`, `:243`) | `2026-08-04`, regeneration owed | `planning-story-manifest.yaml` | `2026-08-04`; no `execution_rank`/`execution_waves`/`story_lifecycle_status`/OQ11–OQ13 | **reproduces — honest (F4b closed)** |
| 78 | Governance YAML keeps historical C3/C6/C9 values (`:329`) | as stated | `c0-c13-governance-evidence.yaml:87,137,183` | C3/C6/C9 all `status: approved` | **reproduces — honest, tracked gap** |

## Lockstep status per sibling artifact

| Sibling | Owed by | State vs `architecture.md` | Verdict |
| --- | --- | --- | --- |
| `sprint-change-proposal-2026-09-15.md` (§5.2, §7.1, §7.2, §7.3, PD1–PD11) | authority | 6/6 §5.2 edits landed; §7.1 6/6 ranks and all edges; §7.2 10/10 rules homed; §7.3 8/8 rules homed | **CONFORMANT**, except AUTH-1 (S-7 over-specification beyond rule 1), AUTH-3 (PD5), AUTH-9 (rule c) |
| `prd.md` | done | FR58/NFR84 inventories, PD8/PD10/PD11 target-state framing, OQ11–OQ13, C3/C6 supersession all agree | **IN LOCKSTEP**, except AUTH-1 (`prd.md:611` denial vocabulary), AUTH-2 (`:1156` 49 ops), AUTH-3 (`:1154` PD5) |
| `epics.md` | §5.4 partially owed | NFR74–NFR84 mirrored verbatim (`:242–252`); **still owed:** 84-count declaration, `execution_rank`/prerequisites (0 occurrences), Story 10.9 retitle (`:2327` unchanged), §7.3 lifecycle ACs, a story owning the PD10 correction (0 references to PD8/PD10/PD11 — the architecture's claim at `:276` reproduces exactly) | **PARTIAL — correctly recorded as owed**; AUTH-3, AUTH-6 |
| `sprint-status.yaml` | §5.6 owed | `10-8-…: done` (canonical `in-progress`), `10-9-…: review` (canonical `done` after retitle), `last_updated: 2026-09-12`, freeze retained | **OWED — architecture correctly disclaims story-status authority (`:208`)** |
| `planning-story-manifest.yaml` | §5.5 owed | `generated_on: 2026-08-04`; none of the §5.5 fields | **OWED — honestly stated (`:206`, `:210`, `:243`)** |
| `ux-design-specification.md` | §5.3 **now done** (2026-09-16, `de281e7`) | UX-DR33/34/35/36 align with S-6 `withheld`, S-7 two envelopes, PD11 `auto-recovering`; provenance digests for proposal/PRD/architecture all reproduce byte-exact | **IN LOCKSTEP** (its `upstreamLockstepNote` "uncommitted working tree" caveat is now stale-but-conservative; the digests are valid at `HEAD`) |
| `docs/exit-criteria/nfr-traceability.md` | done | 84 rows / 11 categories; all eleven new rows `reference-pending`; NFR60/`C3` untouched; ownership reproduces cell-for-cell against `architecture.md:274` | **IN LOCKSTEP**; AUTH-5 and AUTH-8 are architecture-side sweeps |
| `docs/exit-criteria/c0-c13-governance-evidence.yaml` | §5.8 owed | C3/C6/C9 still `status: approved`; no `superseded-pending-reapproval` vocabulary | **OWED — architecture is honest about it (`:329`) and names the exact lockstep (YAML + schema + `GovernanceCompletenessGateTests` + `GovernanceEvidenceReferencePendingCriteriaStaySurfaced`)**; AUTH-8 is the stale mirror sentence |
| `docs/exit-criteria/c6-transition-matrix-mapping.md` | §5.8 **partially done** (`de281e7`) | `dirty` disposition + 24-event vocabulary landed; `unknown_provider_outcome` still `awaiting-human` | **PARTIAL — AUTH-4** |
| `docs/diagrams/workspace-lifecycle.md` | §5.8 **done** (`de281e7`) | 41 edges, 24 events, `dirty` predicate; exact set equality with the architecture matrix | **IN LOCKSTEP** for edges/events; `unknown_provider_outcome` disposition — AUTH-4 |
| `docs/exit-criteria/c3-retention.md` | §5.8 owed (A7b) | still elapsed-lock-time trigger | **OWED — architecture says so explicitly (`:430`) and adds the unreachable-window conflict (`:432`)** |
| `docs/contract/authorization-matrix.md` + spine + generated client + `previous-spine.yaml` + C13 inventory | §5.8 owed (A6/A6b) | matrix unchanged at `1.0.0`; spine still carries 403 and the three codes; `previous-spine.yaml` `known_omissions` still blind to status codes | **OWED — routed at `architecture.md:645–651`**; AUTH-1 is the vocabulary the regeneration must not consume as written |
| `tests/…/ConsumerDocsConformanceTests.cs` | done | 41/24 pins landed in the same commit | **IN LOCKSTEP — 314/314 green** |
| `tests/…/NfrTraceabilityConformanceTests.cs` + `run-nfr-traceability-gates.ps1` | done | `NfrTotal = 84`, two new bands, eleven release-blocking gap rows | **IN LOCKSTEP** |
| `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` + lifecycle tests + `DispositionLabelMapper.cs` | §5.8 owed | pre-PD11 model | **OWED — honestly declared target state (`:240`, `:427`)**; the mapper/`unknown_provider_outcome` half is undisclosed (AUTH-4) |
| `_bmad-output/gates/consumer-docs/latest.json` | — | `"status": "passed"` and genuinely green | **HONEST (F1 closed)** |
| `_bmad-output/gates/nfr-traceability/latest.json` | — | `"status": "failed"`, `source_commit: 1621358`, `nfr_total: 84` | **stale + environmental** (the runner's `dotnet test --filter` VSTest path is unsupported on the pinned SDK); every NFR traceability fact passes in-process. Do not attribute this red to the 84-row relock |

## Open items the architecture records — still genuinely open, and routed

| Open item | Location | Owner named | Still open? |
| --- | --- | --- | --- |
| Rank rule unsatisfiable by its own wave table | `:214` | Delivery + PM | **yes** — manifest has no `execution_rank`; `epics.md` has none either |
| PD8 tokenization vs the operational path (lock identity, Git executor, NFR79 restart) | `:643` | Security + Architecture, in the C9 relock | **yes** — no tokenizer exists (`:242`) |
| C3 staged-content window unreachable | `:432` | Legal + Product + Security, with A7b | **yes** — `c3-retention.md` unchanged |
| A-11 migration decision for a breaking wire change (`v2` vs in-place `v1` exception) | `:651` | (implicitly Architecture + PM; **the line names no owner**) | **yes** — and it is the one open item without an explicit routing tag. Minor: add "route to Architecture + PM", as `:214`/`:276`/`:432`/`:643` do |
| No story owns the PD10 spine correction | `:276` | PM + Delivery | **yes** — `epics.md` has 0 references to PD8/PD10/PD11 (reproduced) |

## Supersession bookkeeping

The architecture claims OQ3 matrix `1.0.0`, C3, C6-transitions and C9 are superseded/approval-pending. That claim is stated **consistently** in all four places it appears — `:227–234` (overlay table), `:315`/`:318`/`:321` (exit-criteria table), `:329` (status-reconciliation blockquote), `:342`/`:345`/`:348` (operations plan) — with one exception: `:331` (AUTH-8) still reads "C3, C4, and C7 are approved".

On the governance YAML carrying the old values: **the document is honest.** `:329` states the YAML cannot express supersession today, names both blocking tests and the exact reason each blocks, prescribes a `superseded-pending-reapproval` status as one-commit YAML+schema+test work, and says "Until that lands, this document is the record of supersession and the YAML keeps its historical values." It also states "No NFR row changes status as a result, and the `{C3, C4, C7, C12}` reference-pending hard-pin is untouched" — verified: `nfr-traceability.md` NFR60/`C3` unchanged, all NFR74–NFR84 entered as `reference-pending`, and both `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` and `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` pass. **This is a tracked gap, not a defect.**

## Recommended disposition

1. **Fix before the PD10 spine regeneration consumes S-7:** AUTH-1. This is the one finding that can propagate a wrong wire value into the artifact A6b must reapprove.
2. **Fix by sweep (no lockstep cost):** AUTH-2, AUTH-3, AUTH-5, AUTH-7, AUTH-8, AUTH-10, AUTH-11, AUTH-12, AUTH-13, AUTH-14. All are single-sentence edits inside `architecture.md`.
3. **Route to owners:** AUTH-4 (C6 lockstep for `unknown_provider_outcome` — Architecture, with the `DispositionLabelMapper` story), AUTH-6 (NFR74 has no acceptance criterion — PM + Delivery, alongside the PD10-ownership gap already flagged at `:276`), AUTH-9 (two extra revocation events — A7 approvers).
4. **Do not touch:** the `{C3, C4, C7, C12}` hard-pin, any NFR row status, the 41/24 C6 pins, or the governance YAML statuses. The amendment gets all of these right.

## Verification commands used

```
dotnet run --project tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj
  -> Total: 314, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0
sha256sum docs/contract/authorization-matrix.md docs/contract/provider-compatibility-catalog.md
sha256sum _bmad-output/planning-artifacts/{sprint-change-proposal-2026-09-15.md,prd.md,architecture.md}
git show HEAD:_bmad-output/planning-artifacts/{prd.md,architecture.md} | sha256sum   # working tree == HEAD
# C6 recomputed with the gate's own ArchitectureTransitionRow / PascalEventToken / C6StateCatalogRow
# regexes and slice bounds: 41 matrix edges, 41 diagram edges, symmetric difference empty;
# 24 events both sides; 11 states.
# NFR inventory recomputed: prd.md 84 bullets / 11 category headings; nfr-traceability.md 84 rows /
# 11 distinct categories; NfrTraceabilityConformanceTests.NfrTotal = 84.
# Spine denominator recomputed: 49 in matrix, oq3 evidence, parity-contract.yaml, and three
# ConsumerDocsConformanceTests pins; zero sources for 50.
# Canonical vocabularies read from parity-contract.schema.json $defs and HexalithFoldersClient.g.cs:13324.
```
