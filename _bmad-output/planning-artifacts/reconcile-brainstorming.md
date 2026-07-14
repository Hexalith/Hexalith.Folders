# Input Reconciliation — Brainstorming Session

Input: `_bmad-output/brainstorming/brainstorming-session-20260505-070846.md`

Compared with: `_bmad-output/planning-artifacts/prd.md` (updated 2026-07-14)

Addendum: no `addendum.md` was present in the planning-artifacts workspace at reconciliation time.

Purpose: preserve product-level ideas and qualitative intent from the brainstorming session while distinguishing later, intentional scope decisions and implementation mechanisms. This file does not change the PRD, brainstorming source, architecture, contracts, or implementation.

## Reconciliation verdict

The updated PRD retains the brainstorming session's defining character unusually well. Hexalith.Folders is presented as an AI-native task workspace boundary—not a generic file manager, Git UI, chatbot interface, or repair console. It hides provider/filesystem mechanics from agents, makes prepare/lock/change/commit/status the core workflow, treats GitHub and Forgejo differences as explicit capabilities, exposes metadata-only audit, and frames the read-only console as a workspace trust surface rather than a CRUD folder browser.

Four product-level gaps remain. Several ambitious brainstorming ideas were intentionally narrowed or deferred by the later PRD and should not be restored as accidental MVP scope.

## Meaningful gaps and qualitative losses

### 1. Effective-permission precedence is not defined

Brainstorming intent:

- Tenant/organization permissions provide a baseline.
- Folder ACLs may add or narrow permissions for a particular folder.
- Users, groups, roles, and delegated service agents share one effective-permission model.
- The same model governs metadata, file operations, Git actions, and operational views.

PRD coverage:

- FR4-FR10 cover tenant and folder access controls, multi-principal grant/revoke, effective-permission inspection, and protected-operation checks.
- UJ9 makes day-to-day ACL administration and effective-permission visibility part of MVP.
- OQ3 requires a canonical actor/access-state × operation authorization matrix.

Gap:

The PRD does not state how tenant/organization grants, role membership, delegated authority, and folder ACL entries combine. It is unclear whether a folder ACL can grant beyond the baseline, only narrow it, whether explicit deny wins, and how conflicting user/group/role entries resolve. An effective-permissions endpoint cannot be authoritative across surfaces until those precedence semantics are fixed.

Product-level reconciliation:

- Define the authorization composition invariants at product level: baseline source, allowed folder-level widening/narrowing, explicit-deny behavior, principal conflict precedence, and archived/revoked overrides.
- Bind the exact actor × operation results to OQ3 and the canonical authorization matrix.
- Keep rule-evaluation data structures and policy-engine implementation downstream.

### 2. Folder archive does not define the provider-repository disposition

Brainstorming intent:

Folder archival must have an explicit remote-repository policy so folder lifecycle and provider state do not silently diverge. The session considered archiving or disabling active use while preserving data and exposing recovery/status information.

PRD coverage:

- FR13 denies later mutations against an archived folder.
- FR14 and C3 preserve metadata-only lifecycle, audit, lock, timeline, and last-commit evidence.
- The MVP consistently treats the provider-confirmed remote/ref as durable state.

Gap:

The PRD says what Folders does after archive but not what happens to the bound GitHub/Forgejo repository. Leaving the remote active, archiving it through the provider, detaching it, or merely refusing Folders operations have materially different user and operational outcomes. The current contract also lacks an explicit status for a remote whose archive disposition differs from the logical folder.

Product-level reconciliation:

- Choose the MVP remote disposition for folder archive and state whether it is mandatory, best-effort, unsupported, or policy-controlled.
- Define the caller-visible result when logical archive succeeds but provider disposition fails or is unconfirmed.
- Preserve data, audit evidence, non-enumeration, and explicit recovery posture; keep provider API mechanics downstream.

### 3. Multi-file task semantics and move/rename behavior were narrowed without an explicit decision

Brainstorming intent:

- A chatbot task locks a workspace, applies many file changes, and commits them as one logical change.
- Batch file operations may group multiple mutations under one command/change identifier.
- File move/rename is a first-class operation rather than an accidental delete-plus-add.
- Audit can remain fine-grained while projections present the task as one coherent change.

PRD coverage:

- The canonical task lifecycle supports multiple add/change/remove operations before one commit.
- FR38-FR41 preserve task/correlation identity, changed-path evidence, and idempotency.
- The MVP evidence and NFRs acknowledge large change sets and require traceable summarized projections.

Gap:

The PRD does not say whether an atomic/logical batch command exists, what happens when a multi-file apply partially fails, or whether move/rename must preserve identity/history as a supported operation. Add/change/remove plus commit may be an intentional minimal surface, but the omission is not named as a scope decision. Different adapters could therefore expose incompatible batching and rename semantics while still claiming lifecycle parity.

