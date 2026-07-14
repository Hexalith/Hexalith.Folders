# Validation-Fix Source Extract — Hexalith.Folders PRD

Purpose: evidence-backed source map for reconciling the validation findings into the PRD. This file does not change the PRD, architecture, contracts, fixtures, or implementation artifacts.

Extraction date: 2026-07-14

## Authority order used for this reconciliation

Use different authorities for different questions rather than calling REST, the SDK, or an implementation artifact the source of truth for everything:

1. The PRD owns product intent, release scope, actors, user-visible outcomes, and success criteria.
2. The OpenAPI 3.1 Contract Spine at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` is the canonical machine-readable operation/schema contract (Architecture C0/A-1; `_bmad-output/project-context.md`, Contract Spine rules).
3. The generated SDK is the typed canonical client; CLI and MCP wrap it; REST is the parallel runtime transport and its emitted OpenAPI must validate against the Contract Spine (Architecture A-2/A-3; `_bmad-output/project-context.md:77-78`; `docs/contract/idempotency-and-parity-rules.md`).
4. `tests/fixtures/parity-contract.yaml` is the generated C13 operation/parity denominator, not a separate product-scope authority. It currently contains 49 operation rows, including the two FR58 rows `SearchFolderIndexedFiles` and `GetFolderIndexingStatus`.
5. `docs/exit-criteria/c0-c13-governance-evidence.yaml` is the live status authority for C0-C13. Criterion detail comes from each linked artifact.

PRD anchor: replace “REST is the canonical contract” in **MVP Contract Summary**, **Public Surfaces**, FR47, and related prose with this separation. Keep REST as a required public transport, not the schema/product authority.

## A. Authoritative decisions safe to reconcile now

### 1. C3 retention is approved and numeric

Status: **approved for live release**. PM: Jerome, 2026-06-22. Legal: Jérôme Piquot, 2026-06-24, Louveciennes. No open policy questions.

Canonical sources: `docs/exit-criteria/c3-retention.md`; `docs/exit-criteria/c0-c13-governance-evidence.yaml` C3.

Binding product values:

| Data class | Retention/disposition |
| --- | --- |
| Audit metadata | 7 years; metadata-only; tenant-deletion anonymizes user display aliases while preserving audit-safe correlation/category/timestamp/outcome evidence. |
| Workspace status | 400 days; terminal metadata retained; task-local display labels tombstoned after tenant deletion. |
| Provider correlation IDs | 400 days; IDs/status classes only, never provider payloads or tokens. |
| Read-model views | 400 days or until rebuilt from event streams, whichever is sooner. |
| Temporary working files | Delete 7 days after terminal workspace state; retain metadata-only cleanup evidence. |
| Cleanup records | 400 days. |
| Folder metadata and soft-delete markers | Tenant lifetime plus 400 days after the tenant-deletion request enters the approved retention workflow; tombstone identity/hierarchy metadata subject to legal hold/audit needs. |
| Auth claims copied into metadata | 400 days; normalized subject/transformed tenant/permission category only; anonymize aliases. |
| Diagnostics and rejected-command records | 400 days; bounded canonical failure metadata only. |
| Commit idempotency records | Same as audit metadata: 7 years. |

PRD anchors:

- Replace the C3 TBD row in **Deferred Quantitative Targets — Architecture Exit Criteria**.
- Replace the “must be defined” wording in **Data Retention and Cleanup** with the approved policy and canonical link.
- Tighten FR14, archive acceptance, cleanup visibility, and commit-idempotency retention references to C3.

Do not copy retention-job, compaction, or storage mechanics into the PRD.

### 2. C4 query/input limits are approved and numeric

Status: **approved** by Jerome (PM), 2026-06-22. These are global MVP defaults; tenant-tunable limits are deferred.

Canonical sources: `docs/exit-criteria/c4-input-limits.md`; governance YAML C4.

Binding product values:

| Limit | Value and required outcome |
| --- | --- |
| Requested paths | Maximum 100 inclusive before authorization/path-policy checks; excess returns `input_limit_exceeded`, with no partial execution. |
| Tree entries | Maximum 2,000 inclusive after authorization/path filtering; partial metadata allowed with `isTruncated = true`. |
| Search/glob results | Maximum 500 inclusive after authorization/path-policy filtering; partial metadata with `isTruncated = true`; client must refine. |
| Single bounded range | Maximum 262,144 bytes inclusive after authorization/path-policy checks; excess returns `input_limit_exceeded`. |
| Aggregate context-query response | Maximum 1,048,576 serialized bytes inclusive after authorization/filtering; truncate only for families that support it, otherwise `response_limit_exceeded`. |
| Query duration | Maximum 2 seconds of server-side execution after validation/authorization start; `query_timeout`; no automatic handler retry. |
| Truncation semantics | One `isTruncated` flag per response after authorization/path filtering; file content is never silently truncated. |
| Audit visibility | Include family, configured limit, actual count/bytes, elapsed time, truncation, category, correlation ID; exclude raw query text, file content, path lists, and unauthorized existence. |

PRD anchors:

- Replace the C4 TBD row.
- Bind **Performance and Query Bounds**, FR34-FR35, and context-query done conditions to these values.
- Do not mistake the 262,144-byte bounded-read limit for a general file-size/large-file acceptance policy; that remains separate.

### 3. C9 is approved and has a defined default

Status: **approved**. Governance title: “Sensitive metadata classification evidence.” The current gate authority is Safety Invariants; canonical evidence is `tests/fixtures/audit-leakage-corpus.json` plus the safety-channel inventory/gate.

Product policy fixed by Architecture S-6/C9:

- Paths, repository names, branch names, and commit messages are `tenant-sensitive` by default.
- They may be visible to authorized tenant members and operators with a defined need-to-know scope.
- They are redacted in cross-tenant operator views and external diagnostics.
- A per-tenant override may raise the classification to `confidential`; confidential values are hashed at audit/projection write time rather than stored or emitted in clear text.
- Redacted, unknown, missing, hidden/unauthorized, stale, and unavailable are distinct states. Redaction must be visible, not silent.

Canonical sources: Architecture **S-6 Sensitive metadata classification (C9)** and concern 17; governance YAML C9; `tests/fixtures/audit-leakage-corpus.json`; `docs/contract/safety-invariant-ci-gates.md`.

PRD anchors: **Observability, Auditability, and Replay**, FR17, FR21, FR39, FR52-FR58, error-detail rules, and the console boundary.

Important scope distinction: C9 being approved does **not** approve body-text indexing. Full-content materialization still requires its separately recorded Security + PM sign-off.

### 4. No incoming webhook ingestion in MVP

Authoritative posture:

- No webhook ingestion in MVP.
- Routes that would receive webhooks return `404 Not Found`.
- A post-MVP webhook capability requires tenant-routing design first.

Canonical source: Architecture hard boundary, concern 5, **Webhook posture** (`architecture.md:94`, `806-807`).

PRD anchor: remove the webhook/callback duplicate-delivery bullet from **MVP Acceptance Evidence**. Keep tenant scope and replay safety for asynchronous internal/provider work without calling it webhook ingress. Add webhook ingestion to explicit post-MVP non-goals.

### 5. Console is projection-first with a narrow incident-mode exception

Normal product behavior remains read-only and projection/read-model based. The approved exception is a last-resort, ACL-checked event-stream view at `/_admin/incident-stream` when projections are degraded.

Required incident-mode behavior already fixed in Architecture F-6:

- permission gate: `eventstore:permission=admin` plus the normal tenant/folder access boundary;
- read-only, metadata-only, C9 redaction still enforced;
- persistent degraded-mode warning that events may be incomplete/out of order and shows the last projection checkpoint;
- operator-disposition labels shown beside event types;
- copyable correlation ID plus timestamp window;
- no mutation, repair, credentials, file content, raw diffs, or unrestricted event/filesystem access.

PRD anchors: qualify **Public Surfaces**, FR36, FR52-FR56, and **Observability, Auditability, and Replay**. State projections as the normal authority and the event stream as a visibly degraded incident aid, not a second ordinary console data source.

### 6. Minimum current-release surfaces

The current product contract requires:

- the Contract Spine and REST public transport;
- the generated typed SDK;
- CLI and MCP for the core canonical lifecycle, preserving behavioral parity;
- the read-only operations console as the diagnostic surface.

Current Architecture, C13, the MVP Feature Set, and current release planning all treat REST/SDK/CLI/MCP as required. The PRD’s “REST and one SDK first” sentence is a contingency if a later explicit scope-change decision is made; it is not the current minimum scope.

PRD anchors: **MVP Contract Summary**, **Technical Success**, **MVP Strategy**, **MVP Feature Set**, FR47-FR51, and **Risk Mitigation Strategy**. Say “core lifecycle on all four contract surfaces”; keep console query-only. Do not promise every diagnostic query in the CLI merely because the generated oracle advertises the adapter unless the product explicitly wants that broader parity.

### 7. Existing provider-repository binding is MVP; migration-style adoption is not

Current authoritative behavior:

- `BindRepository` is an MVP-approved mutating command in `docs/contract/idempotency-and-parity-rules.md` and a generated C13 row.
- Current Epic 3 Story 3.7 defines it as binding an existing provider repository to an existing logical Folders folder after provider readiness, repository-access validation, and branch/ref compatibility.
- It records binding metadata and must not expose unauthorized repository existence.

Safe brownfield boundary for the PRD:

- **In scope:** bind a pre-created provider repository that passes readiness, access, duplicate-binding, and branch/ref-policy checks to an existing logical Folders folder.
- **Out of scope:** importing/adopting an arbitrary local working tree or unmanaged folder, a migration wizard, automatic repair/reconciliation of pre-existing dirty state, history rewriting, or local-first promotion.

PRD anchors: **MVP Contract Summary**, **Product Scope**, FR19, and **Explicit MVP Non-Goals**. Replace the unqualified phrase “brownfield folder or repository adoption” with this explicit distinction.

The sources do not settle every repository eligibility edge (see open item B2).

### 8. Tenant-administration capabilities belong in the current MVP contract

Journey 9 and FR4-FR14 already make this product scope. The current C13 operation inventory includes `UpdateFolderAclEntry`, `ListFolderAclEntries`, `GetEffectivePermissions`, `ArchiveFolder`, and `GetFolderLifecycleStatus`.

Reconcile the MVP Feature Set and Acceptance Evidence to require:

- grant **and revoke** folder access for users, groups, roles, and delegated service agents;
- inspect effective permissions without revealing hidden principals or folder existence;
- archive a folder;
- deny all subsequent archived-folder mutations with a stable result;
- retain metadata-only lifecycle, audit, lock, timeline, and last-commit evidence per C3;
- preserve REST/SDK/CLI/MCP semantics for these contract operations.

PRD anchors: Journey 9, FR4-FR6, FR11-FR14, **MVP Feature Set**, and **MVP Acceptance Evidence**. Change FR5 from grant-only to grant and revoke.

### 9. FR58 current meaning is authorized metadata-token recall, not body-content search

Authoritative current-release posture:

- FR58 is in the current 58-FR inventory and current-release scope.
- The present increment indexes curated text/attributes derived only from mutation metadata evidence (type/size classification, media type, folder/organization identity, path-policy outcome).
- It must expose no raw path, file body, snippet, or source URI.
- Retrieval is authorized before egress, security-trimmed to tenant/folder/workspace, hydrated against current Folders authority, and metadata-only.
- Current C13 operations are `SearchFolderIndexedFiles` and `GetFolderIndexingStatus`.
- Authorized real body-content materialization is a separate follow-up requiring Security + PM sign-off; the separate RAG ingestion path is not MVP scope.

Canonical sources: Architecture **Content materialization**, **Query Facade**, FR58 reconciliation note; current Epic 10 Stories 10.5-10.6; current parity oracle.

PRD fix:

- Rename/rewrite FR58 so its headline promises **authorized metadata-token recall over indexed mutation metadata**, not “search the content.”
- Give it user-visible done conditions: only currently authorized/live indexed units appear; stale/archived/unauthorized hits are dropped; results contain classification/status/opaque authorized identity only; zero raw path/body/snippet/source URI; unavailable indexing/facade is visible and fail-safe.
- Put full body-text search in a separate post-MVP/gated requirement.
- Remove Epic/story/wiring/live-DCP status from the PRD.

The current evidence supports “current-release Phase 2 extension,” not treating full body-content retrieval as part of the core repository-workflow MVP.

### 10. State vocabulary is fixed by C6 and must be separated by dimension

Canonical workspace lifecycle states (lowercase wire vocabulary):

`requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, `reconciliation_required`.

