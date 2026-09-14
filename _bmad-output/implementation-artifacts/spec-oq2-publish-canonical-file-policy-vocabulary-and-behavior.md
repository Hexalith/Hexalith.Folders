---
title: 'Publish OQ2 canonical file-policy vocabulary and behavior'
type: 'feature'
created: '2026-09-13'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '822831269766c5824e122ddc725216d15f5374b3'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ2 blocks Stories 12.1, 12.3, and 4.20 because exact file-policy vocabulary and behavior remain provisional across the canonical contract, OpenAPI, and tests.

**Approach:** Publish a versioned canonical policy, bind PM, Architecture, and Security approval to its digest, align contract-facing artifacts, and add offline cross-surface conformance gates without claiming the downstream runtime implementation is complete.

## Boundaries & Constraints

**Always:** Preserve authorization/path policy before observation, atomic ordered change-set validation, relative forward-slash paths without retargeting, C4 limits, D-9's 262,144-byte transport boundary, metadata-only diagnostics, reopen-on-any-governed-identity-change, and cross-surface projection of the approved error vocabulary.

**Never:** Add a 50th public operation, implement runtime content/workspace/provider/query behavior or unrelated CLI/MCP product behavior, perform broad PD10 cleanup, or claim FR32-FR35 runtime evidence. The existing Add/Change/Remove endpoints remain public one-item adapters to the canonical internal batch request. Focused CLI/MCP input validation and error projection required for OQ2 parity are allowed; no command or tool is added.

## Approved Decisions

- Use the conservative path profile: ASCII `A-Z a-z 0-9 . _ - /`, 500 characters maximum, NFC declaration, ordinal-ignore-case collision detection, and rejection of every touched symlink/reparse entry or ancestor without following it.
- Bound mutations to 1 MiB per file, 100 changes, and 10 MiB aggregate. Strict UTF-8 with an optional BOM is content-readable; binary and other encodings may be mutated but remain metadata-only.
- Use server-owned classes `content_allowed`, `metadata_only`, `excluded`, and `restricted`. A bounded include allowlist is required, exclusions always win, re-inclusion is unsupported, and invalid or unavailable policy fails closed.
- Return 404 `tenant_access_denied/resource_unavailable` for missing, excluded, restricted, sensitivity-denied, and unauthorized paths. Reserve 416 for an authorized, visible, unsatisfiable range.
- Record `Administrator` as PM, Architecture, and Security approver on 2026-09-14, following the OQ1/OQ8 precedent and binding the final post-review decision set.
- Publish policy version `1.1.0`. `MutateFilesRequest` contains 1-100 caller-ordered `FileMutationRequest` items; operation IDs and ordinal-ignore-case paths are unique, removes contribute zero bytes, and every validation or policy failure rejects the complete batch. Add/Change/Remove expose only the matching one-item form.
- Include/exclude rules are root-anchored and ordinal-ignore-case. A policy has 1-100 combined rules, each 1-256 characters and at most 25,600 aggregate characters. `/` separates segments, `*` matches within one segment, `?` matches one non-slash character, and `**` is valid only as a complete segment and matches zero or more segments. Escapes, negation, classes, braces, and re-inclusion are unsupported; exclusions win.
- A file is content-readable only at 1,048,576 bytes or less when strict UTF-8 (optional BOM only at byte zero) decodes and the only control characters are tab, CR, and LF. Other files remain mutation-eligible but `metadata_only`. An authoritative visible directory is always `metadata_only`.
- Reject a trailing space/dot in any segment, every ordinal-ignore-case component alias, and `.git` plus descendants as `restricted`; never retarget an alias.
- For inline content, validate decoded bytes at 262,144 bytes or less and require decoded length and trusted observed content-hash reference to match the declarations before acceptance.
- A direct multi-target metadata request containing any hidden/missing target fails wholly with the canonical 404 and returns no visible subset.
- Range routing is half-open: `[EOF,EOF)` returns 200 with no bytes; any start beyond EOF or non-empty range starting at EOF returns 416; a range starting before EOF but ending after it returns available bytes with 206.
- Pin one immutable policy version/digest for evaluation. If it changes before an atomic apply or response shaping, discard the result and fail closed without mutation or disclosure.
- Policy-snapshot drift or inability to verify the pinned policy uses one non-disclosing HTTP 503 envelope: `category: file_policy_unavailable`, `code: file_policy_unavailable`, `retryable: true`, and `clientAction: retry`.
- Content failure precedence is exact. Any decoded or observed file content above 1,048,576 bytes returns HTTP 422 `input_limit_exceeded/file_content_limit_exceeded`, non-retryable with `revise_request`, before inline-transport routing. Inline content from 262,145 through 1,048,576 bytes retains HTTP 413 plus the retry-as-stream hint. Malformed base64 or a decoded-length/trusted-hash mismatch returns HTTP 400 `validation_error/content_evidence_invalid`, non-retryable with `revise_request`.
- Permit only the focused CLI/MCP parity changes required by this contract: reject unknown or numeric policy-class input through the stable usage-error path, project the exact typed OQ2 problems, and map `range_unsatisfiable` and `file_policy_unavailable` according to the generated parity fixture. Do not add a command, tool, or unrelated behavior.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Canonical path | Authorized policy-valid relative path | Server classifies; caller spelling is preserved | No retargeting |
| Ambiguous path | Traversal, link/reparse target, reserved name, or case alias | Reject before observation; apply nothing | No path echo |
| Content boundary | Text, binary, malformed, mismatched, transport-oversized, or absolutely oversized content | Apply one cross-surface classification/size and error-precedence matrix | 400 evidence-invalid, 413 retry-as-stream, or 422 absolute-limit; no truncation |
| Hidden content | Missing, excluded, restricted, sensitivity-denied, or unauthorized path | Use one approved non-enumerating public outcome | No existence, policy, count, or path disclosure |
| Mixed direct targets | Visible and hidden/missing paths in one metadata request | Return no items | Canonical 404 for the whole request |
| Range boundary | Empty EOF, beyond EOF, or EOF-shortened range | 200 empty, 416, or 206 respectively | 416 only after visibility and content authorization |
| Policy race | Policy version/digest changes or cannot be verified during evaluation | Discard evaluated result; apply/return nothing | Canonical non-disclosing 503 `file_policy_unavailable` |
| Approved evidence | Matching policy version/digest and three approval records | OQ2 is closed as a governed design decision | Runtime gaps remain explicit |

