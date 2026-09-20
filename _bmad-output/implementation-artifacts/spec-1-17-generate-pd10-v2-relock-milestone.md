---
title: 'Generate the PD10 v2 relock candidate set'
type: 'feature'
created: '2026-09-19'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '3f1056d998ac4688f36eb869c516812c1a4ddb71'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Rank-4 milestone `1.17-GENERATE` has approval to generate, but the repository still exposes a v1-only Contract Spine and legacy authorization/error behavior that cannot serve as the digest-bound PD10 v2 candidate for A6b.

**Approach:** Generate the complete v2 contract, server, SDK, CLI, MCP, UI, previous-spine, C13, documentation, drift-gate, and focused-test candidate set in lockstep, then emit an exact-path/SHA-256 conformance manifest without exposing or approving it.

## Boundaries & Constraints

**Always:** Preserve v1 and historical OQ3 evidence; cover exactly fourteen canonical access states and all eleven protected families; authenticate and establish fresh authority before any protected lookup/read; prove folder scope and task binding; use canonical 401/404/503 envelopes with closed visibility/action/error/exit/failure vocabularies; keep generated files generator-owned; preserve all pre-existing worktree changes. Stop and escalate if a deployed external v1 consumer is discovered.

**Never:** Route or publish v2; close Story 1.17 or any ordinary story; infer or record A6b/A8; remove the execution hold; modify/regenerate `sprint-status.yaml`; generate OQ5–OQ13 runtime evidence; edit generated SDK/parity rows by hand; alter deployment/release exposure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Unauthenticated | Any protected v2 operation | No protected lookup; canonical `401` | `authentication_failure` / `authentication_required` / `check_credentials` / `redacted` |
| Fresh negative authority | Wrong tenant, revoked, hidden, absent, disabled, unknown, or insufficient scope | Zero protected reads; byte-equivalent `404` | `tenant_access_denied` / `resource_unavailable` / `no_action` / `redacted` |
| Unusable authority | Stale, unavailable, conflicting, or incomplete evidence | Zero protected reads; retryable `503` | `read_model_unavailable` / `projection_unavailable` / `retry` / `redacted` |
| Scoped diagnostics/task | Folder-scoped diagnostic or task status | Fresh folder authority precedes lookup; task belongs to folder | Scope/binding failure collapses to canonical `404`; authority failure to canonical `503` |
| Adapter conflict | `concurrency_conflict` | CLI exit `77`; MCP kind `concurrency_conflict` | No legacy caller-visible outcome |
| Read-model outage | `read_model_unavailable` | CLI exit `73`; closed MCP mapping | Retry metadata retained without disclosure |

</frozen-after-approval>

## Code Map