Canonical paired operator dispositions:

- `requested`, `preparing`, `committed` → `auto-recovering`;
- `locked`, `changes_staged` → `degraded-but-serving`;
- `dirty`, `unknown_provider_outcome`, `reconciliation_required` → `awaiting-human`;
- `failed`, `inaccessible` → `terminal-until-intervention`;
- `ready` → available, or `degraded-but-serving` when freshness exceeds C2.

Canonical lock-state dimension from `docs/contract/workspace-lock-contract-groups.md`:

`unlocked`, `locked`, `expired`, `stale`, `revoked`.

Do not mix the lock dimension with workspace lifecycle. Generic operation execution labels such as `Pending/InProgress/Succeeded/Failed/Cancelled` are not substitutes for C6 workspace state and should be separately named if retained.

Canonical sources: Architecture **Workspace State Transition Matrix (C6 — Enumerated)**; `docs/exit-criteria/c6-transition-matrix-mapping.md`; workspace/lock contract groups.

PRD anchors: add a concise glossary/state table; update **Workspace State and Concurrency**, FR28, FR31, FR40, FR45-FR46, lifecycle NFRs, journeys, console language, and error/status prose.

### 11. Authorization revocation has a fail-closed product outcome

The fixed behavior is:

- every mutation requires a held lock and fresh authorization revalidation;
- authorization is revalidated on every mutating call;
- mutations do not use bounded-stale tenant access during Tenants degradation: they synchronously obtain fresh authority or reject;
- detected revocation changes the held lock to revoked/inaccessible, is audited, and causes subsequent mutations to return `tenant_access_denied`/the canonical revocation category without touching files, repository, provider, commit, or audit resources;
- provider credential revocation/provider inaccessibility maps to the `inaccessible`/known-failure path rather than blind retry.

