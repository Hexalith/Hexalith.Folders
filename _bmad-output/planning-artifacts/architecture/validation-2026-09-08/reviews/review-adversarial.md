# Adversarial architecture validation

Verdict: changes required. The architecture contains incompatible normative rules for file transport and Memories access, and its total lifecycle matrix does not preserve the phase needed to reconcile interrupted provisioning safely. The separate, already recorded PD11 completion-model conflicts remain blocking and are not new discoveries.

Reviewed 2026-09-08 against `_bmad-output/planning-artifacts/architecture.md` (legacy document intentionally retained), current `_bmad-output/planning-artifacts/prd.md` (1,103 lines), the Contract Spine, C6/C3/OQ8 records, selected production configuration, and the transition implementation. This is static architecture validation: scenarios below demonstrate incompatible contracts or modeled outcomes, not successful production exploits. No input artifact, implementation, or approval record was changed.

## ADV-1 — High — Reconciliation drops the originating provisioning phase

Classification: new concrete C6 design gap; discuss with Architecture and add to the C6 re-approval scope.

Evidence:

- `_bmad-output/planning-artifacts/architecture.md:324` sends a normal successful repository binding to `preparing`, dispatching workspace preparation. Line 327 permits `ready` only after `WorkspacePrepared` on that normal path.
- The same document at lines 326 and 329 sends both binding and preparation ambiguity to `unknown_provider_outcome`. Lines 348–354 permit reconciliation exits to `ready`, `committed`, `failed`, or escalation, but never to the interrupted binding/preparation phase. The default at line 299 rejects every unlisted pair.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:47` accepts only current state, event, and optional commit-oriented dirty resolution; lines 60–67 erase whether the ambiguity arose during binding or preparation. Lines 108–119 implement clean reconciliation directly to `Ready` and dirty reconciliation to commit outcomes.
- `_bmad-output/planning-artifacts/prd.md:423` says an unbound logical folder cannot prepare a workspace; line 425 requires an actually prepared workspace for context access. Its line 489 requires confirmed outcomes to follow the corresponding known outcome. PD11 at line 1103 records other C6 disagreements but does not identify this loss of provisioning phase.

Concrete failure scenario: repository creation times out before a repository is created; bounded read-only reconciliation proves that no mutations occurred. A reconciler following line 348 emits `ReconciliationCompletedClean`, and the matrix reports `ready`, although no repository binding or prepared workspace exists. Alternatively, a reconciler that confirms successful creation tries to emit `RepositoryBound` so preparation can begin; the total matrix rejects `(unknown_provider_outcome, RepositoryBound)`. An independently built provisioner and lifecycle reducer therefore cannot both follow the documented normal pipeline and reconciliation pipeline.

Required invariant: persist the originating operation/phase and the evidence needed to resume it; bind each reconciled outcome to the same prerequisites and follow-up work as the equivalent confirmed normal outcome. A clean result must not imply a materialized workspace. Add paired normal/ambiguous binding, preparation, and commit cases to C6 rather than relying only on state/event enumeration coverage. No claim is made that the incomplete deployed data plane currently exposes this modeled false-ready path.

## ADV-2 — High — D-9 disagrees with its own canonical wire contract

Classification: architecture-to-contract contradiction; reconcile to the existing wire authority, with Contract/Architecture review before changing any public schema.

Evidence:

- `_bmad-output/planning-artifacts/architecture.md:549` requires two distinct operations and two generated SDK methods, explicitly rejects base64, rejects a single-operation `oneOf` design, and names response header `x-hexalith-retry-as: stream`.
- The same document at line 567 makes OpenAPI the single Contract Spine, and line 792 instead requires `X-Hexalith-Retry-Transport` for stream substitution while reserving `X-Hexalith-Retry-As` for caller/operator allocation.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2032` and line 2200 name the actual operations `AddFile` and `ChangeFile`. Lines 8368–8453 place `PutFileInline` and `PutFileStream` inside the request's discriminated `oneOf` transport shape. Lines 8486–8490 require base64 `contentBytes`. Lines 2087–2089 publish `X-Hexalith-Retry-Transport` on 413.
- `_bmad-output/planning-artifacts/prd.md:140` assigns operation names and wire schemas to the Contract Spine; generated surfaces cannot override either normative source.

Concrete incompatible pair: an SDK author follows D-9 and generates/exposes separate inline/stream methods with non-base64 JSON; a server author implements the frozen AddFile/ChangeFile schema with base64 content and a transport discriminator. The first request cannot satisfy the second schema. Even if the body is repaired, an adapter following D-9's old header name will miss the server's documented stream hint or confuse it with retry allocation.

Required invariant: replace D-9's superseded operation, encoding, and header assertions with the adopted Contract Spine contract, or explicitly govern a coordinated public-contract revision. Do not change the canonical schema merely to make the old architecture prose true. This finding is distinct from PD10's already recorded safe-denial/permission/release-reason conflicts.

