# Validation Report — Hexalith.Folders

- **PRD:** `_bmad-output/planning-artifacts/prd.md`
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-09-08T15:32:48+02:00
- **Grade:** Poor (as reviewed; all critical and high findings fixed after review, post-triage grade unassessed)
- **Reviewers:** rubric walker, adversarial-general, post-finalization drift (ad-hoc)
- **Pass:** second pass of 2026-09-08 (first pass: Poor, 63 findings, all critical/high fixed)
- **Raw findings:** rubric 18 (0C/3H/3M/12L), adversarial 20 (2C/4H/8M/6L), drift 11 (0C/2H/4M/5L) = 49 → 41 after merging the 8 duplicates flagged by more than one reviewer (2 critical, 4 high, 14 medium, 21 low)

## Overall verdict

The PRD is now a usable chain-top product contract whose authority conflicts are recorded rather than hidden: the permission model is stated in the Spine's own `read|write|administer` terms, FR44 is the one error enumeration and names every outcome the rest of the document relies on, the release inventory admits its own incompleteness, and PD10 lists the Spine defects by name. What is at risk has moved from authority to internal consistency of the state model written this morning: the `dirty` definition contradicts the completion table's own third row, A18 promises an `inaccessible` workspace keeps its changes while the Glossary makes `inaccessible` task-terminal and cleanup-eligible, and the new actor × family table does not let UJ1's developer run UJ1. All three are in-place fixes, and all three would otherwise be resolved silently by whoever writes the aggregate story first.

The adversarial reviewer's charge is that the triage aligned the PRD's wording to artifacts it did not read to the end: the completion rows cite "the approved C6 matrix", but that matrix (architecture, mapping, and the 1:1 `FolderStateTransitions.cs`) contains no transition by which a `dirty` workspace is ever re-locked, continued, committed, or released by the originating task, sends a known commit failure to `failed`, and returns `inaccessible` to `ready` — so UJ3's "only MVP exit" is `state_transition_invalid` under the artifact the PRD calls approved, and the C6 row admitted only two of six disagreements. Its second critical: 46 of the 49 Spine operations declare both a 403 and a 404 safe denial with the same category and code, exactly the per-operation status difference the restated invariant forbids, and PD10 did not record it; the safe-denial category was unspelled and `tenant_access_denied` was double-mapped. It also found that folder `read` hands every audit reviewer and operator file-body reads, that a zero-mutation lease lapse became a permanently uncleanable, metric-visible `dirty` workspace, and that task-terminal cleanup discards staged work while the document still says "No MVP operation discards staged changes". The drift reviewer independently confirmed the two gate-level conflicts (four completion-table transitions absent from the approved matrix and the coded aggregate; the dual 403/404 envelopes) and added that PD10 undercounted the leaking-category operations (24, not ten) and that the new families table disagreed with the Spine's per-operation requirement tokens on `CreateRepositoryBackedFolder` and `GetEffectivePermissions`. Programmatic checks confirmed the 58 FR bullets and 73 NFR bullets in `epics.md` byte-identical, every cited Spine identifier present, and every "(to be created)" path absent.