For asynchronous work, the safe product implication is that authorization must still be valid when a worker is about to perform a side effect; authorization only at command receipt is insufficient. This follows from the “every mutation” and fresh-authorization rules, although the worker mechanism belongs outside the PRD.

Canonical sources: Architecture concerns 16 and 20, C6 transitions, locking process pattern; project context safety invariants; workspace/lock contract revocation outcomes.

PRD anchors: **Authentication and Authorization Model**, FR8-FR10, FR24-FR29, Security NFRs, Reliability NFRs. The numeric revocation/freshness SLO remains open under C7 (B3).

### 12. Safe error details are metadata-only and non-enumerating

Current canonical error extension shape is:

`category`, `code`, `message`, `correlationId`, optional `taskId`, `retryable`, `clientAction`, and `details.visibility`.

Product constraints safe to state now:

- `details` is not an arbitrary payload bag; it is closed/bounded by the Contract Spine for each error and remains metadata-only.
- Never include secrets, tokens, raw file content, diffs, provider payloads, local absolute paths, or unauthorized resource existence.
- Authorization is evaluated before state-specific details.
- The canonical 404 safe-denial envelope is byte-identical across absent, cross-tenant, missing binding, missing policy, and equivalent protected-resource cases.
- Redacted is visibly distinct from unknown/missing; a redacted wrapper cannot also carry a cleartext value.