## ADV-3 — High, already tracked — C6 cannot implement the current task-completion and revocation rules

Classification: known open decision PD11/OQ1/OQ7, not a newly discovered product gap; discuss and retain as blocking until re-approved.

Evidence:

- `_bmad-output/planning-artifacts/architecture.md:336` handles authorization revocation while `locked`, but no `changes_staged` revocation row exists; its default rejection at line 299 therefore rejects that event after the first file mutation.
- Lines 339, 346–347, and 352 bind commit failure/restoration to `failed` or unconditional `ready`. Lines 343–354 expose operator repair transitions despite the explicit no-user-repair MVP rules at lines 892–893.
- `_bmad-output/planning-artifacts/prd.md:1103` explicitly identifies originating-task resume, retryable commit failure, staged revocation, restoration, dirty/stale cleanup, dispositions, and operator repair as C6 disagreements needing re-approval. PD2 at line 1094 explicitly accepts the default no-discard posture pending a PM decision. Assumption A18 at line 1082 expects restored authority to preserve staged changes.

Concrete incompatible pair: the authorization worker follows the architecture's requirement to revoke held work within the freshness budget and emits `AuthRevocationDetected` after staging. A reducer generated exactly from the total matrix rejects the event and preserves `changes_staged`. Separately, a restoration implementation following the matrix reports `ready` while one following the current PRD reports `dirty` with staged work retained. These are real contract disagreements, but they are already in the PM decision inventory. There is no claim that per-call authorization checks are bypassed in the running product.

Required action: close PD11 in a coordinated C6 mapping, architecture, code, and test change; preserve PD2's recorded default until the PM changes it. Do not silently add a discard capability or mark the accepted no-discard consequence as a newly unowned issue.

## ADV-4 — Medium — I-3 grants workers a Memories invoke permission forbidden by the facade boundary

Classification: internal architecture contradiction with a clear current implementation precedent; architecture-only correction is reviewable without expanding network authority.

Evidence:

- `_bmad-output/planning-artifacts/architecture.md:641` explicitly says both `folders` and `folders-workers` may invoke `memories`.
- The same document at lines 174 and 179 explicitly restricts workers to publication over pub/sub with no invoke rule; only the Server facade may invoke `GET /api/search`.
- `deploy/dapr/production/accesscontrol.yaml:132` documents this same boundary, and lines 134–141 grant only `folders` the GET search operation. There is no workers invoke entry in the Memories target policy.

Concrete incompatible pair: a deployment author treating I-3 as the access-control specification adds a workers-to-Memories invoke allow-rule; the facade security-test author following line 179 requires that exact triple to be denied. Both are complying with an explicit architectural instruction, but their artifacts cannot pass the same policy test. Granting that additional invoke permission would broaden the published boundary; this review does not establish a data disclosure or show that the current production policy grants it.

Required invariant: state the facade-only GET permission in I-3 and retain workers' publication-only boundary, unless a separate architecture decision authorizes a new need. Regenerating a more permissive policy is not the fix implied by this validation.

## Known blockers and topics deliberately not counted as new findings

- The unbuilt or incomplete durable data plane, empty diagnostic projections, search bridge evidence, and release readiness are explicitly acknowledged at architecture lines 197–231 and 1698; the review does not relabel their acknowledged absence as an undiscovered design flaw.
- C7 timing, provider catalog, alias-collision lock evidence, safe-denial schemas, confidential-value replacement, and release inventories have explicit OQ/PD owners in the current PRD. Their remaining approval or evidence is not supplied by this review.
- The approved OQ8 design at `docs/exit-criteria/oq8-idempotency-design.md:15` and lines 26–60 binds the admission owner, descriptor, fencing, restart, and tombstone retention semantics. It prevents the simplistic expired-key deletion/re-execution counterexample; that issue is not reported against this architecture.
- C3 read-model lifetime versus retained audit replay is already called out through PRD A14/PD6. No claim of actual expired-data resurrection is made without inspecting a complete production retention and replay path.
- Legacy formatting, absent new-style AD records, and rationale verbosity are outside this adversarial lens; no reformatting is required by these findings.

## Validation limits

Evidence comes from read-only source/document comparison and the explicit state-switch implementation. No production service was invoked, no deployment policy was changed, and no .NET test suite was run for this document-only review. Initial searches under the anticipated `src/Hexalith.Folders.AppHost/DaprComponents/production/` and `Workers/RepositoryWorkflows/` locations returned path-not-found; actual policy evidence was resolved under `deploy/dapr/production/`, and no finding depends on the missing anticipated paths. Recommended checks above are acceptance criteria for a future authorized Update, not claims of completed testing.