- `docs/contract/authorization-matrix.md` -- finalize the existing `2.0.0-candidate.1` denominator as candidate version `2.0.0`; do not edit `oq3-authorization-evidence.yaml`.
- `src/Hexalith.Folders.Contracts/openapi/` -- retain v1 byte-stable; add `hexalith.folders.v2.yaml` and update extension references/tests to consume v2.
- `src/Hexalith.Folders.Server/{Program,FoldersDomainServiceEndpoints,ProviderReadinessEndpoints,OpsConsoleDiagnosticsEndpoints,AuditEndpoints}.cs` and authorization mappers -- candidate-only policy, ordering, scope, binding, and canonical envelopes; do not production-route v2.
- `src/Hexalith.Folders.Client/{Hexalith.Folders.Client.csproj,nswag.json,Generation/,Generated/}` -- switch the deterministic full generation pipeline to v2; never hand-edit `.g.cs`.
- `src/Hexalith.Folders.{Cli,Mcp,UI}/` -- consume generated v2 contracts and canonical projections; UI retains distinct denial/unavailable states using Fluent/FrontComposer patterns.
- `tests/tools/parity-oracle-generator/`, `tests/fixtures/{previous-spine.yaml,parity-contract.schema.json,parity-contract.yaml}` -- add status/error fingerprints and regenerate C13 deterministically; never hand-edit parity rows.
- `tests/Hexalith.Folders.*.Tests/` and `docs/{contract,sdk}/` -- focused conformance, zero-read, closed-vocabulary, consumer, and drift evidence.
- `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- deterministic inventory of every candidate artifact path and raw-byte SHA-256 digest for A6b review only.

## Tasks & Acceptance

**Execution:**
- [x] Contract/matrix files -- regenerate the v2 surface with 49 unique method/path identities, a non-duplicating server base/path composition, all v1 request capabilities preserved, operation-specific error outcomes retained alongside exact canonical authorization branches in every response union, and examples validated against their fully resolved declared schemas. The canonical 401/404/503 authorization envelopes and visibility/action/error vocabularies must be normative closed schemas, not descriptive extensions over permissive `ProblemDetails`.
- [x] Server/auth files -- make the isolated v2 candidate host execute a total, explicit method-and-route operation descriptor before every protected lookup. Each of the 49 identities must declare its family grant, action token, read/mutation policy class, folder scope, and task-binding rule; opaque identifiers and unknown routes must never influence or fall through to another identity. Preserve the working real executor, folder diagnostic scope, task binding, structural JSON rewrite, and segment boundary. Canonicalization must fail closed on malformed legacy bodies, convert authorization-accessor failures to exact 503, preserve operation-specific post-authorization 503s, and remove noncanonical denial headers. Keep the seam opt-in and production composition unmodified.
- [x] SDK/CLI/MCP/UI files -- regenerate the .NET client and migrate every consumer, mapping, reference, and conformance test in lockstep, including CLI 73/77 and MCP `concurrency_conflict`. Remove silent adapter parameters not present in the operation contract; preserve every valid v1 request vocabulary; verify effective-permissions task context and `details.visibility` at CLI/MCP wire boundaries plus null task context in non-task UI calls.
- [x] C13/gate/docs files -- reject any generator output that aliases immutable v1, read each manifest input once for both size and digest, validate stored status/error fingerprints against v1, reject duplicate generated routes, reproduce v2 byte-for-byte, run resolved-schema example validation, use canonical adapter fixtures, assert the real non-routing seam symbol, render-test all UI dispositions, and migrate the API/error/exit documentation plus its consumer tests to the 49-operation nine-action v2 surface. Full Contracts and CLI suites must pass, not only selected candidate classes.
- [x] Conformance-set artifact -- independently assert the complete candidate path set, including every candidate-affecting generator, gate, workflow, and `run-governance-completeness-gates.ps1`; then regenerate exact path/SHA-256 inventory only after all outputs and full affected suites are stable. The rejected 75- and 123-artifact digests must not be reused.

**Acceptance Criteria:**
- Given the v2 candidate set, when focused contract and adapter gates run, then each of 49 operation identities appears exactly once with all required scopes, status/error fingerprints, and parity cells, and forbidden protected outcomes are absent.
- Given every denial/authority-unavailable test double, when a protected endpoint is invoked, then authentication/authorization completes before resource, provider, content, audit, task, count, filter, or search access and protected read counters remain zero.
- Given candidate generation completes, when the conformance-set is verified, then every listed path exists and every SHA-256 matches, while v1, the hold, lifecycle tracking, approval records, and production exposure remain unchanged.

## Implementation Notes

- The rejected review input remained non-routed and unpublished, and it correctly preserved `Program.cs`, supported deployment profiles, the historical v1 Spine, OQ3 evidence, execution hold, and sprint status. Preserve those properties during re-derivation.
- Preserve the working 49-operation, 14-access-state, and 11-family denominators; CLI exits 73/77; closed MCP `concurrency_conflict`; distinct UI denial/unavailable dispositions; v1 byte stability; and deterministic SDK/parity generation.
- The earlier 75-artifact manifest (`649ecf...` / candidate set `535c67...`) is historical rejected evidence: review proved that its executable seam bypassed v2 scope/binding checks and that its allowlist could omit candidate-affecting files. Regenerate and report new digests after correction; do not modify or infer approval from the concurrent approval-register work.
- Preserve from review loop 2 the real `Pd10ProtectedOperationExecutor` integration, task-to-folder comparison, folder-scoped diagnostic rewrite, exact authorization envelope types, structural request transformation, duplicate-route rejection, independent path-set assertion, generated v2 SDK, adapter error projection, UI disposition rendering, and unchanged production `Program.cs`; replace only the heuristic or incomplete parts identified by the triage log.

## Spec Change Log

- 2026-09-19: Generated and verified the complete non-routed PD10 v2 candidate, closed the generated CLI/MCP vocabulary gaps found during independent audit, and sealed the final 75-artifact conformance digest for A6b review.
- 2026-09-20: Review loop 1 rejected the sealed candidate because the isolated executable seam bypassed the new authorization executor and discarded folder scope/task binding, while generator/schema/drift checks accepted noncanonical or incomplete outputs. Amended the execution tasks to require executable authorization ordering, exact normative envelopes, preserved v1 request/error capabilities, safe historical-baseline handling, independent generation/manifest checks, and missing adapter/UI verification. Known-bad state avoided: self-consistent hashes and green isolated tests over a candidate that could read through v1 without v2 scope enforcement. KEEP: the non-routed production boundary, v1/OQ3/sprint stability, exact 49/14/11 denominators, CLI 73/77, MCP concurrency mapping, UI state distinction, deterministic generation, and all unrelated concurrent planning work.
- 2026-09-20: Review loop 2 rejected the re-derived candidate because the executable seam used verb/subpath heuristics instead of a total operation identity table, assigning ordinary metadata or folder-create grants to console, audit, ACL, and POST query families; the same review found incomplete exact-503 schema branches, unsafe generator output aliasing, an omitted v2-consuming governance gate, stale v1/47-operation docs tests, and four red full-suite tests. Amended the execution tasks to require an explicit 49-operation authorization descriptor, fail-closed exception/body/header handling, resolved-schema example validation, complete docs/test migration, v1 output protection, single-read manifests, complete gate inventory, and full affected-suite verification. Known-bad state avoided: focused green tests and a sealed digest over incorrectly authorized operations and red normal CI suites. KEEP: the real executor and binding checks, folder-scoped rewrites, closed authorization envelopes, structural JSON rewrite, exact 49/14/11 denominators, deterministic generators, CLI/MCP/UI projections, non-routing boundary, v1/OQ3/sprint stability, and all unrelated concurrent planning work.

## Review Triage Log

| ID | Verdict | Route | Evidence |
|---|---|---|---|
| BH-01 | false | reject | The shared client targets v2, but the reviewed artifact is explicitly non-routed and unpublished; the claimed production 404 requires the forbidden later publication/exposure action. |
| BH-02 | high | bad_spec | Repository references show `Pd10ProtectedOperationExecutor` is used only by its unit test, while executable candidate requests use the compatibility seam and historical endpoints. |
| BH-03 | high | bad_spec | `CandidatePathToHistoricalPath` drops `folderId` from task status, so the executable candidate cannot establish the required task-to-folder binding before lookup. |
| BH-04 | high | bad_spec | Both diagnostic rewrites drop `folderId` and forward to tenant-scoped v1 endpoints, bypassing the frozen folder-scope requirement. |
| BH-05 | medium | bad_spec | The seam's 404 type/title/message differ from `SafeDenial404NotFound` and it substitutes request correlation, so its output is not the claimed canonical byte-equivalent envelope. |
| BH-06 | high | bad_spec | `transform_operation` replaces every operation's 503 response with authority-unavailable, erasing operation-specific 503 response contracts while still advertising their categories. |
| BH-07 | high | bad_spec | Canonical responses reference permissive base schemas rather than const-constrained exact-envelope schemas, allowing noncanonical authorization bodies to validate. |
| BH-08 | high | bad_spec | `ProblemDetails.code` is pattern-only, visibility lacks a value enum, and additional properties remain allowed, contradicting the claimed closed normative vocabulary. |
| BH-09 | medium | bad_spec | `PrincipalMismatchSafeDenialProblem` retains forbidden `not_found` and undeclared `verify_authorization`, making the generated example inconsistent with its schema. |
| BH-10 | medium | bad_spec | `do_not_retry` and `restart_query` remain in examples but not in the generated client-action enum, so the candidate is internally inconsistent. |
| BH-11 | medium | bad_spec | A nested `attemptedTransition` object violates the base details value schema and cannot be represented by the generated `Dictionary<string,string>`. |
| BH-12 | high | bad_spec | `--initialize-baseline` defaults to the v2 contract yet labels and overwrites the result as historical v1, allowing destruction of the protected baseline. |
| BH-13 | medium | bad_spec | Historical status/category fingerprints are checked only for non-emptiness and never compared with the byte-stable v1 source, so arbitrary fingerprint drift passes. |
| BH-14 | high | bad_spec | The hand-maintained manifest omits candidate-affecting workflow/gate files while its self-referential test can still declare the set complete. |
| BH-15 | medium | bad_spec | Cross-adapter fixtures set noncanonical codes and omit required details, so green parity tests can certify invalid protected envelopes. |
| BH-16 | medium | bad_spec | The OpenAPI server URL and every path both contain `/api/v2`, yielding `/api/v2/api/v2` for standards-compliant URL composition. |
| BH-17 | false | reject | Legacy UI tokens remain relevant only to the unchanged routed v1 release; the reviewed UI is an unpublished v2 candidate, so the asserted production regression is outside the allowed execution state. |
| BH-18 | medium | patch | Literal whitespace-sensitive replacement rejects valid JSON formatting in the isolated seam; structural JSON rewriting directly fixes the demonstrated input. |
| EC-01 | false | reject | Same claim as BH-01: no production publication or routing occurs in this story, so the necessary trigger is excluded. |
| EC-02 | medium | bad_spec | Same verified URL-composition defect as BH-16; the server base and operation paths duplicate the version prefix. |
| EC-03 | low | patch | Prefix-only matching also captures `/api/v20`; an exact segment-boundary check is a direct private-seam correction. |
| EC-04 | medium | patch | Same verified formatting defect as BH-18; tabs/newlines bypass the two literal replacements. |
| EC-05 | high | bad_spec | Historical endpoints can emit `policy_evidence_unavailable`, which v2 removed; the seam forwards 503 unchanged and the generated enum cannot deserialize it. |
| EC-06 | medium | patch | The Python generator overwrites an existing method at the same candidate path and still marks both IDs observed; an explicit duplicate-route failure is required. |
| EC-07 | high | bad_spec | v2 narrows six valid release reasons to `caller_completed`, preventing abandon/cancel/revocation lock-release requests and risking stuck locks. |
| EC-08 | high | bad_spec | Verified with BH-07/BH-08: permissive Problem Details accepts fields and visibility values outside the claimed exact closed contract. |
| EC-09 | medium | bad_spec | Verified with BH-09: the retained principal-mismatch example uses a forbidden category and invalid action. |
| EC-10 | high | bad_spec | Verified with BH-02: the new authorization executor has no executable endpoint or seam caller. |
| EC-11 | high | bad_spec | Verified with BH-14: candidate completeness is defined and tested from the same incomplete allowlist. |
| EC-12 | false | reject | `git diff HEAD` and the implementation-agent timeline identify the approval-register edit as concurrent external planning work; Story 1.17 did not modify it, and the record still leaves A6b pending. |
| VG-01 | high | bad_spec | Pre-verified: candidate task and diagnostics calls lose their folder parent at the seam and no executable test proves authorization/binding before reads. |
| VG-02 | medium | patch | Pre-verified: the non-routing test does not check the actual `UsePd10V2CandidateCompatibilitySeam` symbol, so enabling the seam would leave it green. |
| VG-03 | high | patch | Pre-verified: manifest tests replay the generator's own list and lack an independent expected count/path set, so omissions remain self-consistent. |
| VG-04 | medium | patch | Pre-verified: no test runs `generate-pd10-v2-contract.py` and byte-compares its output, allowing a broken generator beside a valid stale contract. |
| VG-05 | medium | patch | Pre-verified: model classification is tested but rendered disposition labels/attributes are not, so operator guidance can regress undetected. |
| VG-06 | medium | patch | Pre-verified: CLI/MCP forwarding and UI omission of optional effective-permissions task context have no exact wire assertions. |
| BH2-01 | high | bad_spec | `ActionToken` assigns every ops-console GET the ordinary `read_metadata` token, so the executable seam does not enforce the matrix's console-view/operator grant. |
| BH2-02 | high | bad_spec | Audit-trail and operation-timeline GETs also resolve to `read_metadata`, leaving the matrix's audit-reviewer family grant unenforced. |
| BH2-03 | high | bad_spec | `ListFolderAclEntries` falls through to `read_metadata` even though the governing matrix requires a distinct folder `administer` grant. |
| BH2-04 | high | bad_spec | All five POST context-query routes fall through to `create_folder`, authorizing with the wrong family grant in both permissive and restrictive directions. |
| BH2-05 | high | bad_spec | Every non-GET folder route receives `LayeredFolderOperationPolicy.Mutation()`, including read-only POST context queries; tenant-scoped POST reads also use mutation authorization. |
| BH2-06 | high | bad_spec | Authorization selection uses substring searches over opaque path segments, so caller-controlled identifiers can select another operation's permission token. |
| BH2-07 | medium | bad_spec | The MCP lifecycle tool exposes `taskId` but passes `null` and calls an SDK method with no task parameter; the v2 contract and MCP reference correctly declare this operation non-task-scoped. |
| BH2-08 | medium | bad_spec | `FileContextUnavailableProblem` omits the exact `AuthorityUnavailableProblem` branch, so canonical authority 503 examples pass only through the permissive legacy `ProblemDetails` branch rather than the required closed schema. |
| BH2-09 | medium | bad_spec | Historical authorization rechecks can emit `read_model_unavailable`, `projection_stale`, or `projection_unavailable`, but the seam does not distinguish and canonicalize those authority-race outcomes from operation-specific 503s. |
| BH2-10 | medium | bad_spec | `docs/sdk/api-reference.md` still declares 47 operations while the generated candidate and MCP reference declare 49. |
| BH2-11 | medium | bad_spec | The canonical error catalog and API reference still document seven client actions while the generated closed enum contains nine; the full Contracts suite fails this conformance check. |
| BH2-12 | medium | bad_spec | Error documentation maps `authorization_revocation_detected` to exit 73, while the regenerated parity oracle and `ErrorProjection` map it to access-denied exit 66. |
| BH2-13 | medium | bad_spec | `ConsumerDocsConformanceTests` still parses v1 conventions and cannot inventory the v2 reference; two full-suite tests fail deterministically. |
| BH2-14 | medium | patch | `ExitCodeWiringTests` still expects the removed synthetic `test_code` after its fixture changed to canonical `validation_error`; the full CLI suite has one deterministic failure. |
| BH2-15 | medium | defer | `_bmad-output/gates/baseline-ci/latest.json` has no source binding or automatic invalidation and therefore still says passed while current suites fail; this pre-existing latest-report behavior is not created by the candidate implementation. |
| EC2-01 | medium | bad_spec | Same verified MCP lifecycle no-op as BH2-07: a public argument is silently discarded instead of matching the non-task-scoped operation surface. |
| EC2-02 | false | reject | `V2ProtectedOperationAuthorizer` has no executable caller outside its focused unit tests, so an undefined internal enum value cannot reach a protected observation in the candidate seam. |
| EC2-03 | false | reject | Authorization is a point-in-time decision and the spec does not require an atomic reauthorization after the task-binding read; a concurrent revocation alone does not show the implemented ordering is violated. |
| EC2-04 | low | patch | The conformance generator obtains byte length with `stat()` and the digest from a later read, so a concurrent file replacement can produce an internally inconsistent manifest entry. |
| EC2-05 | high | bad_spec | `generate-pd10-v2-contract.py` validates the v1 input but permits `--output` to resolve to that same immutable v1 file, allowing candidate generation to overwrite historical evidence. |
| EC2-06 | false | reject | The cited `TaskId` OpenAPI parameter is explicitly `required: false`; direct SDK calls with a null task header are valid for operations where task context is optional. |
| EC2-07 | medium | patch | Legacy authority-body inspection calls object/string-only JSON APIs and catches only `JsonException`; valid non-object or non-string JSON can escape as `InvalidOperationException` and return 500. |
| EC2-08 | high | bad_spec | Same operation-family authorization defect as BH2-01 through BH2-04: the seam applies ordinary metadata/create grants to console, audit, ACL, and context families. |
| EC2-09 | medium | bad_spec | Canonical response rewriting leaves historical response headers intact; existing endpoints set correlation, freshness, task, and transport headers, so nominally identical safe denials can remain distinguishable. |
| EC2-10 | medium | bad_spec | Exceptions from tenant/claim/layered authorization accessors escape the seam and become 500 responses instead of the required canonical authority-unavailable 503. |
| EC2-11 | medium | bad_spec | `EveryProblemExampleUsesTheClosedProblemVocabularyAndShape` checks selected keys and enums but never resolves the example's declared schema, contrary to the amended schema-validation requirement. |
| EC2-12 | high | bad_spec | The independently asserted manifest path set omits `run-governance-completeness-gates.ps1`, even though that gate consumes the v2 contract, so a candidate-affecting gate can change without invalidating the digest. |
| EC2-13 | false | reject | carried: same claim as BH-01; release publication is a separate forbidden operator action, and this build neither routes nor publishes the candidate. |
| EC2-14 | false | reject | carried: same claim as EC-12; the approval-register diff is concurrent planning work and remains outside Story 1.17's implementation. |
| VG2-01 | high | patch | Pre-verified: no executable seam test exercises real task-to-folder binding for matching, mismatched, stale, and unavailable evidence, so the decisive cross-folder guard can regress undetected. |
| VG2-02 | high | patch | Pre-verified: folder-scoped readiness and freshness diagnostics have only static contract coverage; no runtime test proves folder denial short-circuits the tenant diagnostic read. |
| VG2-03 | medium | patch | Pre-verified: CLI and MCP tests never assert preservation of required `details.visibility`, so either adapter can silently discard the redaction signal. |
| VG2-04 | medium | patch | The verification reviewer reproduced BH2-14 directly: the changed CLI fixture makes the normal `ExitCodeWiringTests` assertion fail. |

## Design Notes

The relock candidate is a parallel v2 surface, not an in-place v1 migration. Candidate server endpoints may be assembled and tested through an isolated mapping seam, but `Program.cs` and supported deployment profiles must not select them until later governance gates authorize exposure.

## Verification

**Commands:**
- `dotnet msbuild src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersIdempotencyHelpers /p:Configuration=Debug` -- generated SDK and helpers are deterministic from v2.
- `dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root . --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --output tests/fixtures/parity-contract.yaml` -- C13 and fingerprints regenerate without drift.
- Focused builds/direct xUnit v3 class runs for changed Contracts, Server, Client, CLI, MCP, and UI test classes -- all pass with zero warnings/errors.
- `pwsh ./tests/tools/run-contract-spine-gates.ps1 -NoRestore` and `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` -- candidate contract/generation/parity gates pass.