Canonical sources: `_bmad-output/project-context.md:79,116,141`; `docs/contract/tenant-folder-provider-repository-contract-groups.md`; `docs/contract/workspace-lock-contract-groups.md`; audit/ops-console contract groups.

PRD anchors: **Error Codes**, FR43-FR46, Security NFRs, and cross-surface parity. Keep the exact per-code schema in the Contract Spine, not duplicated in the PRD.

### 13. Gate denominators must come from canonical inventories

Safe current denominator rules:

- **Cross-surface operation/parity coverage:** every operation in the current C0 Contract Spine must have exactly one C13 row. The generated oracle currently has 49 rows. Additions without a row and undeclared removals fail the gate. Use “all current Contract Spine operations,” not a hard-coded `47/47` or an “at least” DTO list.
- **Idempotency completeness:** every current/future mutating Contract Spine operation must declare a key rule, equivalence semantics, TTL tier, same-intent replay outcome, conflicting-intent outcome, correlation behavior, and task identity behavior.
- **Read completeness:** every non-mutating operation must reject idempotency keys and declare read consistency, safe denial, audit metadata, correlation behavior, and projection expectation.
- **Provider contract coverage:** both currently supported providers, GitHub and Forgejo, must pass the declared MVP lifecycle/failure capability matrix; hermetic PR evidence and live drift evidence are separate modes.