</frozen-after-approval>

## Code Map

- `docs/contract/file-context-contract-groups.md`, `docs/contract/oq2-file-policy-evidence.yaml` -- canonical 1.1.0 policy and digest-bound approval package; retain downstream runtime deferrals.
- `src/Hexalith.Folders.Contracts/openapi/{hexalith.folders.v1.yaml,extensions/hexalith-extension-vocabulary.yaml}` -- constrain the policy extension, batch component, operation-specific one-item request schemas, content-only search result, file-specific exact 404/416/503 and 400/413/422 bodies, examples, decoded-byte extension, and range categories. Do not tighten the shared non-file 404 response and do not add an operation.
- `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.yaml` -- align affected error rows through the generator; keep the 49-operation denominator.
- `src/Hexalith.Folders.Client/{Generated/*.g.cs,Convenience/FileUpload.cs}`, `tests/Hexalith.Folders.Client.Tests/{ClientGenerationTests,FileUploadConvenienceTests}.cs` -- regenerate rather than hand-edit, enforce exact typed-response values and missing policy-class initialization, enforce the 1 MiB streamed maximum, and prove freshness before any build can repair checked-in output.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/{ContractSpineFoundationTests,FileContextContractGroupTests,GovernanceCompletenessGateTests,ParityOracleGeneratorTests}.cs` -- exact positive and negative gates for every approved decision.
- `tests/tools/{run-governance-completeness-gates,run-contract-parity-ci-gates}.ps1`, `_bmad-output/gates/{governance-completeness,contract-parity-ci}/latest.json`, `.gitattributes`, and CI/release callers -- complete report provenance, run generated freshness before a repairing build, retain honest failure reporting, and keep LF-stable digest inputs.
- `_bmad-output/planning-artifacts/{prd,architecture,epics,.memlog}.md` -- synchronize OQ2 design closure while keeping Stories 12.1/12.3/4.20 and live body-search evidence open.
- `src/Hexalith.Folders.Cli/{Commands/File/FileCommand.cs,Commands/CommandPipeline.cs,Errors/ErrorProjection.cs}` and focused CLI tests; `src/Hexalith.Folders.Mcp/{Tools/FileTools.cs,Tooling/ToolPipeline.cs,Errors/FailureKindProjection.cs}` and focused MCP tests -- validate the closed policy class and preserve exact OQ2 error projection only; add no command or tool.
- `src/Hexalith.Folders/**`, server, providers, workers, and UI product behavior -- do not change; their current validators are precedent/evidence, not OQ2 runtime scope. Generated-type compile propagation may touch samples or UI tests.

## Tasks & Acceptance

**Execution:**
- [ ] Canonical policy/evidence/planning artifacts -- publish 1.1.0 with the approved public outcomes, bind exact approval identity/digest, correct planning history, and synchronize design-only closure.
- [ ] OpenAPI spine and extension vocabulary -- encode every approved decision as closed schemas and examples without expanding the public operation inventory or narrowing unrelated shared responses.
- [ ] Contract, governance, and parity gates -- cover valid boundaries plus malformed extension strings, batch semantics, endpoint-kind mismatch, search eligibility, path/content/routing, approval-authority omission, report provenance, and all changed parity projections.
- [ ] Generated SDK and helpers -- regenerate deterministically; close response values in generated/runtime validation; preserve missing-required-value detection; enforce streamed limits; and prove freshness before any repairing build.
- [ ] Focused CLI/MCP parity -- validate policy-class input, project typed OQ2 failures, map both new canonical categories, and add direct adapter/parity tests without adding surface area.

**Acceptance Criteria:**
- Given the approved decisions, when the canonical document and OpenAPI are inspected, then policy behavior and the internal batch wire shape are closed, bounded, and have no OQ2 placeholder or unbound public operation.
- Given missing, mismatched, stale, extra, or incomplete approval evidence, when offline gates run, then OQ2 fails closed with bounded metadata-only diagnostics.
- Given generated/schema/parity drift or a malformed policy instance, when offline gates run, then the package fails before a stale or permissive contract can pass.
- Given OQ2 governance is approved, when planning and evidence are inspected, then OQ2 is closed while Stories 12.1/12.3/4.20, public multi-file transport, and FR32-FR35 runtime proof remain incomplete.
- Given any OQ2 exact problem reaches the generated SDK, CLI, or MCP adapter, when it is projected, then its canonical category is preserved rather than collapsed to `internal_error`; invalid policy-class input fails before the SDK call.

## Implementation Notes

- Historical iteration-2 KEEP: published canonical file-policy version `1.1.0` with LF-stable SHA-256 `1fb6009543c87f7fee2785e7cf5ffff40e178efa6afd2292fa19960e05c48f6c` and exactly one PM, Architecture, and Security approval record for `Administrator` on 2026-09-13.
- Historical iteration-2 KEEP: closed the OpenAPI 3.1 policy extension vocabulary, internal `MutateFilesRequest` schema, path/content bounds, response-specific path types, range routing, and policy-snapshot behavior without adding a public operation.
- Historical iteration-2 KEEP: regenerated the NSwag client, idempotency helpers, and 49-row parity fixture while excluding the internal batch schema from the public SDK.
- Historical iteration-2 known-bad state to avoid: `$dynamicRef`, a shared global exact 404, `$ref`-sibling `const`, plain `maxBytes`, and test-project-only generation suppression did not provide the claimed enforcement or cross-surface safety.
- Preserve the explicit deferrals for Stories 12.1, 12.3, and 4.20, public multi-file transport, and FR32-FR35 runtime evidence.

- The implementation described below was reverted to `baseline_commit` after review found unresolved human-owned policy decisions. It is retained as historical KEEP context for re-planning.
- Published canonical file-policy version `1.0.0` with LF-stable SHA-256 `09a25950ee8db6e5417d543f1b0a16ed88364b898ddd3d7cd9d3c3628ef37dd4` and exactly one PM, Architecture, and Security approval record.
- Aligned the OpenAPI policy vocabulary, file-mutation bounds, path metadata, safe-denial/range routing, examples, and generated parity fixture while retaining all downstream runtime gaps.
- Added positive policy checks and fail-closed negative controls for missing, mismatched, stale, extra, and incomplete governance evidence; all five edge-case matrix rows are exercised by the focused test classes.

## Spec Change Log

- 2026-09-13: Implemented OQ2 canonical policy publication, cross-surface contract alignment, governance evidence, and conformance coverage.
- 2026-09-13: Review iteration 1 identified unresolved frozen-intent decisions; reverted implementation files to baseline, returned the spec to draft, and preserved the full triage record for human resolution and re-planning.
- 2026-09-13: Human approved the recommended resolutions; replanned policy 1.1.0 with a bounded internal batch shape, deterministic grammar/content/path/range/race behavior, strict schemas, generated freshness, and the existing public parity boundary.
- 2026-09-13: Review iteration 2 found missing public outcomes plus response scoping, endpoint-kind, search eligibility, decoded-byte enforcement, generated exactness, SDK-limit, parity, governance-negative, and pre-build freshness defects. The human approved canonical 503/400/413/422 outcomes; the spec now avoids the known-bad iteration-2 mechanisms and preserves the canonical policy/evidence, 49-operation inventory, deterministic generation, and downstream runtime deferrals as KEEP requirements.
- 2026-09-14: Human approved the focused CLI/MCP validation and error-projection exception required to close OQ2 parity without adding commands or tools; final governance approval is rebound to the completed 2026-09-14 decision set.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | high | intent_gap | The three file endpoints each accept one `FileMutationRequest`, while the new 100-change/10-MiB atomic change-set claim and the PRD require one command capable of carrying one or many mutations. The approved intent does not choose the missing batch wire shape or its relationship to the existing endpoints. |
| BH-02 | false | reject | `PathMetadata.pathPolicyClass` is intentionally transported as an assertion that the active server policy must revalidate; the canonical policy explicitly forbids callers from selecting or upgrading the server-owned result. |
| BH-03 | false | reject | `normalizedPath` is the path identity and `displayName` is separately documented as human-readable metadata. They need not be equal, and `displayName` cannot retarget the operation. |
| BH-04 | high | intent_gap | The approved policy fixes precedence but no include/exclude grammar, anchoring, escaping, directory inheritance, or comparison rules, so two conforming implementations can classify the same configured rule and path differently. |
| BH-05 | high | intent_gap | The policy names binary and oversized content as classification inputs but does not decide how valid UTF-8 containing binary controls is classified or what size makes otherwise readable content metadata-only. |
| BH-06 | medium | intent_gap | `FileMetadataItem` includes directories, but both visible policy-class definitions depend on file bytes or mutation eligibility; no approved directory classification rule exists. |
| BH-07 | high | intent_gap | Full-path ordinal-ignore-case equality does not settle ancestor-component aliases such as `docs` versus `Docs/new.md`; the frozen decision does not state whether every component must be collision-checked. |
| BH-08 | false | reject | The policy specifies the binding outcome—never follow a link/reparse entry and never retarget outside the workspace. A race-prone implementation would violate that outcome; the canonical contract need not mandate one filesystem algorithm. |
| BH-09 | high | intent_gap | `.git/config` may be structurally valid and subsequently restricted by policy, so that part is not a schema defect. Trailing-dot platform aliasing is real, however: the runtime rejects it while the approved path profile allows it despite the no-retargeting invariant, and the frozen decision does not resolve the conflict. |
| BH-10 | high | intent_gap | The stream contract requires observed length/hash agreement, but no equivalent rule binds decoded inline bytes to `byteLength` and `contentHashReference`; adding that behavior changes the digest-approved policy beyond the recorded decisions. |
| BH-11 | high | intent_gap | `FileMetadataRequest` accepts up to 100 direct targets, while the policy distinguishes direct-target 404 from collection omission without deciding how a mixed visible/hidden target list behaves atomically. |
| BH-12 | medium | intent_gap | Zero-length success and EOF partial-read prose do not settle `start == EOF`, `start > EOF`, or a zero-length request beyond EOF, so 200/206/416 routing remains ambiguous. |
| BH-13 | high | defer | The live-search operation was already metadata-only before OQ2, while the authoritative PRD already required line/byte location and a bounded snippet. OQ2 exposed but did not cause this pre-existing FR34–FR35 contract gap. |
| BH-14 | medium | bad_spec | The new extension vocabulary gives its nested policy members only `type: object`, so empty or malformed nested values validate despite the fully enumerated approved example; the implementation failed to encode decisions the spec already supplies. |
| BH-15 | medium | patch | `idempotency-and-parity-rules.md` still declares `redacted` for the affected operations after the OpenAPI and generated parity fixture remove it, leaving named parity authorities inconsistent. |
| BH-16 | high | bad_spec | The checked-in generated SDK still exposes the old open string and redacted-range contract even though the spec says to regenerate derived files when needed; the changed public schema made regeneration necessary. |
| BH-17 | medium | patch | The manifest's `reopen_policy` omits authority, signer, and approval-date drift that the canonical policy and planning artifacts declare, and the gate does not validate that field. |
| EC-01 | medium | patch | `ReadFileRange` returns a `range_unsatisfiable` 416 example, but its operation-level canonical error list omits that category, so generated parity cannot declare the only approved 416 outcome. |
| EC-02 | high | bad_spec | `SafeAuthorizationDenial404` references generic `ProblemDetails` through an unconstrained denial schema; a path-bearing or wrong-category 404 remains schema-valid despite the approved exact envelope. |
| EC-03 | high | bad_spec | The 416 response also references generic `ProblemDetails`, so a redacted/sensitivity-denial body remains schema-valid even though the approved decision reserves 416 for `range_unsatisfiable`. |
| EC-04 | high | bad_spec | Successful `FileMetadataItem.path` references the four-class shared `PathMetadata`, allowing `excluded` or `restricted` values in responses despite prose requiring only visible classes. |
| EC-05 | high | bad_spec | Successful `FileRangeReadResult.path` likewise permits `metadata_only`, `excluded`, and `restricted` even though only `content_allowed` may return bytes. |
| EC-06 | false | reject | The stream descriptor descriptions already impose declared/observed/top-level length and hash equality as semantic validation; JSON Schema cannot generally express cross-field equality, and schema validity alone is not operation acceptance. |
| EC-07 | high | intent_gap | Base64 `maxLength: 349528` can represent 262145–262146 decoded bytes, and the approved package does not state the missing decoded-length/hash validation rule for inline content. |
| EC-08 | medium | patch | Same verified manifest reopen-condition contradiction as BH-17. |
| EC-09 | high | intent_gap | Same verified missing policy-language decision as BH-04. |
| EC-10 | medium | intent_gap | `server-bounded` supplies no maximum rule count, rule length, or aggregate policy size; accepting the same policy can therefore vary by server, and no approved values exist to choose. |
| EC-11 | high | intent_gap | Same verified binary-detection ambiguity as BH-05. |
| EC-12 | high | intent_gap | Same verified missing content-readable size threshold as BH-05. |
| EC-13 | medium | intent_gap | Same verified EOF boundary ambiguity as BH-12. |
| EC-14 | high | intent_gap | Same verified trailing-dot/no-retargeting conflict as BH-09. |
| EC-15 | medium | patch | The shared 403 response still says path-policy cases may use 403, contradicting the new canonical 404 route for policy-hidden paths; 403 must be limited to coarse authorization failures. |
| EC-16 | medium | bad_spec | Same verified unconstrained nested extension schemas as BH-14. |
| EC-17 | high | intent_gap | The policy does not decide whether to pin a policy snapshot or recheck a concurrently changed active policy before atomic application, so authorization can change between validation and commit. |
| EC-18 | false | reject | Same outcome-level no-follow refutation as BH-08: any ancestor replacement that is followed would violate the existing canonical requirement regardless of implementation mechanism. |
| EC-19 | high | intent_gap | Same verified mixed visible/hidden direct-target ambiguity as BH-11. |
| EC-20 | medium | intent_gap | Same verified directory classification gap as BH-06. |
| EC-21 | high | intent_gap | The claim that behavior is closed is not supportable until the verified grammar/bounds and binary/readability decisions are supplied. |
| EC-22 | high | bad_spec | The approved prose is not enforced by the response schemas: hidden classes and noncanonical denial bodies remain machine-valid, confirming EC-02 through EC-05. |
| EC-23 | medium | patch | `OfType<YamlMappingNode>()` drops malformed scalar approval entries before record-count validation, so three valid records plus an extra non-mapping node can pass without `oq2_approval_extra`. |
| VG-01 | medium | patch | The passing governance report's `canonical_inputs` omits both new OQ2 artifacts, and the existing report assertion checks only the older C7 input. |
| VG-02 | medium | patch | The evaluator checks seven top-level package-identity scalars, but no negative control removes or mismatches any of them, allowing those checks to regress undetected. |
| VG-03 | high | bad_spec | Client generation runs in place before compile, so later consistency tests can inspect build-repaired files; the patch neither regenerated the checked-in SDK nor added a clean-tree/untouched-copy guard. |

**Grouped survivors:**

- `intent_gap`: atomic change-set wire shape; deterministic policy grammar and bounds; binary/content-readability and directory classification; ancestor/trailing-dot path identity; inline content verification; mixed-target and EOF routing; concurrent policy snapshot semantics.
- `bad_spec`: machine-enforced response/extension schemas and generated-SDK freshness.
- `patch`: parity authority alignment, exact reopen-policy gating, 403/404 wording, range parity category, malformed approval-node detection, report provenance, and package-identity negative controls.
- `defer`: the pre-existing FR34–FR35 live body-search result contract gap.

**Iteration 1 resolution:** The human approved all intent-gap recommendations on 2026-09-13. The frozen decisions above settle them; the `bad_spec` and `patch` survivors are now explicit execution tasks. The live body-search gap remains deferred, and the 49-operation public parity surface remains unchanged.

**Iteration 2 findings:**

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH2-01 | medium | patch | `.memlog.md` records OQ2 digest `b590b45c…`, while the policy, evidence manifest, gate constant, and direct SHA-256 result use `1fb60095…`; the planning event is factually stale. |
| BH2-02 | high | bad_spec | Tightening the shared `SafeAuthorizationDenial404` schema changed 404 bodies for 48 references, including non-file operations whose declared categories still include `not_found` and `cross_tenant_access_denied`; those valid operation outcomes no longer satisfy the shared exact schema. |
| BH2-03 | high | bad_spec | AddFile, ChangeFile, and RemoveFile still reference the unrestricted `FileMutationRequest`; only prose names the matching kind, so a removal body remains schema-valid on AddFile. |
| BH2-04 | high | bad_spec | SearchFolderFiles claims only `content_allowed` files are eligible but returns `FileTreeResult`, whose item schema permits `metadata_only` paths and directories. |
| BH2-05 | false | reject | Cross-item uniqueness and aggregate size cannot be expressed by ordinary JSON Schema keywords, but the registered root `x-hexalith-file-policy` instance encodes operation-ID comparison, path comparison, aggregate bytes, ordering, and atomicity and its closed value schema is validated; these rules are not prose-only. |
| BH2-06 | high | bad_spec | Plain `maxBytes` is not a registered OpenAPI extension or standard enforcing keyword, while `maxLength: 349528` admits encodings of 262145 and 262146 decoded bytes; an ordinary validator can accept content above the frozen inline boundary. |
| BH2-07 | medium | bad_spec | `FileUpload.BuildStreamedFileMutation` enforces only the lower stream boundary and accepts observed lengths above 1048576, allowing SDK callers to construct requests that the newly published contract forbids. |
| BH2-08 | medium | patch | CLI `Enum.Parse` accepts undefined numeric enum values and throws an uncaught `ArgumentException` for unknown symbolic input; closed policy-class input needs a bounded usage-error conversion. |
| BH2-09 | medium | patch | MCP has the same user-controlled `Enum.Parse` behavior, so malformed input can escape the stable pre-SDK usage-failure path or serialize an undefined enum. |
| BH2-10 | high | intent_gap | The frozen policy says policy snapshot drift fails closed but assigns no public status, category, code, retryability, or client action. Several existing error families are plausible, so the exact cross-surface outcome cannot be inferred safely. |
| BH2-11 | high | intent_gap | The frozen policy requires rejection for stream payloads above 1 MiB and inline decoded-length/hash mismatches but does not choose their exact public status/category/code behavior; current 413 and 422 responses encode different conditions. |
| BH2-12 | false | reject | Approval is intentionally bound to the single canonical Markdown policy digest. Derived surfaces are separately constrained by the closed extension schema, generated-artifact hashes, parity generation, and conformance gates; the approved intent did not require a multi-artifact approval digest set. |
| BH2-13 | high | bad_spec | NSwag generated unrestricted primitive properties for the supposedly exact 404/416 DTOs, and the client test checks only type presence; wrong category/code/status bodies deserialize without rejection. |
| BH2-14 | high | bad_spec | `FileMetadataItem.redaction` is contractually constant `not_redacted`, but the generated wire DTO still exposes `redacted`, `excluded`, and `binary_disallowed`, proving the `$ref` sibling `const` did not close the SDK type. |
| BH2-15 | medium | defer | The parity report honestly remains failed because the unchanged GoldenLifecycle integration class returns three pre-existing 400 responses instead of 202 and stops later categories. OQ2 cannot claim the entire wrapper passed; repairing those runtime flows is outside this decision story. |
| EC2-01 | high | bad_spec | Same verified adapter-schema defect as BH2-03: no request schema overlay constrains each public endpoint to its matching mutation kind. |
| EC2-02 | medium | patch | Same verified CLI parsing defect as BH2-08. |
| EC2-03 | medium | patch | Same verified MCP parsing defect as BH2-09. |
| EC2-04 | medium | bad_spec | Required non-nullable generated `PathPolicyClass` defaults to enum zero (`content_allowed`), so an SDK caller that omits it silently sends and hashes a value instead of failing request construction. Server revalidation prevents an authorization upgrade, but the SDK still changes caller intent. |
| EC2-05 | high | bad_spec | CLI and MCP catch only `HexalithFoldersApiException<ProblemDetails>`; the newly generated `SafeResourceUnavailableProblem` and `RangeUnsatisfiableProblem` exceptions fall into the bare-exception branch and collapse canonical failures to `internal_error`. |
| EC2-06 | medium | bad_spec | The policy test's custom schema validator ignores `minLength`; an empty `foundationUse` passes despite the extension schema and the implementation claim that malformed instances fail closed. |
| EC2-07 | high | bad_spec | Direct execution confirms CLI parity fails 4 cases and MCP parity fails 4 cases after `range_unsatisfiable` enters the fixture: both still encode the old documented `internal_error` exception rather than the generated parity mapping. |
| EC2-08 | medium | patch | Same verified stale planning digest as BH2-01. |
| VG2-01 | high | bad_spec | Normal solution builds still run in-place generation before the parity freshness test; CI builds the solution first and then runs the gate with `--no-build`, so stale checked-in generated sources can be repaired before comparison. The new project-reference property protects only a test-project build. |
| VG2-02 | medium | patch | Existing CLI and MCP tests do not assert the newly introduced policy-class conversion on captured requests; hard-coding a wrong valid enum remains undetected. |
| VG2-03 | medium | patch | OQ2 negative controls never remove or empty `approval.required_authorities`, leaving the evaluator's missing-authority-list diagnostic branch unverified. |
| VG2-O1 | high | bad_spec | Same verified unrestricted endpoint request-schema defect as BH2-03 and EC2-01. |
| VG2-O2 | medium | patch | Same verified undefined/throwing CLI and MCP enum parsing defect as BH2-08, BH2-09, EC2-02, and EC2-03. |
| VG2-O3 | medium | patch | Same verified stale planning digest as BH2-01 and EC2-08. |

**Iteration 2 grouped survivors:**

- `intent_gap`: choose exact public outcomes for policy-snapshot drift, streamed content above 1 MiB, and inline decoded-length/hash mismatch.
- `bad_spec`: scope exact path 404 without breaking unrelated operations; enforce endpoint mutation kinds and content-only search results; use an enforcing decoded-byte constraint; propagate the stream maximum; make exact response values exact in the SDK; preserve omission detection; project new typed problems; validate extension string bounds; restore CLI/MCP range parity; and move freshness verification before any repairing build.
- `patch`: correct the planning digest; validate CLI/MCP policy-class input and test its mapping; cover a missing/empty required-authority list.
- `defer`: the pre-existing GoldenLifecycle 400-versus-202 failures that prevent the full parity wrapper from reaching later categories.

**Iteration 2 resolution:** Awaiting human decisions for the `intent_gap` outcomes. All derived implementation changes are reverted to `baseline_commit` before replanning; the verified KEEP requirements above must survive re-derivation.

## Verification

**Current implementation results:**
- Contract and client focused test projects build with 0 warnings and 0 errors.
- `dotnet build Hexalith.Folders.slnx --no-restore -m:1` passed with 0 warnings and 0 errors after generated-type propagation.
- Focused contract, governance, and parity classes passed 49/49 through the xUnit in-process runner.
- Focused client generation and upload classes passed 49/49; isolated NSwag/client-helper regeneration is included in that result.
- Directly affected CLI file-transport, MCP file-transport, and UI folder-tree classes passed 2/2, 3/3, and 5/5 respectively.
- The governance completeness wrapper passed 19/19 through its .NET 10 in-process fallback and wrote a `passed` report with both OQ2 canonical inputs.
- The contract parity wrapper passed server/spine (12), previous-spine (21), generated-client (19), idempotency (19), parity-schema (15), parity-determinism (15), and SDK-transport (223) categories. It then stopped on three existing GoldenLifecycle integration failures whose archive/create requests receive HTTP 400 `validation_error` instead of the expected HTTP 202; those runtime files are outside OQ2 scope and are unchanged by this implementation.

**Commands:**
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~FileContextContractGroupTests|FullyQualifiedName~ContractSpineFoundationTests|FullyQualifiedName~GovernanceCompletenessGateTests|FullyQualifiedName~ParityOracleGeneratorTests"` -- expected: all focused contract gates pass.
- `dotnet test tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --no-restore --filter FullyQualifiedName~ClientGenerationTests` -- expected: checked-in SDK matches isolated regeneration.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: OQ2 version/digest/approval checks pass.
- `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -SkipRestoreBuild` -- expected: generated client and 49-row parity stay synchronized.
- `dotnet build Hexalith.Folders.slnx --no-restore -m:1` -- expected: zero warnings/errors after generated type propagation.
- `git diff --check 822831269766c5824e122ddc725216d15f5374b3 -- .` -- expected: no whitespace errors.

**Pre-rollback results (historical):**
- Contract test project build passed with 0 warnings and 0 errors.
- Focused policy and governance tests passed: 25/25, with no skipped or not-run tests.
- Governance wrapper passed through its direct executable fallback: 18/18; its initial legacy VSTest attempt reports the known .NET 10 Microsoft.Testing.Platform incompatibility before the successful fallback.
- Full contract suite passed: 292/292, with no skipped or not-run tests.
- Policy digest and LF attributes match the approved evidence; `git diff --check 822831269766c5824e122ddc725216d15f5374b3 -- .` passed.