After these three reviews were written, a final in-place pass fixed every critical and high finding of this pass and twelve of the fourteen mediums (C6 disagreement enumerated in the C6 row and owned by new PD11; safe-denial family merged with the Spine's `tenant_access_denied`/`resource_unavailable` envelope with the 403/404 split and corrected 24/6/4 counts in PD10; `dirty` covers the zero-mutation case and a clean `dirty` returns to `ready` at `stale` (A21); task-terminal cleanup discarding staged work stated openly in UJ3 and PD2 with A18 bounded to the seven-day window; context read requires `write` (A22); UJ1 bootstrap via folder-create readiness, creator `administer`, and tenant-admin-implies-`administer` (A23); "verb" replaced by operation family; and the remaining mediums as noted per finding). These fixes were not re-verified by a further review round, so the post-triage grade is unassessed; the grade above is computed from the reviews as written (two adversarial criticals → Poor). The NFR conformance gate (`NfrTraceabilityConformanceTests`, 16 tests) passes against the final text and the 58 FR bullets remain byte-identical in `epics.md`.

## Dimension verdicts
- Decision-readiness — adequate
- Substance over theater — adequate
- Strategic coherence — adequate
- Done-ness clarity — adequate
- Scope honesty — strong
- Downstream usability — adequate
- Shape fit — adequate

## Findings by severity

### Critical (2)

**[Adversarial; Drift (high)]** — The approved C6 matrix cannot represent the PRD's only recovery path, and the C6 row admits two of six disagreements (§Task and Lock Completion Model rows 3, 5, 6, 8, 10; C6 row; UJ3; A18; `FolderStateTransitions.cs`)
The translated matrix contains, from `dirty`, exactly two transitions (`ReconciliationRequested → reconciliation_required`, `OperatorDiscardRequested → failed`). There is no `dirty + WorkspaceLocked`, `dirty + FileMutated`, `dirty + CommitSucceeded`, or `dirty + WorkspaceLockReleased`, so row 3's re-acquire/release, row 5's re-acquire/commit, and row 8's "retry with a new key" are all rejected pairs. `changes_staged + CommitFailed → failed`, not row 8's `dirty`/`locked`. `inaccessible + ProviderReadinessValidated → ready`, not A18's "returns to `dirty` with its changes intact". `changes_staged + AuthRevocationDetected` and `locked + ProviderOutcomeUnknown` are unlisted; `committed + WorkspaceLockReleased → ready` exists while row 7 says commit already leaves the lock `unlocked`. The governance record carries C6 as `approved` over an artifact that contradicts five completion rows, and the PRD's C6 row certifies the alignment. The drift reviewer confirmed the same four transition families are absent from the architecture matrix (L297–L347) and the coded aggregate (L60–L115) while the C6 status cell names only two disagreements.
Fix: Either the PRD stops citing C6 as approved for anything but the two vocabularies and adds a C6 row conflict list (missing `dirty` recovery transitions, `CommitFailed` target, `inaccessible` restoration target, missing `changes_staged`/`locked` revocation and unknown-outcome rows, spurious `committed` release) as a PD10-style item with an owner and a date, or the completion model is rewritten to the matrix as approved (no MVP resumption; PD2 answered "(b) with no exit"). Do not leave both texts labelled approved. If the PRD rows stand, amend architecture matrix, mapping, `FolderStateTransitions.cs`, and aggregate tests in one lockstep change and reopen the C6 governance row.
Outcome: fixed after review — the C6 matrix disagreement is now enumerated in full in the C6 row and owned by new PD11.

**[Adversarial; Drift (high)]** — The restated safe-denial invariant is violated by 46 of 49 Spine operations, the defect register does not say so, and the one Spine category that carries the invariant is listed in FR44 as a second family (§Error Codes; FR44; PD10; §MVP Contract Summary exception list; Spine `SafeAuthorizationDenial403`/`404`)
`SafeAuthorizationDenial403` and `SafeAuthorizationDenial404` are declared together on 46 operations, `AddFile` through `ValidateProviderReadiness`; both examples carry `category: tenant_access_denied`, `code: resource_unavailable`, and differ only in `status: 403` versus `404`. That is precisely the "status" difference the invariant forbids per operation, and it discloses "you exist but are denied" versus "this may not exist" on every protected surface. PD10 does not record the dual-status declaration, which is the widest conformance defect in the Spine. The safe-denial category is still not spelled in the exception list ("the `SafeAuthorizationDenial` envelopes" are response names, not a category). The category the Spine actually uses is `tenant_access_denied`, which FR44 lists as its own family "tenant authorization denied" distinct from "safe denial"; since every negative case fails closed with the safe denial, there is no residual population for a distinguishable "tenant authorization denied" — the family is either dead or a leak, and "every Spine category maps to exactly one family" cannot assign it to both. The drift reviewer adds that the Spine assigns "cross-tenant" to the 404 envelope while "tenant/folder/binding/policy" cases sit under 403, and the ranged-read 416/404 routing (L3258) is an admitted open item.
Fix: Spell the safe-denial category and code in the exception list; state per operation whether the safe denial is 403 or 404 (one rule) and add "dual 403/404 safe denials on 46 operations" to PD10 with the same owner and date; delete "tenant authorization denied" from FR44 or define the one caller class that receives it distinguishably and why that is not enumeration.
Outcome: fixed after review — the safe-denial family is merged with the Spine's `tenant_access_denied`/`resource_unavailable` envelope, and the 403/404 split with the corrected 24/6/4 leaking-category counts is recorded in PD10.

### High (4)

**[Done-ness clarity; Adversarial]** — `dirty` is defined as "mutations are present" but the completion model and the approved C6 matrix assign `dirty` to a zero-mutation lease lapse (§Workspace State and Concurrency; Task and Lock Completion Model row 3; architecture.md C6; FR13; FR30; CM2; PD2 scope; UJ3)
An engineer implementing the definition and a tester working from the table disagree on the first edge case. A task that acquires a lock, writes nothing, and dies leaves a `dirty` workspace that only it can release; because it will never return, the folder can never satisfy FR13's archive precondition, the working copy is never cleaned (FR30 excludes `dirty`), PD2 does not cover it, and CM2 counts it as recovery burden even though nothing needs recovering. The previous review's "stale lock on a clean workspace blocks archive" was fixed by making it worse. Separately, `changes_staged` depends on "no interruption", which the server cannot observe while the lease is valid; UJ3 asserts the interrupted workspace is `dirty` while its lock is still `locked`, which the definitions make impossible. After a row 5/6 re-acquisition the workspace holds a valid lock but stays `dirty`; whether the next mutation moves it to `changes_staged`, and whether FR37 accepts `dirty`+`locked`, is unstated.
Fix: Redefine `dirty` as "the lock lapsed, was revoked, a commit failed, or reconciliation confirmed a partial set, whether or not mutations are present"; either extend PD2 to the empty-staged-set case or add a product rule that a `dirty` workspace with no staged changes returns to `ready`/`unlocked` at the C7 `stale` threshold and is excluded from CM2. Delete "no interruption" from the `changes_staged` definition; add one row "Originating task re-acquires in `dirty` → `changes_staged`/`locked`".
Outcome: fixed after review — the `dirty` definition now covers the zero-mutation lapse, and a clean `dirty` workspace returns to `ready` at the `stale` threshold (A21).

**[Done-ness clarity; Adversarial]** — A18's "changes intact" promise is contradicted by `failed` and `inaccessible` being task-terminal and cleanup-eligible, so the no-discard rule is broken by FR30 (Task and Lock Completion Model rows 6 and 10; A18; Glossary "Task-terminal"; FR30; UJ3; PD2(b); C6 matrix `changes_staged + CommitFailed → failed`)
After the seven-day window the working files are deleted while the lifecycle stays `inaccessible`, so a restoration on day eight yields `dirty` with no changes, or (per A10) content-unavailable; the cleanup story and the revocation story will encode opposite rules. Under the approved matrix every known commit failure is `failed`; `failed` is task-terminal, so seven days later FR30 deletes the working copy holding Asha's staged changes — a discard, performed by the platform, of exactly the work UJ3 promises to preserve, and UJ5's credential-lost-push-permission 403 is the likeliest producer. No rule sorts a provider failure into row 8 (retryable, work kept), row 10 (non-retryable, work deleted after seven days), or row 6 (authorization, resumable); the same provider 403 fits all three. `failed` and `inaccessible` are also absent from the context-read rule, so no actor, not even the owner, may inspect the staged changes before cleanup deletes them.
Fix: Either `failed`/`inaccessible` with staged changes are not cleanup-eligible (add to FR30's exclusion list and FR13's precondition; make `inaccessible` non-task-terminal while A18 stands), or the completion model states that a non-retryable commit failure discards after seven days and UJ3/PD2 drop "no discard"; bound A18 to the observation window. Define retryable/non-retryable/authorization classification. Add `failed` and `inaccessible` to the owner-readable set or say why not.
Outcome: fixed after review — UJ3 and PD2 now state openly that task-terminal cleanup discards staged work, and A18 is bounded to the seven-day window.

**[Done-ness clarity; Adversarial]** — The actor × family table cannot run UJ1 and has no bootstrap for folder-level `administer`; the Spine disagrees with the table on `CreateRepositoryBackedFolder` and `GetEffectivePermissions` (§Protected operation families; UJ1; Actors table "Tenant member … FR11, FR18"; FR4; FR5/FR13; Spine requirement tokens)
Nadia creates a logical folder with a tenant permission; nothing says the creator receives any grant on it. To create the repository-backed folder she needs `administer`; to prepare, lock, and mutate she needs `write`, which `administer` does not imply. Both grants come only from a tenant administrator (FR4), who appears nowhere in UJ1, so the journey's value moment is unreachable without two ACL updates the journey omits. "Readiness and provider evidence" is granted to "Tenant administrator authority or operator permission" only, yet UJ1 has a tenant member run it. A tenant administrator without an explicit `administer` grant has no path to FR5/FR13 on any folder. The Spine authorizes `CreateRepositoryBackedFolder` on "existing-folder-access", not admin, and `GetEffectivePermissions` on "folder-permission-read", not `administer`; FR6 is useless to an agent if it requires `administer`, and the "Spine grant today" column is wrong on its own terms for those two operations.
Fix: Add product rules: "`CreateFolder` grants the creating principal `administer` on the new folder (first ACL entry, audited)" and "tenant-administrator authority implies `administer` on every folder in the tenant"; add "tenant member with the folder-create permission" to the readiness family's grant column; move `GetEffectivePermissions` (self) to the Status family at `read`; correct the "Spine grant today" cells or add them to PD10.
Outcome: fixed after review — readiness is open to folder-create permission holders, the creator receives `administer`, and tenant-administrator authority implies `administer` (A23); the Spine token mismatch is closed by the families-versus-tokens fix (FR6 moved to the read family, tokens mapped in OQ3).

**[Adversarial]** — The three-level grant model gives audit reviewers and operators file-body reads and makes "administer implies nothing" a one-call speed bump (§Protected operation families — Context read, Audit read, Console view, Incident evidence rows; UJ8; FR54; §Public Surfaces)
Every actor who may read audit, view the console, or open the incident view must hold folder `read`, and folder `read` is the grant for `ReadFileRange`, `SearchFolderFiles`, and `GlobFolderFiles` on any `ready`, `locked`, or `committed` workspace. The metadata-only posture of UJ8/FR54/FR56 is therefore a property of those operations, not of the reviewer's access: Priya can read every committed file body with the same grant that lets her read audit, and with no explicit deny entries there is no way to grant audit-read without body-read. A principal with `administer` may call `UpdateFolderAclEntry` on the same folder; unless self-grant is forbidden (unstated), A12/A17 is a formality. "Incident evidence" is a Tenant-scope family that "also requires folder `read`", contradicting "Tenant-level families … have no folder ACL entry" two lines earlier.
Fix: Either split `read` into `read.metadata` and `read.content` and key audit/console/incident/status to `read.metadata`, or state that audit-reviewer and operator roles do not require a folder grant and are evaluated as tenant-level roles scoped by folder identity. Forbid self-modification of one's own ACL entry by `administer` holders or admit `administer` is effectively all-powerful. Move "Incident evidence" to Folder scope or drop the folder-`read` requirement.
Outcome: fixed after review — context read (file bodies) now requires `write` (A22).

### Medium (14)

**[Decision-readiness]** — C6 disposition conflict is assigned to a PD item whose options do not cover it (§Deferred Quantitative Targets C6 row; PD2 options (a)/(b))
The C6 row says "PD2 must close in one direction", but PD2's options decide only the discard path; closing PD2 either way will not re-approve C6 or amend §Workspace State and Concurrency's "unknown-provider-outcome is auto-reconciling during its bounded evidence-check budget". The verified C6 mapping (State Catalog `unknown_provider_outcome` → `awaiting-human`, approved 2026-05-11) confirms the conflict is real.
Fix: Either add a third clause to PD2 or open PD11 for the disposition alone with Architecture as owner.
Outcome: fixed after review — the C6 disposition conflict is now owned by new PD11.

**[Done-ness clarity]** — Workspace-to-task cardinality after release is undefined (Task and Lock Completion Model row 2; Task identity "one task identity binds to at most one workspace"; Glossary "Workspace")
The PRD bounds one direction only; whether `LockWorkspace` by a second task on a `ready` workspace prepared by the first is a lock-conflict, a safe denial, or allowed decides the shape of the lock aggregate and the meaning of "prepared by the caller's task".
Fix: Add one sentence: "A workspace belongs to the task that prepared it for its lifetime; a different task must prepare its own workspace, and `LockWorkspace` by any other task on it is denied with the lock-conflict result even when `unlocked`."
Outcome: fixed after review — a workspace now belongs to its preparing task.

**[Downstream usability; Adversarial]** — "Verb" survives as an undefined ghost term distinct from "operation family" in the authorization FRs (FR5; FR8 "one evaluated verb per protected operation family"; FR10 "actor, tenant, verb, operation family, result"; Actors table and §Authentication "delegated verb/task scope")
The triage replaced verbs with families and levels but left five normative uses of "verb". FR10's audit record and FR8's OQ3 conformance clause each name a field the Glossary does not define, after the identifiers that gave it meaning were removed; FR5 defines "verb scope" as a derived set, which makes FR8's "one evaluated verb per family" a count of something with no identifier. FR8's "one" also collides with the dual requirements in the table (`read` plus audit-reviewer role; `read` plus operator permission; incident-admin plus `read`), which evaluate two permissions per family.
Fix: Global replace "verb" → "operation family" in FR5, FR8, FR10, the Actors row, and §Authentication (or add a Glossary row defining verb as the audit unit); rewrite FR8 as "one folder level plus any declared tenant-level role per family".
Outcome: fixed after review — "verb" is replaced by operation family in FR5, FR8, FR10 and the authorization model.

**[Adversarial]** — The FR44↔Spine mapping rule contradicts itself in one paragraph, and the PRD uses a Spine category it gives no family (§Error Codes; A6; Spine `CanonicalErrorCategory` members)
If the Spine may carry categories for outcomes the PRD does not name, "every Spine category maps to exactly one family" is unsatisfiable; at least twelve current members (`success`, `redacted`, `internal_error`, `state_transition_invalid`, `client_configuration_error`, `projection_stale`, `projection_unavailable`, `query_timeout`, `response_limit_exceeded`, `range_unsatisfiable`, `failed_operation`, `provider_failure_known`) have no plausible parent. Conversely five FR44 families ("folder archived", "content unavailable", "duplicate operation", "stale or interrupted lock", "transient infrastructure failure") have no Spine member today; PD10 records "the mapping does not exist" but not that it cannot be total. A6 makes `state_transition_invalid` a normative product outcome without naming its FR44 family.
Fix: "every Spine category maps to exactly one FR44 family or to the enumerated `unmapped` list approved as C13 evidence"; name the families for `state_transition_invalid` and the projection/query members; list the five family-without-home cases in PD10.
Outcome: fixed after review — the mapping-rule contradiction is fixed.

**[Adversarial]** — Missing-binding and missing-policy are safe-denial cases regardless of the caller's authorization, which contradicts FR12 and the unbound-folder model (§Error Codes; §Endpoint Specifications; FR12; §Authentication; Spine `GetRepositoryBinding` requirement)
A folder administrator calling `GetRepositoryBinding` on the logical folder they just created must, as written, receive a response indistinguishable from the one a cross-tenant attacker receives, because "missing-binding" is in the list unconditionally. That makes "unbound" unobservable to the one actor who needs to observe it before calling `BindRepository`, and it makes FR12's "binding status" a lie for the most common status.
Fix: "for a caller not authorized on the folder or tenant scope, missing-binding and missing-policy are indistinguishable from absence; for an authorized caller they are reported as the stable `unbound`/`no policy` status."
Outcome: fixed after review — FR12 now gives authorized readers an explicit binding status.

**[Adversarial]** — The approved C3 record triggers cleanup on lock expiry and cancellation, which FR30 forbids and A20 says do not exist (`c3-retention.md` "Temporary working files" row; FR30; rows 3/5; A20; C3 row)
The Legal-approved retention record names "lock expiry" as a cleanup trigger; the PRD routes every lock expiry to `dirty`, which is never cleaned. The record also names "cancellation", a release reason the PRD reserves for post-MVP. Either the PRD's exclusion is a change to an approved Legal artifact (which the C3 row does not flag) or C3 is stale.
Fix: Add the disagreement to the C3 row and to PD10's model (owner, date); or align FR30 with C3 by making lease lapse without mutations cleanup-eligible.
Outcome: fixed after review — the C3 cleanup-trigger disagreement is routed to PD11.

**[Adversarial]** — "The same task" admits concurrent agents under one lock, and the ownership proof is obtainable by anyone who passes the same-task test (§Task identity; §Workspace State; A6; UJ4; Non-Goals)
Two AI tools delegated by Nadia that present the same caller-provided task identity are, by definition, one task. Each obtains the existing lock and its proof through the idempotent same-task acquisition, each mutates different paths (A6 guards only same-path collisions), and the task's "single commit" merges both: the mixed commit UJ4 exists to prevent, produced by design rather than by contention. The proof is derivable from principal + task identity, so it adds no security property; what it is for is unstated.
Fix: Bind the task identity at preparation to a server-issued task-instance token returned once and required on continuation, or state that concurrent agents under one task identity are the caller's responsibility and outside the no-lost-update guarantee; define the proof's purpose (lock-instance fencing) and say it is returned only on acquisition.
Outcome: fixed after review — concurrent agents presenting one task identity are defined as one task, and the delegating principal serializes them.

**[Adversarial]** — `X-Hexalith-Task-Id` is required on all 49 Spine operations, but the PRD defines its meaning for three (§Task identity; Spine header declarations on `ArchiveFolder`, `UpdateFolderAclEntry`, `ConfigureProviderBinding`, `ListFolderAclEntries`, every `Get*`/`List*`/`Search*`)
Which task identity does Elise present to archive a folder or edit an ACL, and what does a mismatch mean? When a third party reads `GetWorkspaceStatus` on a workspace bound to someone else's task, is the header validated against the binding, ignored, or used to decide the owner-only branch of the context rule? What does a second `PrepareWorkspace` with an already-bound task identity return? None of this is written, and the product-owned header is the one field the PRD insists must appear verbatim.
Fix: One paragraph: on tenant-administration operations the header is correlation only; on reads the header selects the owner branch when it matches the workspace binding and is otherwise ignored; a second preparation with a bound identity returns the duplicate-operation family without side effect.
Outcome: fixed after review — per-operation-class semantics for the header are defined.

**[Adversarial]** — Platform-local unknown outcomes are bolted onto a state five other passages define as external (§Command and Query Contract; Glossary "Unknown provider outcome"; row 11; reconciliation preamble; §Error Codes; NFR; matrix has no `locked + ProviderOutcomeUnknown`)
The five-check/15-minute evidence budget and "Provider compatibility evidence may define a shorter bound" are provider rules being applied to a local batch that has no provider; the approved matrix only enters the state from `requested`, `preparing`, and `changes_staged`, so a first-mutation interruption from `locked` has no entry.
Fix: Either update the Glossary, row 11, the reconciliation preamble, §Error Codes, and the NFR to "any unconfirmed side effect, platform-local or external" and add the `locked` entry to the C6 conflict list; or give local batches their own resolution rule (synchronous journal replay, no evidence budget).
Outcome: fixed after review — platform-local unconfirmed outcomes now land in `dirty`, never `unknown_provider_outcome`.

**[Adversarial]** — PD2(b)'s accountability hook cannot fire (PD2; CM2; §Measurable Outcomes)
In a designed cohort every non-resumable task is an injected failure, and injected failures are excluded from CM2 and CM3. The metric that is supposed to make the no-discard default visible is defined to exclude the events it should count, so PD2(b) has no cost anywhere the PRD measures.
Fix: Either make non-resumable tasks a counted class in the OQ10 plan (not excluded) or replace the hook with a field metric ("no more than N serialized repository/refs per tenant per quarter") that survives release.
Outcome: fixed after review — non-resumable tasks are now a counted class.

**[Drift]** — PD10 undercounts the operations that declare a leaking category as caller-visible (PD10 L1099 "on ten operations"; Spine `x-hexalith-canonical-error-categories`)
24 of 49 operations declare at least one of `cross_tenant_access_denied` (6), `audit_access_denied` (4), or `not_found` (24, including every diagnostic and status read). "Ten" is the union of the first two lists only; once `not_found` is named as a leaking member, the defect spans 24 operations, so the correction scope handed to Architecture is understated by more than half.
Fix: Change "ten operations" to "24 operations (all 24 declare `not_found`; six also declare `cross_tenant_access_denied` and four `audit_access_denied`)" or reference the generated list.
Outcome: fixed after review — PD10 now carries the corrected 24/6/4 counts.

**[Drift]** — Families table assignments contradict the Spine's per-operation authorization requirements and FR6 (§Protected operation families L542; FR6 L843; Spine requirement tokens for `CreateRepositoryBackedFolder`, `GetEffectivePermissions`, `ListFolderAclEntries`)
Creating a repository-backed folder needs only existing-folder access in the Spine (`tenant-context-existing-folder-access-and-provider-binding-use`), and effective-permission inspection is a read in both the Spine (`tenant-context-and-folder-permission-read`) and FR6 yet an `administer` family here. The families table did not exist at HEAD, so this is triage-introduced drift that PD10's "permission representation" bullet does not cover.
Fix: Either move "inspect effective permissions" (and ACL listing) into the read-level rows and decide whether `CreateRepositoryBackedFolder` is `administer` or `write`, or add both placements to PD10 as Spine corrections; record the choice in the memlog and the OQ3 closure condition.
Outcome: fixed after review — FR6 moved to the read family and the Spine requirement tokens are mapped in OQ3.

**[Drift]** — The C6 row attributes the disposition disagreement to an artifact that is itself stale against its declared source of truth (C6 row L708; memlog L139; `c6-transition-matrix-mapping.md` 2026-05-11; architecture.md L314/L344/L614; `FolderStateTransitions.cs` L100)
On disposition the approved authority already agrees with the PRD: architecture.md reads `unknown_provider_outcome` | `auto-recovering` (2026-07-15), while the 2026-05-11 mapping document it defers to still says `awaiting-human`. The disagreement the PRD records is between the mapping document and the architecture, so PD2 is asked to "close in one direction" a conflict the PRD does not actually have with the authority. On discard vocabulary the architecture labels `OperatorDiscardRequested` post-MVP/rejected while the aggregate implements it, which is the fact PD2 option (a) should cite. The architecture's "43 members" prose is a further staleness against the Spine's 48.
Fix: Reword the C6 cell to say the mapping document lags the architecture matrix, which already uses `auto-recovering`; hand the mapping-document refresh (and the 43-versus-48 count) to Architecture as C6/C13 hygiene outside the PRD.
Outcome: open

**[Drift]** — The manifest/sprint-status conflict PD4 records is still unresolved and its "Immediately" condition is still unmet (PD4 L1093; `planning-story-manifest.yaml` generated 2026-08-04; `sprint-status.yaml` last_updated 2026-07-14; spec-10-7/10-8/10-9 frontmatter)
The PRD text is now correct and complete, so nothing further is owed in the PRD; the drift is that no tracking artifact moved since the previous pass, the sprint-status header still reads 2026-07-14, and three carriers still disagree on 10.8.
Fix: No PRD change; regenerate the manifest from current evidence, reconcile the 10.8 story/spec/sprint statuses, hold 10.9 per PD5(a) or (b), update the sprint-status header, and close or re-date the recovery action.
Outcome: open

### Low (21)

**[Decision-readiness]** — PD1 and PD3 have an event, not a date, as their revisit condition (PD1, PD3 "Before the next implementation-readiness assessment")
For the two items that decide what blocks release, an undated trigger lets the next assessment happen with the inventory still incomplete.
Fix: Add "or 2026-09-30, whichever is earlier" to both.
Outcome: open

**[Substance over theater]** — Innovation section still duplicates the summary and remains deferred without a date (§Detected Innovation Areas; PD7 "Next major PRD revision")
Both paragraphs are the MVP Contract Summary's second paragraph and §Public Surfaces' first paragraph restated.
Fix: Cut §Detected Innovation Areas to the trust-surface sentence at the next edit and leave PD7 to the FR1/FR2/FR36 reclassification only.
Outcome: open

**[Strategic coherence; Adversarial]** — FR58 remains a Must-Have with no journey (FR58 `[NOTE FOR PM]` "no user journey depends on it"; PD9 default (a))
Downstream story authors will have no user-observable acceptance for the search facade beyond OQ5's evidence list; a default in force does not cure a must-have with zero journey dependency.
Fix: Add a short journey (an agent locating a prior task's artefact across the tenant's workspaces by classification, or UJ1's context step through indexed recall) or take PD9 (b) / move to Phase 2.
Outcome: open

**[Strategic coherence]** — Phase 3 list omits the SDK-language expansion that §SDK Requirements assigns to it (§SDK Requirements [ASSUMPTION A15] vs. §Post-MVP Features › Phase 3)
Fix: Add "additional SDK languages" to the Phase 3 list.
Outcome: open

**[Done-ness clarity]** — Open qualifiers survive outside the NFR lock (§Architectural Boundaries "where needed"; §SDK Requirements "where applicable"; FR57 "where it affects operational readiness"; FR2 "documents and demonstrates")
Each leaves the reader to guess the rule; the throttling list, by contrast, is now closed with "exactly".
Fix: Replace "where needed/applicable" with the condition, bound FR57 to the FR23 evidence fields, and define FR2's demonstration as a runnable example per surface exercising the ordered lifecycle including at least one failure transition.
Outcome: open

**[Done-ness clarity]** — Operator disposition labels every healthy active task "degraded" (§Workspace State and Concurrency "locked/changes-staged are degraded-but-serving"; C6 catalog)
A console that derives disposition from this rule warns on normal in-progress work.
Fix: Rename to "serving-with-active-task" in both the PRD and C6 (one lockstep change).
Outcome: open

**[Done-ness clarity]** — PD6 does not list every NFR bullet that still fails this dimension (§Scalability, §Observability, §Performance bullets)
Bullets not named in PD6 ("must remain queryable as folder history grows", "without making routine … queries unusable", "within a defined status-freshness target", "sanitized error category where applicable", "retry hints where available") will survive the relock unchanged.
Fix: Add these five to PD6's list.
Outcome: open

**[Scope honesty]** — An MVP FR names Phase 2 operations inside its own text (FR13 vs. §Explicit MVP Non-Goals [ASSUMPTION A13])
A story author reading FR13 alone scopes legal-hold administration into the archive story.
Fix: Move the legal-hold clause into the Non-Goals bullet and leave FR13 with "may still revoke access".
Outcome: open

**[Scope honesty]** — `[NON-GOAL for MVP]` tags unused where prose carries the exclusion inline (FR29; FR30; FR32; UJ3)
Present but not greppable the way assumptions now are.
Fix: Prefix the four inline exclusions with `[NON-GOAL for MVP]`.
Outcome: open

**[Downstream usability]** — FRs use the synonym, not the canonical actor (FR7, FR23, FR57 "Platform engineers"; FR15 "platform engineers")
FR1 requires Glossary terms in generated names and help; a generator lifting FR subjects will not read the synonym column.
Fix: "Tenant-scoped operators (typically platform engineers)" in the four FRs.
Outcome: open

**[Shape fit]** — Sprint-status arbitration inside the product contract (PD4; PD5)
Correct content, wrong artifact; the PRD accrues a row per tracking dispute and PD5 already needed a same-day status edit.
Fix: Replace PD4/PD5 with one-line pointers to a sprint-change proposal carrying the option analysis.
Outcome: open

**[Shape fit]** — Idempotency canonicalization rules remain mechanism-depth (§Command and Query Contract)
The product-owned invariant is the equivalence definition; the algorithm belongs to `docs/contract/idempotency-and-parity-rules.md`.
Fix: Keep the equivalence sentence, replace the algorithm sentence with a pointer.
Outcome: open

**[Adversarial]** — Five checks and fifteen minutes remain the only untagged numbers, now load-bearing for provider timeouts (§Task and Lock Completion Model; §Rate Limits)
Surviving verbatim.
Fix: `[ASSUMPTION A21]` with owner and revisit, or fold into OQ4. (Note: the post-review triage assigned A21 to the clean-`dirty` rule; this number remains untagged.)
Outcome: open

**[Adversarial]** — `status: final` with `finalized: 2026-07-15` over a document rewritten on 2026-09-08 (frontmatter; §Current Delivery Posture; PD register; `.memlog.md`)
Surviving in reduced form; the dates now disagree with the edit history rather than with the directory, and PD closure authority is a dotfile.
Fix: `status: approved-revised`, bump `finalized`, or move posture and PD closure to the readiness report.
Outcome: open

**[Adversarial]** — Post-commit and second-preparation behaviour remain undefined (row 7; row 2; Task identity; matrix `committed + WorkspaceLockReleased → ready`)
Whether a new task locks the `committed` workspace or must `PrepareWorkspace` a second workspace on the same folder (counting against "2 active workspaces per tenant"), and what the matrix's `committed → ready` release means when row 7 already says `unlocked`, is unstated.
Fix: One row: "New task on a committed workspace → `PrepareWorkspace` creates a new workspace; the committed one is cleanup-eligible."
Outcome: open

**[Adversarial; Drift]** — Identifiers the assumptions depend on are not product-owned, and provenance hygiene lags (§MVP Contract Summary exception list L140; A20; A12/A17; A6; Search Families "lock-independent read"; frontmatter `documentCounts`; memlog L53)
A20 constrains release to the Spine's `caller_completed`, the family table hangs on the three level names, `lockOwnershipProof` is load-bearing in the completion model and FR29, and none is in the exception list, so the Spine may rename them without a PRD conflict. The Search Families row's "lock-independent read" contradicts the owner-only rule for `changes_staged`/`dirty`. Any evidence citing 50 operation ids is stale (the Spine has 49). `documentCounts.validationReports: 1` while `inputDocuments` lists four review files; memlog L53 still names Borel/Theo although L123 superseded it.
Fix: Add the three level names, `caller_completed`, `state_transition_invalid`, and `lockOwnershipProof` to the exception list (or say the assumptions are Spine-relative); change "lock-independent" to "lock-independent for `ready`/`locked`/`committed`"; add `reviewReports: 3` (or `validationReports: 4`); tag memlog L53 as superseded by L123.
Outcome: open

**[Adversarial]** — A19 lets an archived folder in tenant A retain a live pointer to a remote that tenant B now writes (A19; A8; UJ9)
Tenant A's archived view keeps repository identity and last commit reference for a remote that tenant B may subsequently bind; nothing leaks from B, but A's archived binding now names a resource A no longer holds, and audit reviewers in A cannot tell.
Fix: State that archived binding metadata is frozen at archive time and labelled `released`.
Outcome: open

**[Drift]** — The OQ4 partial approval of 2026-09-05 is still absent from the PRD and memlog (OQ4 L1047; manifest OQ4 notes)
The manifest records the PM approval of the GitHub profile; the memlog has no `2026-09-05` entry. OQ4 correctly stays open; the missing piece is the ledger entry.
Fix: Memlog `(event)` for the 2026-09-05 GitHub-profile approval; optionally note it in the OQ4 evidence cell.
Outcome: open

**[Drift]** — The 2026-07-19 OQ8 design approval is referenced but never logged as an event (memlog L139; §L507; `oq8-idempotency-evidence.yaml`)
The memlog records the consequence (A3/A4 withdrawn) but not the approval itself, so the ledger has no dated entry for the decision that withdrew two assumptions.
Fix: Append a memlog `(event)` "2026-07-19 OQ8 design approved by Architecture, Security, and Test".
Outcome: open

**[Drift]** — The C3 row omits the consumed-key class that L507 relies on and lists only the PM/Legal approvers (C3 row L705; §L507; `c3-retention.md` L10, L28/L52)
The class exists and is approved (Architecture/Security/Test, 2026-07-19), but by a different approver set and date than the row cites, and a reader of the exit-criteria table cannot find the class L507 names.
Fix: Add "consumed idempotency-key evidence: managed-tenant lifetime plus 400 days (Architecture/Security/Test, 2026-07-19)" to the C3 row.
Outcome: open

**[Drift]** — UX specification still predates every ratified correction (`ux-design-specification.md` 2026-05-11, zero UX-DR33; readiness report L652)
Not a PRD defect; the PRD's 11-state lifecycle, Actors table, families table, FR56 dual authorization, and safe-denial rule still have no UX counterpart.
Fix: Nothing in the PRD; execute the approved UX reconciliation.
Outcome: open

## Post-review triage summary
- fixed after review: 18 — C6 matrix disagreement (C6 row + PD11); safe-denial 403/404 split and merged envelope (PD10); zero-mutation `dirty` (A21); task-terminal cleanup discard / A18 window; read grant exposing file bodies (A22); UJ1 bootstrap (A23); C6 disposition → PD11; workspace-to-task cardinality; ghost term "verb"; FR44↔Spine mapping rule; missing-binding status for authorized readers (FR12); C3 cleanup triggers → PD11; shared task identities; `X-Hexalith-Task-Id` per-operation-class semantics; platform-local unknown outcomes → `dirty`; PD2 metric hook (counted class); PD10 operation undercount (24/6/4); families versus Spine requirement tokens (FR6 → read family, OQ3)
- open: 23 — 2 medium (C6 row attributes the disposition disagreement to a stale mapping document; PD4 manifest/sprint-status tracking still unreconciled) and 21 low

## Resolved since the first pass
- Release-blocking inventory presented as complete — §Open Release Items preamble now states the inventory is incomplete until PD1 and PD3 resolve; PD1 names OQ11.
- Context-query scoping rule omitted `locked` and contradicted itself on `ready` — one rule: `ready`/`locked`/`committed` readable by any folder `read` holder; the four owner-only states listed.
- Folder ACL verb identifiers declared Spine-verbatim / verb table not carried by the Spine — replaced by "Protected operation families and permissions" mapped onto the Spine's `FolderPermissionLevel` (`read|write|administer`, verified) with a "Spine grant today" column; verbatim list now includes `X-Hexalith-Task-Id` and declares every other identifier illustrative; OQ3 decides levels vs. per-family grants.
- FR44 lacked "folder archived", "content unavailable", a named safe denial, and was contradicted by the closed Spine enum — FR44 is an outcome-family enumeration realized by the Spine under a C13-published mapping; §Error Codes defines when `folder ACL denied` versus the safe denial is returned; leaking members recorded as PD10 defects; mirrored in epics.md.
- FR44 categories and Spine enum not one-to-one — §Error Codes allows finer and additional Spine categories under an explicit family mapping; PD10 records the mapping as a deliverable.
- Verb-table rows mis-assigned lock inspection and provider-binding configuration, mis-homed tenant-level operations, handed archive to tenant members — family table puts lock inspection in "Status and lock inspection" (`read`), provider configuration as a tenant-level family with no folder ACL entry, archive with tenant administrators (FR13).
- Completion table contradicted itself on commit failure and revocation — row 6 (`inaccessible`/`revoked`, A18) and row 8 (`dirty`/`locked`) are now distinct.
- Lease-lapse row contradicted the approved C6 matrix — row 3 now reads `dirty`/`expired` and cites the matrix; `FolderStateTransitions.cs` agrees.
- Stale threshold closed the owner's exit — "`stale` restricts nothing further for the owner".
- Task-identity binding denied the UJ7 hand-off — delegated service agents resolve to the binding principal; before preparation the header is correlation only.
- Crash recovery collided with non-reentrancy and a 24-hour lease — idempotent same-task acquisition returns the existing instance and proof.
- Synchronous mutations vs Spine `202` — file mutations are accepted commands; A6 aligned to "rejected".
- Archive-preserves-binding made a repository permanently unbindable; non-enumeration claim overstated — A19 and the honest provider-access scope sentence.
- "Only creation/binding and commit reach the provider" — corrected to readiness validation, workspace preparation, repository creation or binding, and commit.
- `folder ACL denied` distinguishable from safe denial leaked existence — hidden-folder rule: returned only when the caller holds at least one allow on that folder.
- Archive requires "no lock" but stale locks on clean workspaces never released — FR13 "no lock in state `locked`" (superseded in this pass by the row 3 `dirty` finding, itself now fixed).
- FR9 byte-equivalence unverifiable — equivalence defined as status/category/code/message/detail keys apart from correlation and per-request identity; test-harness access counters.
- A3/A4 disagreed with the approved OQ8 design / A4 contradicted the consumed-key class — A3/A4 withdrawn in place with reasons; §Command and Query Contract cites the C3 consumed-key class matching `oq8-idempotency-design.md` and `c3-retention.md`.
- Spine release reasons included forbidden operations — A20: release accepts only the caller-completed reason in MVP.
- `details.visibility` not required by the Spine — recorded as a PD10 defect.
- PD2 not connected to CM2/CM3 and undated; PD2/PD9 decided but pending; assumptions addressed to the author with no date — PD2 counts non-resumable tasks against CM2/CM3 with revisit 2026-09-30; PD2/PD9 state "Default in force since 2026-09-08"; A-rows carry named approvers and revisit dates.
- SM7 had no counter-metric — CM5 (hollow adoption); OQ10 covers SM1–SM8 and CM1–CM5.
- Unanchored nouns "memlog", "Workstream 11", "Hexalith integration register" — cited by path, renamed "Epic 11", marked "(to be created; OQ10)".
- Open-list qualifier in §Rate Limits — throttling dimensions closed with "exactly" (remaining qualifiers restated as one low finding).
- Story 10.9 advanced, PD5 stale; manifest/sprint-status conflict wider than PD4 — resolved in the PRD text (PD5 records `review`; PD4 names the Epic 3/11 rows and the un-regenerated manifest); tracking-side resolution still pending (open medium).
- PD index out of order (PD8/PD9), PD1 not naming OQ11, A6 wording — PD1–PD10 in order; PD1 reads "Add it as OQ11"; A6 says "rejected".
- Product-owned list not extended; index/provenance hygiene — partially resolved (`X-Hexalith-Task-Id` added; index order fixed); `lockOwnershipProof`, level values, `validationReports` count, and memlog L53 remain (open low).
- Verified unchanged and still true: FR1–FR58 clause lines byte-identical between `prd.md` and `epics.md` (58 of 58); NFR1–NFR73 byte-identical (73 of 73); the six "(to be created)" evidence paths absent and the thirteen unmarked paths present.

## Mechanical notes
- Frontmatter: `status: final`, `finalized: 2026-07-15`, `updated`/`lastEdited: 2026-09-08`; the 2026-09-08 edit-history entry is accurate to the body including the reviewer-gate sentence; `implementationReadinessAssessedAt: 2026-08-04` matches §Current Delivery Posture.
- ID continuity: FR1–FR58, OQ1–OQ10, SM1–SM8, CM1–CM5, UJ1–UJ9, C0–C13, PD1–PD10 contiguous, unique, and in order; A1–A20 with A3 and A4 retained as "Withdrawn 2026-09-08" rows; OQ11–OQ13 referenced only as drafts in `reconcile-july-2026-synthesis.md` §8. (The post-review triage added PD11 and A21–A23; not re-verified by this pass.)
- Assumptions Index roundtrip: inline tags A1 ×3, A2 ×2, A5–A16 and A18–A20 ×1, A17 inside "[ASSUMPTION A12, A17]"; every live row appears inline; A3/A4 have no inline tag by design. `[NOTE FOR PM]` ×3 (FR25–FR29, FR32–FR35, FR58). `[NON-GOAL for MVP]` ×0.
- Cross-references to repository artifacts: 14 of 20 cited paths exist; the 6 missing (`c7-lock-authorization-timing.md`, `authorization-matrix.md`, `fr58-search-evidence.md`, `console-projection-evidence.md`, `incident-access-evidence.md`, `release-calibration-plan.md`) are exactly the ones marked "(to be created; OQn)". `.memlog.md` exists and records PD1–PD6, PD9, PD10; PD7 and PD8 have no memlog entry yet. Governance YAML: C7 and C12 `reference_pending`, all others `approved` — matches the C-table.
- Brownfield identifier check (Spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`): `FolderPermissionLevel` enum `read|write|administer` matches the family table; `releaseReasonCode` enum matches A20; `X-Hexalith-Task-Id` (62), `SafeAuthorizationDenial` (151), `idempotency_key_expired` (19), `isTruncated` (26) present; `cross_tenant_access_denied` (8), `audit_access_denied` (6), `not_found` (25) present as PD10 states; `folder_acl_denied` (41) and `safe_denial` (45) exist; no `folder_archived` or `content_unavailable` token (covered by PD10's missing mapping); `CreateFolder` declares `tenant-context-and-folder-create-permission`; no initial-ACL rule appears in the Spine.
- Architecture C6 cross-check: `locked → dirty | LockLeaseExpired (no mutations applied yet)` and `locked → ready | WorkspaceLockReleased (clean release with no mutations)` confirm completion-table rows 2 and 3 faithful to C6; C6 mapping catalog `unknown_provider_outcome → awaiting-human` confirms the recorded disposition conflict (the architecture matrix itself already reads `auto-recovering` as of 2026-07-15, per the drift review).
- epics.md lockstep: 73 NFR bullets; FR44, FR8, and FR9 mirror the PRD text; all 58 FR clause lines byte-identical (programmatic diff). CM1–CM5 are not mirrored in epics.md (never were; not a defect). Against the final post-triage text the NFR conformance gate (`NfrTraceabilityConformanceTests`, 16 tests) passes and the 58 FR bullets remain byte-identical.
- Glossary drift: core nouns clean. Residual: "verb" (undefined at review time; replaced after review); "Platform engineers" as FR subject in FR7/FR23/FR57 and FR15; "commit SHA" in UJ1/UJ8 (Glossary declares it equivalent); "Git-backed" survives only as an adjective; "safe blocked state" (UJ3) is descriptive. State casing consistent.
- UJ protagonists: all nine named inline — Nadia (UJ1, UJ4, UJ7), Elise (UJ2, UJ6, UJ9), Asha (UJ3, UJ4), Marcus (UJ5, UJ7), Priya (UJ8); UJ4's competing agent unnamed by design.
- Required sections for stakes: all present; the decision log the index depends on exists at the cited path.
- Memlog roundtrip (drift review): memlog decisions absent from the PRD: none; the seven triage entries L134–L140 trace to PRD lines. Post-finalization decisions absent from the memlog: the 2026-07-19 OQ8 design approval, the 2026-09-05 GitHub-profile approval, Stories 3.10/3.12/11.2 `done` and 11.3/11.4 `review`, and the two Spine conflicts the drift pass surfaced.

## Reviewer files
- `review-rubric.md`
- `review-adversarial-general.md`
- `review-post-finalization-drift.md`