PRD anchors: **Contract and Quality Gates**, **Verification Expectations**, and **MVP Acceptance Evidence**. Remove the partial DTO list as a denominator or label it illustrative and point to C0/C13.

The ACL-matrix denominator is not currently authoritative (B6); do not retain “100% ACL matrix” without a linked normative inventory.

### 14. Sensitive-metadata defaults are no longer open

Use the C9 policy from A3 everywhere the PRD currently says “potentially sensitive” or “where appropriate.” Specifically, paths, repository names, branch names, and commit messages default to tenant-sensitive; confidential is a stricter per-tenant override; provider payload bodies, file content, secrets, and generated context remain forbidden rather than merely classified metadata.

PRD anchors: **Observability, Auditability, and Replay**, FR38-FR39, FR52-FR58, operator views, incident mode, and FR58.

### 15. Canonical technical targets available for success criteria

Existing numeric release targets can be promoted into stable SM/NFR references:

- zero tolerated cross-tenant leaks (PRD invariant);
- command acceptance acknowledgement: 1 second p95 for bounded inputs (PRD/Architecture budget);
- bounded status/audit summary execution: 500 ms p95 (PRD/Architecture budget);
- context-query execution: 2 seconds p95, with the separate C4 hard timeout of 2 seconds;
- C2 freshness: maximum 500 ms commit-to-status-read visibility lag in the defined hermetic release-calibration path;
- C1 capacity: 4 concurrent tenants; 2 folders/tenant; 2 active workspaces/tenant; 2 concurrent agent tasks/tenant;
- C5: the same scale units plus at least 1 lifecycle operation/second.

Canonical sources: PRD/Architecture performance budgets; `docs/exit-criteria/c1-capacity.md`, `c2-freshness.md`, `c4-input-limits.md`, `c5-scalability-quantifiers.md`.

Do not conflate query execution latency with C2 projection freshness. Strategic adoption, task-completion, integration-effort, and operator-burden targets are not canonically set (B7).

### 16. Idempotency coverage is all mutations, not a hand-picked lifecycle subset

The current C13 oracle has 14 mutating operations:

`AddFile`, `ArchiveFolder`, `BindRepository`, `ChangeFile`, `CommitWorkspace`, `ConfigureBranchRefPolicy`, `ConfigureProviderBinding`, `CreateFolder`, `CreateRepositoryBackedFolder`, `LockWorkspace`, `PrepareWorkspace`, `ReleaseWorkspaceLock`, `RemoveFile`, `UpdateFolderAclEntry`.

Normative behavior:

- every mutating command requires `Idempotency-Key` and tenant-scoped semantic equivalence;
- same key + equivalent intent returns the same logical result without duplicate events, provider writes, file changes, repositories, commits, audits, or idempotency records;
- same key + different intent returns `idempotency_conflict` without revealing prior protected metadata;
- mutation TTL is 24 hours; commit TTL is C3/audit retention (7 years);
- unknown provider outcome enters `unknown_provider_outcome`/`reconciliation_required` rather than blind retry;
- non-mutating operations must not accept `Idempotency-Key`;
- any future cleanup mutation is automatically covered by the same all-mutations rule even though the current Contract Spine exposes cleanup status, not a public cleanup command.

Canonical sources: Architecture A-9/D-7/process patterns; `docs/contract/idempotency-and-parity-rules.md`; `_bmad-output/project-context.md:143-146`; current parity oracle.

PRD anchors: replace the incomplete operation-specific table and NFR subset with the algorithmic all-mutations rule plus a link to C0/C13. Product outcomes belong in the PRD; per-command equivalence field lists, hash ordering, parser normalization, and SDK helper generation do not.

## B. True unresolved product decisions/open items

These must remain explicit open items with owner and blocking consequence. The sources do not justify silently inventing values.

### B1. Cross-tenant platform-operator authority model

Known floor: all access is fail-closed; normal console access is tenant/folder authorized; C9 allows “operators-with-need-to-know”; incident view requires `eventstore:permission=admin`; all privileged reads are metadata-only and audited.

Unresolved: which role may select/search across tenants, whether tenant consent is required, scope/time limits, which fields each operator audience may see, and how break-glass/need-to-know access is distinguished and reviewed. `admin` alone is not a sufficient product authority model.

Owner: Security + PM. Blocks trustworthy operator journey/acceptance wording for cross-tenant use.

### B2. Exact eligibility for binding a pre-created provider repository

Known floor: readiness, provider access, duplicate-binding checks, and branch/ref compatibility must pass; local-folder import/migration is out.

Unresolved: explicit policy for empty vs history-bearing repositories, default/missing branches, detached or unusual refs, existing protected branches, large repositories, submodules/LFS, and repositories already bound through an alias. The PRD should either define these user-visible eligibility outcomes or link a future approved repository-binding policy.

Owner: PM + Provider/Architecture. Blocks fully testable FR19 done conditions, but does not reopen the existence of `BindRepository` in MVP.

### B3. C7 lock lease/auth-revalidation numbers

Governance status is `reference_pending`, not approved. The architecture fixes a two-number contract (lease-renewal interval and authorization-revalidation interval) tied to a revocation-effect SLO, but no binding numbers exist.

Owner: Architecture. The PRD can state the fail-closed outcome now but must leave the numeric freshness SLO explicit and linked to C7.

### B4. Authoritative lock collision identity

Current documents variously say tenant/folder/workspace, repository binding, task, and lock ID. They do not decide how two folders/bindings/ref aliases targeting the same provider repository/ref collide.

Required product decision: the serializing identity and alias rule across managed tenant, provider/repository identity, ref/branch, workspace, and task. It must prevent two aliases from concurrently mutating the same effective repository/ref while permitting genuinely independent work where intended.

Owner: Architecture + PM. Blocks a complete FR25-FR29/lock invariant and concurrency acceptance test.

### B5. Meaning of successful commit / remote durability

The working copy is explicitly disposable and non-authoritative, but no reviewed source defines whether `CommitWorkspace` success means a local Git commit, provider-side commit, or confirmed durable update of the bound remote/ref. The provider-backed product promise does not by itself settle this.

Required product decision: what durable outcome must be confirmed before the state becomes `committed`, what evidence is returned, and what state applies when the remote outcome is unknown. The existing safe rule is only that unknown outcome enters reconciliation and is never blindly retried.

Owner: PM + Architecture/Provider. Blocks FR37-FR39 and the canonical demo’s “persisted” done condition.

### B6. Normative ACL-matrix denominator