Product-level reconciliation:

- Decide whether MVP includes an explicit batch and/or move operation.
- If included, define logical-result, partial-failure, idempotency, audit, limit, and cross-surface parity outcomes.
- If excluded, name batch atomicity and first-class move/rename as non-goals; require clients to use the canonical sequential add/change/remove lifecycle and define how partial task state remains inspectable.
- Keep per-file event granularity, compaction projections, and aggregate stream design downstream.

### 4. AI context efficiency remains a capability but is no longer a success outcome

Brainstorming intent:

The AI Context Query Surface was a top-priority idea. Its purpose was not merely to offer tree/search/glob/partial-read endpoints; it was to let an agent decide what to read before loading content, reducing context waste and avoiding blind whole-repository reads.

PRD coverage:

- The Executive Summary and MVP include relevant context access.
- FR34-FR35 and the query NFRs define strong authorization, result, byte, path, and time bounds.
- FR58 separately and correctly limits indexed recall to authorized metadata tokens rather than silently promising body-content RAG.

Qualitative gap:

The PRD validates query correctness and boundedness but does not validate the intended agent experience: discovering a relevant file subset and reading only bounded ranges without direct filesystem access or whole-repository ingestion. The brainstorming's "AI context economics" value has become an endpoint inventory rather than an observable success condition.

Product-level reconciliation:

- Add one canonical agent-context acceptance scenario: inspect structure/metadata, narrow with search or glob, read bounded ranges, and complete context selection without unrestricted traversal or whole-repository fetch.
- Measure correctness, security trimming, truncation visibility, and bounded data retrieved; do not turn this into full body-content indexing or RAG scope.

## Qualitative intent already preserved; no PRD change needed

- **AI-native Git workspace boundary:** agents use task/file/context primitives while Folders owns Git and workspace mechanics.
- **Workspace trust surface:** the console foregrounds what is broken, who/what is affected, and what can safely happen next; readiness, lock, dirty, commit, provider, and failure signals are first class.
- **Maintenance UI boundary:** read-only, metadata/status focused, separate from the chatbot UI, not a file editor or hidden repair console.
- **Task transaction workflow:** prepare, lock, apply changes, commit once, inspect durable outcome and interrupted state.
- **Capability-gated providers:** GitHub and Forgejo differences are explicit and tested instead of hidden behind a lowest-common-denominator abstraction.
- **Event-first audit posture:** changes remain explainable through metadata-only evidence and task/correlation/commit identity without storing file bodies.
- **Delegated chatbot authority:** an agent is accountable to explicit tenant/folder authority rather than acting as an unrestricted technical bypass.

## Intentional scope cuts and later decisions

These differences are explicit later product decisions, not reconciliation gaps:

- Local-only folders, local-first promotion, unmanaged local-directory import, and local working-tree adoption moved post-MVP; binding an eligible pre-created provider repository remains in MVP.
- Auto-commit mode, disposable-cache repair, evented repair commands, drift remediation, and a drift-first control room are post-MVP.
- Multiple Git organizations per tenant and richer provider capability recipes are named post-MVP pressure points.
- Incoming provider webhooks are excluded from MVP pending an approved tenant-routing design, despite the brainstorming session treating webhook setup as part of managed repositories.
- "All file types and any file size" was narrowed for safety: the PRD requires explicit binary/large-file policy and stable rejection for unsupported input; broader large-file support is post-MVP.
- Full body-content indexing/recall is not implied by the AI context surface; the current release has controlled workspace queries plus metadata-token recall only.
- Operator-triggered discard, commit, reconnect, rebuild, or repair actions are intentionally absent from the read-only MVP console.

## Technical ideas correctly kept downstream

The following remain valuable architecture/contract inputs but should not be copied into the PRD as implementation mandates:

- an `IGitProvider` interface, low-level capability flags versus workflow-level capability recipes, and exact provider adapter composition;
- per-file events, per-aggregate streams, integration-event coarsening, event compaction projections, and manifest/threshold storage strategies;
- module-controlled physical storage roots, readable physical path naming, deterministic cache paths, and cache rebuild mechanics;
- exact secret providers (environment variables or Dapr secrets), webhook plumbing, repository topics/descriptions, and provider client choices;
- projection inventories, hash-index storage, drift scanners, correlation field placement, and provider reconciliation jobs;
- synchronous versus asynchronous provisioning mechanics, compensation algorithms, and retry scheduling.

These mechanisms should be evaluated by architecture, the Contract Spine, and implementation evidence against the product outcomes above.