There is no canonical, linked actor × operation × tenant/folder/task relation matrix behind the PRD’s “100% ACL matrix coverage.” Operation-level auth categories and safe denials exist, but they are not the denominator the claim needs.

Required decision/artifact: name the authoritative authorization matrix, including tenant administrator, ordinary member, delegated agent, operator, auditor, wrong-tenant, revoked, stale, and hidden-resource cases for every protected operation family.

Owner: Security/Authorization. Until then, remove or qualify the percentage claim.

### B7. Strategic success and counter-metric targets

No canonical sources set a task-completion target/denominator/window, adoption cohort, time-to-integrate target, operator-burden ceiling, dirty/reconciliation accumulation ceiling, or provider-failure counter-metric. C1/C2/C4/C5 are technical release targets, not proof of adoption or customer value.

Owner: PM. Blocks a real product go/no-go success section, not implementation of the fixed MVP contract.

### B8. File-policy outcome authority

Known fixed boundaries: Folders owns folder/path policy; authorization and path policy run before query/mutation; workspace-root confinement, traversal rejection, include/exclude, symlink, Unicode/case, encoding, binary, reserved-name, large-file, and collision concerns must be deterministic and cross-surface consistent.

Still unresolved:

- no approved product artifact defines exact allow/reject/normalize behavior for symlinks, Unicode normalization, case collisions, encoding, binary files, large files, or include/exclude precedence;
- `docs/contract/file-context-contract-groups.md` explicitly leaves the closed `PathPolicyClass` vocabulary reference-pending;
- the final `416 redacted` vs byte-identical `404` safe-denial routing is also reference-pending.

Owner: Architecture + Security + PM where behavior is user-visible. Do not use D-9’s 256 KB inline transport split or C4’s 256 KB bounded read as a substitute for this policy.

## C. Implementation mechanisms that must stay out of the PRD

Keep the product outcome/canonical artifact reference, but move or leave these mechanics in architecture/contracts/addendum/implementation artifacts:

- NSwag generation, generated `ComputeIdempotencyHash()`, lexicographic field ordering, exact equivalence-field lists, NFC/hash encoding mechanics, and CLI key-generation switches.
- ASP.NET endpoint layout, controller/minimal-API choices, Dapr app IDs/service invocation, pub/sub topics, sidecars, Redis/Postgres choices, worker/process-manager registrations, and EventStore handler/project names.
- Exact `/_admin/incident-stream` component/page implementation, Fluent UI component names, banner component, and copy-button wiring; retain only the user-visible incident-mode guarantees in the PRD.
- Memories client classes, Dapr routing, bridge projection type names, materializer classes, CloudEvent routing/source names, Story 10.6/11.10 sequencing, DCP evidence, and delivery status. Retain only FR58’s authorized metadata-token product behavior and unavailable/fail-safe outcome.
- Inline-vs-stream REST mechanics, multipart/media types, `413` retry headers, and SDK convenience methods. Retain only user-visible size/error behavior where it is a product constraint.
- Redis lock/idempotency storage, lease scheduler implementation, tenant event handlers, and exact revocation worker mechanics.
- Provider adapter libraries (Octokit/typed HttpClient), fixture paths, live-nightly job wiring, and schema-diff tooling. Retain supported providers and capability/failure outcomes.
- Exact OpenAPI property composition and generated schema internals. The PRD may name stable public fields/outcomes and link the Contract Spine.

## Recommended PRD edit sequence

1. Correct document posture/frontmatter and contract-authority wording.
2. Reconcile MVP/current-release scope: surfaces, pre-created repository binding, tenant administration, no webhooks, console incident exception, and FR58.
3. Replace C3/C4/C9 stale policy wording and add stable C6 vocabulary/glossary.
4. Tighten FR done conditions for authorization, archive, locks, commit, errors, gates, and idempotency.
5. Add an explicit Open Items section for B1-B8 with owner and blocked behavior.
6. Rebuild success measures using available technical targets while leaving product-value targets visibly open.

