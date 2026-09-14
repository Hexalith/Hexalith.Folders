---
title: 'Publish OQ2 canonical file-policy vocabulary and behavior'
type: 'feature'
created: '2026-09-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 5
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
- `src/Hexalith.Folders.Contracts/openapi/{hexalith.folders.v1.yaml,extensions/hexalith-extension-vocabulary.yaml}` -- constrain and validate the complete policy instance and its vocabulary example, including the exact ASCII character profile; keep the batch internal; make one-item endpoint kinds survive SDK generation; bind the tree and search examples to the correct operations, cap search at 500, and pin its query family; use 64-bit offsets and response-specific 200/206 schemas that still generate one successful SDK return abstraction; and close every exact 404/416/503 and 400/413/422 member without tightening unrelated shared responses or adding an operation.
- `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.yaml` -- align affected error rows through the generator; keep the 49-operation denominator.
- `src/Hexalith.Folders.Client/{Generated/*.g.cs,Generation/*.cs,Serialization/*.cs,Convenience/FileUpload.cs,FoldersFileUploadExtensions.cs}`, `tests/Hexalith.Folders.Client.Tests/{ClientGenerationTests,FileUploadConvenienceTests}.cs` -- regenerate rather than hand-edit; return both valid 200 and 206 range bodies as successful results through one usable SDK abstraction; never treat a non-problem typed result or arbitrary JSON as Problem Details; preserve endpoint request types and omitted removal fields; validate every request transport/evidence branch before serialization and after deserialization; match the OpenAPI media-type grammar exactly; reject noncanonical or overlong base64 before allocation; reject duplicate members, missing/extra/wrong exact-response members, wrong JSON token types, invalid opaque IDs/URI references/non-UTC dates, and numeric enums; preserve schema-valid legacy Problem Details extension members without imposing exact-only bounds; require the approved safe title/message for disclosure-safe exact envelopes; project typed stream-mode problems without raw text only when HTTP/body status agrees; synchronize every shadowed base/derived policy, range, and search-item member; validate complete range/search results including decoded byte evidence, window/page/count/truncation relationships, and the 262,144-byte range maximum; compare every explicit inline hash to the bytes; apply the absolute 1 MiB failure before D-9 transport routing; recognize server stream retry only from the exact 413 envelope plus case-insensitive required transport evidence; make postprocessing atomic, incremental-safe, and fail-closed on generator-template drift; and prove freshness before any build can repair output.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/{ContractSpineFoundationTests,FileContextContractGroupTests,GovernanceCompletenessGateTests,ParityOracleGeneratorTests}.cs` -- execute the registered policy `valueSchema` against both the root instance and vocabulary example, validate real accepted/rejected mutation and response instances including `if`/`then`, resolve each operation example against its bound response schema, and retain exact positive and negative gates for every approved decision.
- `tests/tools/{run-governance-completeness-gates,run-contract-parity-ci-gates}.ps1`, `_bmad-output/gates/{governance-completeness,contract-parity-ci}/latest.json`, `.gitattributes`, and CI/release callers -- complete report provenance, run generated freshness before a repairing build, isolate every parity lane from both ordinary nonzero exits and terminating runner/prerequisite errors, record every lane, aggregate the final result honestly, execute a synthetic failure-injection test of that behavior, and keep LF-stable digest inputs.
- `_bmad-output/planning-artifacts/{prd,architecture,epics,.memlog}.md` -- synchronize OQ2 design closure while keeping Stories 12.1/12.3/4.20 and live body-search evidence open.
- `src/Hexalith.Folders.Cli/{Commands/CommandOptions.cs,Commands/File/FileCommand.cs,Commands/CommandPipeline.cs,Errors/ErrorProjection.cs}` and focused CLI tests; `src/Hexalith.Folders.Mcp/{Tools/FileTools.cs,Tooling/RequestBody.cs,Tooling/ToolPipeline.cs,Errors/FailureKindProjection.cs}` and focused MCP tests -- reject blank required removal bodies plus symbolic and numeric invalid policy classes before any SDK call, normalize direct and callback-wrapped request/size validation failures into bounded metadata-only usage or canonical 422 results, preserve only declared/status-matching exact OQ2 projections, retain exact D-9 code/retry semantics, and exercise real-client 206/416/503 wire paths; add no command or tool.
- `docs/sdk/{quickstart,cli-reference}.md`, `docs/contract/sdk-generation-and-idempotency-helpers.md`, `samples/Hexalith.Folders.Sample/FolderLifecycleSample.cs`, and consumer-doc/pattern-example tests -- use the generated enum in every public snippet, compile the upload example rather than checking prose alone, document the generated range/policy category mappings exactly, and publish the complete explicit generation/postprocessing command without claiming ordinary compilation repairs artifacts.
- `src/Hexalith.Folders/**`, server, providers, workers, and UI product behavior -- do not change; their current validators are precedent/evidence, not OQ2 runtime scope. Generated-type compile propagation may touch samples or UI tests.

## Tasks & Acceptance

**Execution:**
- [x] Canonical policy/evidence/planning artifacts -- republish 1.1.0 with the approved outcomes and exact digest-bound approvals, preserve all runtime deferrals, and clarify that authorization completes before bounded no-follow structural lookup occurs inside path-policy evaluation.
- [x] OpenAPI spine and extension vocabulary -- encode every approved decision as closed schemas and self-validating examples; constrain the exact ASCII/media/base64/URI/UTC profiles; validate the whole policy instance; preserve operation-specific SDK request types and removal omission; correctly bind tree/search examples and a 500-result search family with complete two-way truncation rules and non-null policy classes; express 64-bit/200/206 range semantics in response-specific schemas that generate one successful SDK return abstraction; close every exact response member and approved safe message; keep the 49-operation inventory and unrelated shared responses unchanged.
- [x] Contract, governance, and parity gates -- validate real policy, vocabulary-example, mutation, bound operation-example, exact-response, search, and range instances including omissions, extras, duplicates, conditional branches, token types, grammar, bounds, inherited-property use, and every governed relationship; cover approval/report provenance; and execute a synthetic terminating failure proving that every parity lane is still recorded before aggregate failure.
- [x] Generated SDK and helpers -- regenerate deterministically; return conforming 200/206 responses as successes through populated base and derived members; reject non-problem typed results, arbitrary/malformed problem JSON, noncanonical base64/media/date/URI values, duplicate/omitted/extra/mismatched exact bodies, coercible token types, invalid opaque IDs, numeric enums, incomplete mutation branches, and incomplete/inconsistent range/search evidence; retain endpoint-specific requests and nullable removal evidence omission; preserve valid legacy extensions; synchronize base/derived policy/range/item values; enforce content failure precedence and case-insensitive exact 413 retry evidence; verify explicit hashes including whitespace; make the explicit generation pipeline atomic and incremental-safe; and prove pre-repair freshness.
- [x] Focused CLI/MCP parity -- reject blank required removal bodies and unknown/numeric policy classes before the SDK call; normalize every direct or callback-wrapped validation/absolute-limit failure; preserve only validated exact OQ2 problems and exact D-9 retry fields; map both canonical categories; and add real-handler 206/416/503 adapter tests without adding surface area.
- [x] Consumer documentation -- update the SDK quickstart to the generated enum, compile the public upload snippet through a maintained sample or pattern-example gate, correct the CLI category table, document the complete detached generation/postprocessing command, and leave no unrelated solution-file churn.

**Acceptance Criteria:**
- Given the approved decisions, when the canonical document and OpenAPI are inspected, then policy behavior and the internal batch wire shape are closed, bounded, and have no OQ2 placeholder or unbound public operation.
- Given missing, mismatched, stale, extra, or incomplete approval evidence, when offline gates run, then OQ2 fails closed with bounded metadata-only diagnostics.
- Given generated/schema/parity drift or a malformed policy instance, when offline gates run, then the package fails before a stale or permissive contract can pass.
- Given OQ2 governance is approved, when planning and evidence are inspected, then OQ2 is closed while Stories 12.1/12.3/4.20, public multi-file transport, and FR32-FR35 runtime proof remain incomplete.
- Given any OQ2 exact problem reaches the generated SDK, CLI, or MCP adapter, when it is projected, then its canonical category is preserved rather than collapsed to `internal_error`; invalid policy-class input fails before the SDK call.
- Given a removal body or endpoint-kind mismatch, when it passes through generated SDK, CLI, or MCP serialization, then omitted fields remain omitted and only the endpoint's matching mutation kind can be sent.
- Given an exact OQ2 response is missing a member, has an extra root/detail member, disagrees with its HTTP status, or arrives through stream deserialization without raw text, when the SDK and adapters process it, then only a complete declared canonical envelope is projected and every malformed or unexpected envelope fails closed.
- Given an exact OQ2 response carries a numeric enum, a coercible string in a numeric/Boolean slot, or an invalid correlation identifier, when it crosses the generated wire boundary, then deserialization rejects it before adapter projection.
- Given any successful non-problem typed result or arbitrary JSON is carried by an exception, when the adapter inspects it, then it cannot be reinterpreted as Problem Details or projected as `success`.
- Given any Add/Change/Remove request, when the generated type serializes or deserializes it, then its operation kind, transport discriminator, required and forbidden evidence, size bounds, and length/hash relationships all match its exact branch; a blank required removal body is a bounded usage error before HTTP.
- Given authorized range and search responses, when their schemas, bound examples, generated SDK types, and wire paths are exercised, then offsets are 64-bit, both 200 and 206 return successful usable results, partial/body byte evidence is internally consistent, every required member is present, search is capped at 500, and search results identify the `search` query family.
- Given a mutation or response carries media type, base64, URI-reference, UTC time, or repeated-member input, when it crosses the SDK boundary, then validation matches the canonical raw-wire grammar exactly and rejects overlong/noncanonical values before avoidable allocation or coercion.
- Given a successful range or search result is consumed through either its generated base or narrowed type, when callers read range/path/kind/pagination metadata, then both views expose the same populated values and all window, item-count, page-limit, configured-limit, byte-count, and truncation relationships agree.
- Given a schema-valid legacy problem shares an OQ2 response status, when it carries permitted extension metadata, then it remains usable without exact-envelope-only bounds; exact OQ2 envelopes remain closed, duplicate-safe, status-matched, and fixed to their approved disclosure-safe messages.
- Given any new request-validation or absolute-limit exception reaches CLI or MCP, including reflection-wrapped serialization callbacks, when the adapter handles it, then it returns the stable bounded metadata-only usage or canonical 422 result and does not fault.
- Given the checked-in client is missing, stale, or generated from a changed NSwag template, when the documented explicit regeneration pipeline runs, then postprocessing cannot be skipped or partially committed and an unrecognized 206/converter shape fails generation.
- Given decoded or observed content exceeds 1 MiB, when upload helpers classify it, then the absolute file limit wins before inline retry routing; a 413 becomes stream-retry guidance only when its complete exact envelope and transport evidence match, with its code/retry fields preserved.
- Given one parity lane fails by nonzero exit or terminating prerequisite/runner error, when the offline wrapper runs, then every remaining lane still executes, the report records each result, and the final process status remains failed.

## Implementation Notes

- Iteration-5 KEEP: retain the approved 1.1.0 canonical policy, LF-stable `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713` digest, three `Administrator` approvals dated 2026-09-14, 49-operation inventory, internal-only batch, operation-specific public request types, exact outcome families, and all explicit runtime/public-batch deferrals.
- Iteration-5 KEEP: retain the common successful 200/206 range return, strict raw-token converters, complete mutation/request guards, explicit-inline-hash comparison, absolute 1 MiB precedence, exact typed-problem projection, real CLI/MCP 206/416/503 paths, generated enum consumer examples, per-lane parity isolation, and the 11-lane report with only the two frozen pre-existing runtime failures.
- Iteration-5 KEEP: retain the parent-audit fixes that synchronize common range properties and make a truncated search page require a reason; extend them to every generated shadowed member and both truncation branches rather than removing them.
- Iteration-5 known-bad state to avoid: do not accept punctuation-leading media types, whitespace-padded/overlong base64, over-bound partial windows, inconsistent page counts/limits, non-UTC freshness, duplicate exact members, disclosure-bearing exact messages, or case-sensitive HTTP header lookup; do not reject valid legacy extensions; do not let new validator exceptions escape CLI/MCP; and do not let raw client generation bypass or outlive postprocessing.

- Iteration-4 KEEP: retain the approved 1.1.0 policy document, LF-stable `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713` digest, three `Administrator` approvals dated 2026-09-14, planning synchronization, and every explicit runtime/public-batch deferral.
- Iteration-4 KEEP: retain the 49-operation inventory, internal batch declaration, operation-specific Add/Change/Remove SDK request types, nullable removal omission, exact 404/416/503/400/413/422 values, 64-bit range offsets, distinct status-specific range schemas, search-only result/path/query-family types, and compiled enum-based consumer examples.
- Iteration-4 KEEP: retain deterministic isolated generation, pre-repair freshness, complete root-policy and conditional-schema evaluation, inline hash comparison, raw CLI/MCP numeric-policy rejection, typed stream-mode exact problems, all-lane normal-failure aggregation, report provenance, and the passing contract/client/CLI/MCP/UI/governance suites.
- Iteration-4 known-bad state to avoid: do not let NSwag model 206 as an exceptional alternate success, convert arbitrary typed results to `ProblemDetails`, validate canonical values only after coercive deserialization, validate only discriminators while ignoring complete branch/root evidence, swap tree/search examples, retain a 2,000-item search cap, or rely on an outer parity `try` to survive terminating per-lane errors.

- Iteration-3 KEEP: retain canonical policy version `1.1.0`, the LF-stable `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713` digest, and exactly one PM, Architecture, and Security approval by `Administrator` on 2026-09-14; keep runtime and public-batch deferrals explicit.
- Iteration-3 KEEP: retain the 49-operation public inventory, the internal-only `MutateFilesRequest`, operation-specific one-item Add/Change/Remove schemas, the closed policy-extension vocabulary, and exact 404/416/503/400/413/422 outcome families without narrowing unrelated shared responses.
- Iteration-3 KEEP: retain deterministic isolated SDK/helper regeneration, pre-repair freshness verification, the 1 MiB streamed helper maximum, governance negative controls and report provenance, and the passing contract/client/CLI/MCP/UI focused lanes.
- Iteration-3 KEEP: retain focused CLI/MCP symbolic policy-class validation and typed error projection without adding commands or tools; the three pre-existing GoldenLifecycle 400-versus-202 failures remain outside OQ2 but must not prevent later parity lanes from executing and reporting.

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
- 2026-09-14: Review iteration 3 found generated request/response fidelity, complete-schema validation, raw numeric-enum rejection, inline-hash verification, range/search exactness, and fail-fast parity defects. Replanning now requires omission- and extra-safe exact DTOs, status-checked typed stream projection, endpoint-specific generated requests, complete instance-level conformance gates, all-lane parity reporting, and a compiled quickstart while preserving the approved 1.1.0 digest package, 49-operation inventory, deterministic regeneration, and downstream runtime deferrals.
- 2026-09-14: Review iteration 4 found that the generated client still treated valid 206 as exceptional, confused arbitrary typed results with problems, accepted coercible/numeric wire values, incompletely validated mutation/range/search shapes, misbound tree/search examples and limits, mishandled absolute-size/413 precedence, and could abort parity on terminating errors. The non-frozen plan now requires one usable 200/206 success abstraction, raw-token and complete-instance validation, exact adapter retry semantics, correct self-validating examples/docs, and executable per-lane failure isolation while preserving all iteration-4 KEEP instructions.
- 2026-09-14: Review iteration 5 found raw media/base64/date/URI and duplicate-member gaps, incomplete range/search relationships and inherited DTO synchronization, legacy/exact Problem Details divergence, unnormalized client validation exceptions, case-sensitive retry headers, and a fail-open incremental postprocessing path. The non-frozen plan now requires exact raw-wire grammar, complete relational and polymorphic behavior, disclosure-safe exact messages with valid legacy extensions, bounded CLI/MCP normalization, independent negative controls, and atomic fail-closed explicit regeneration while preserving every iteration-5 KEEP instruction.

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

**Iteration 3 findings:**

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH3-01 | high | bad_spec | `FileMutationRequest.ByteLength` is generated as a non-nullable `int`; raw CLI/MCP removal bodies that omit it deserialize to zero and serialize an explicitly forbidden removal field. |
| BH3-02 | high | bad_spec | NSwag collapses the operation-specific Add/Change/Remove request schemas back to `FileMutationRequest`, so the SDK accepts endpoint-kind mismatches that the OpenAPI now rejects. |
| BH3-03 | medium | bad_spec | The generated `PathMetadata` serialization guard skips every subclass; public narrowed subclasses can therefore carry undefined numeric policy enums through a request typed as the base class. |
| BH3-04 | high | bad_spec | Exact-problem members use `Required.DisallowNull`, which permits omission, and the generated validators compare defaulted values rather than proving every required member was present. |
| BH3-05 | high | bad_spec | Generated exact problem/detail DTOs retain extension-data dictionaries; the generic problem projection can carry forbidden detail keys such as path or content evidence despite `additionalProperties: false`. |
| BH3-06 | high | bad_spec | CLI/MCP project any parseable bare API-exception body without checking HTTP status or declared exact shape, so an unexpected status can masquerade as a canonical OQ2 failure. |
| BH3-07 | high | bad_spec | `BuildInlineFileMutation` accepts an explicit content hash without comparing it to the decoded bytes, and its test blesses the mismatch despite the approved inline-evidence rule. |
| BH3-08 | false | reject | The decoded-byte keyword is a registered Hexalith extension and its focused evaluator rejects malformed and over-bound base64; ordinary OpenAPI-validator support was not claimed. |
| BH3-09 | high | bad_spec | The extension vocabulary's closed `valueSchema` is not executed against the complete root policy instance, so unasserted omissions or nested extra properties can pass the gates. |
| BH3-10 | false | reject | Request-schema invalidity does not preclude the server from routing decoded or observed size failures to the declared 413/422 responses; no generic validation middleware ordering is mandated here. |
| BH3-11 | medium | bad_spec | HTTP 200 and 206 share one range-result schema with no machine-checkable `partial` or offset/length relationship, so contradictory status/body combinations validate. |
| BH3-12 | high | bad_spec | Range offsets omit `format: int64` and generate as `int`, preventing SDK access above roughly 2 GiB even though the contract permits much larger authorized files. |
| BH3-13 | medium | patch | `SearchFolderFiles` references the tree example whose `limits.queryFamily` is `tree`; neither the example nor search result schema pins `search`. |
| BH3-14 | high | bad_spec | The parity wrapper exits at the known GoldenLifecycle failure before the new CLI, MCP, and mixed-surface lanes, so those OQ2 gates do not execute in CI. |
| BH3-15 | false | reject | Ordinary builds intentionally cannot repair generated files, while the parity workflow runs isolated freshness verification before building; detaching generation targets preserves the stated gate order. |
| BH3-16 | medium | bad_spec | The canonical text forbids structural observation until path policy is complete while the same stage requires no-follow namespace/link inspection; it must explicitly permit bounded structural lookup inside policy evaluation after authorization. |
| BH3-17 | false | reject | Commit history proves the `/pushall` files and submodule updates are already separate commits from OQ2; the preserved original baseline merely makes them appear in the cumulative diff. |
| BH3-18 | high | defer | The unrelated `/pushall` skill validates only conflict resolutions, so a clean but failing merge can be pushed and pruned without tests; fixing agent-context skills is outside this build. |
| BH3-19 | high | defer | The unrelated `/pushall` skill commits dirty detached-HEAD work before switching branches, which can strand the new commit; fixing agent-context skills is outside this build. |
| VG3-01 | medium | bad_spec | Pre-verified: generic CLI/MCP raw-body readers retain `StringEnumConverter.AllowIntegerValues=true`, so numeric policy classes reach remove/context calls despite the approved pre-SDK rejection rule. |
| VG3-02 | medium | patch | Pre-verified: `docs/sdk/quickstart.md` still assigns a string to the newly typed policy enum, so the primary upload example no longer compiles. |
| VG3-03 | medium | bad_spec | Pre-verified: mutation `if`/`then` branches have no instance-level conformance tests; deleting mutual-exclusion rules leaves the current assertions green. |
| VG3-04 | medium | patch | Pre-verified: generated validator tests do not mutate client action and all other invariant fields independently, allowing regeneration to weaken exactness undetected. |
| VG3-O1 | high | bad_spec | Pre-verified: exact DTO omission is accepted because presence is not enforced and canonical enum/default values mask absent members. |
| EC3-01 | high | bad_spec | The generated client's default stream response mode preserves a typed exact result but no raw body; the adapter fallback ignores that result and collapses valid 416/503 outcomes to `internal_error`. |
| EC3-02 | high | bad_spec | A parseable problem body on an undeclared or mismatched HTTP status is trusted without status/shape validation, reproducing BH3-06 across both adapters. |
| EC3-03 | high | bad_spec | Exact responses with omitted required fields deserialize using defaults, reproducing BH3-04 and VG3-O1. |
| EC3-04 | high | bad_spec | Undeclared root/detail fields survive generated deserialization and can enter adapter projection, reproducing BH3-05. |
| EC3-05 | high | bad_spec | The canonical 404 schema fixes status/category/code but not type/title/message, even though all hidden causes must have one indistinguishable envelope. |
| EC3-06 | medium | bad_spec | Narrowed path subclasses bypass undefined-enum validation, reproducing BH3-03. |
| EC3-07 | medium | patch | Search responses can report another `limits.queryFamily`, reproducing BH3-13. |
| EC3-08 | medium | defer | The unrelated `/pushall` automatic commit bypasses the repository's mandatory commitlint workflow; fixing agent-context skills is outside this build. |
| EC3-09 | high | defer | Detached-HEAD dirty work can be committed and then abandoned by `/pushall`, reproducing BH3-19. |

**Iteration 3 grouped survivors:**

- `bad_spec`: preserve operation-specific request fidelity through generation, including omission-safe removal fields; close raw and subclass policy-enum validation; make exact response deserialization presence-, extra-field-, status-, stream-mode-, and safe-message-exact; verify inline hashes; validate the complete policy instance and real mutation branches; close range response and 64-bit offset semantics; execute every parity lane despite earlier failures; and clarify that authorization precedes the bounded no-follow structural lookup performed inside path-policy evaluation.
- `patch`: publish a search-specific example and query-family constraint; update and compile-check the SDK quickstart; and make exact-validator tests mutate every governed field.
- `defer`: `/pushall` clean-merge validation, detached-HEAD preservation, and commitlint enforcement are unrelated agent-context defects.
- `reject`: ordinary-validator support for the registered decoded-byte extension, alleged 413/422 unrepresentability, detached ordinary-build generation, and already-separated unrelated commits.

**Iteration 4 findings:**

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH4-01 | high | bad_spec | `ReadFileRangeAsync` returns only `FileRangeReadCompleteResult`, while its conforming HTTP 206 branch throws `HexalithFoldersApiException<FileRangeReadPartialResult>`; callers cannot receive a successful shortened range. |
| BH4-02 | high | bad_spec | The exception helper converts every non-null typed result not recognized as malformed OQ2 into `ProblemDetails`; a typed 206 result has no problem fields and therefore projects with the default `success` category. |
| BH4-03 | high | bad_spec | `ValidateOq2Envelope` uses coercive `JObject.Value<int?>` and `Value<bool?>` reads, so string-valued numbers/booleans can satisfy checks after violating the JSON wire types. |
| BH4-04 | medium | bad_spec | Exact DTO validation checks only that `correlationId` is nonblank, not the contract's 16-128-character opaque-identifier pattern, so invalid identifiers remain projectable. |
| BH4-05 | high | bad_spec | Generated exact and policy enums use the default `StringEnumConverter`; numeric ordinals that map to defined members survive `Enum.IsDefined`, contrary to the string-only vocabulary. |
| BH4-06 | high | bad_spec | Add/Change generated validators check only the narrowed operation kind and do not validate transport selection, required/forbidden evidence, byte bounds, or matching length/hash relationships before serialization. |
| BH4-07 | high | bad_spec | The Remove validator forbids content fields but never requires `transportOperation: metadataOnlyRemoval`, so a removal with an inline/stream transport enum can serialize. |
| BH4-08 | high | bad_spec | Complete/partial range validators inspect only `Range` and root extras; missing path, content, limits, or freshness members can survive generated deserialization through constructor defaults. |
| BH4-09 | medium | bad_spec | A blank CLI/MCP removal body becomes `new RemoveFileRequest()`; its generated serialization guard throws `JsonSerializationException`, which neither adapter pipeline catches as a usage error. |
| BH4-10 | high | bad_spec | `BuildInlineFileMutation` checks the 262,144-byte routing boundary before the absolute 1 MiB limit, so content above 1 MiB is incorrectly reported as retry-via-stream instead of the approved 422-class failure. |
| BH4-11 | medium | patch | An explicitly supplied whitespace hash is treated as absent and silently replaced; only `null` is the documented derivation request, while any explicit nonmatching value must fail. |
| BH4-12 | high | bad_spec | Upload convenience translates every HTTP 413 by status alone into `FileUploadStreamingRequiredException`, without requiring the exact D-9 code or retry-transport evidence, masking unrelated/malformed responses. |
| BH4-13 | high | bad_spec | CLI/MCP handling of that local exception replaces the approved 413 code/retry semantics with generic `input_limit_exceeded`; MCP specifically emits code `input_limit_exceeded` and `retryable: false` instead of `d9_inline_limit_exceeded` and `true`. |
| BH4-14 | medium | defer | The seekable-stream rewind problem is verified but predates the OQ2 baseline: `ReadInlineCandidateAsync` consumes 262,145 bytes before signaling stream retry and does not restore position. |
| BH4-15 | high | defer | MCP's full `Convert.FromBase64String` allocation before bounds checking is real but already exists at `baseline_commit` and is outside the frozen focused-adapter exception. |
| BH4-16 | high | defer | CLI's full stdin/file buffering before bounds checking is real but already exists at `baseline_commit` and is outside the frozen focused-adapter exception. |
| BH4-17 | medium | patch | The extension-vocabulary example omits required `vocabularyRef` and `foundationUse`, so the example does not satisfy its own registered `valueSchema`. |
| BH4-18 | high | bad_spec | The complete policy schema constrains `characterProfile` only by length, allowing a different repertoire despite the frozen exact ASCII profile. |
| BH4-19 | false | reject | Carried BH2-05: ordinary JSON Schema does not express the cross-item ordinal uniqueness/aggregate rule; the registered root file-policy instance is the declared executable authority and encodes those semantics. |
| BH4-20 | high | bad_spec | The parity wrapper has no per-gate terminating-error boundary under `$ErrorActionPreference = 'Stop'`; a missing fallback runner triggers `Write-Error`, aborts the loop, and leaves later lanes absent. |
| BH4-21 | medium | patch | `docs/sdk/cli-reference.md` still omits `range_unsatisfiable` from exit 69 and `file_policy_unavailable` from exit 72 and incorrectly documents range as an unknown-category fallback. |
| EC4-01 | high | bad_spec | Exact-response string enums accept numeric wire ordinals through the default converter, reproducing BH4-05 for generated problem DTOs. |
| EC4-02 | high | bad_spec | Arbitrary or non-problem typed/raw JSON is converted to `ProblemDetails` when it is not recognized as OQ2, allowing a default `success` category to escape as an adapter result, reproducing BH4-02. |
| EC4-03 | high | bad_spec | A valid HTTP 206 is thrown as an exception rather than returned as data, reproducing BH4-01. |
| EC4-04 | high | bad_spec | Typed mutation guards do not enforce the conditional transport/evidence branches, reproducing BH4-06 and BH4-07. |
| EC4-05 | high | bad_spec | Numeric `pathPolicyClass` can deserialize as a defined generated enum member, reproducing BH4-05 on path DTOs. |
| EC4-06 | medium | bad_spec | Narrowed path types shadow the base `PathPolicyClass` without synchronizing it; consumers holding a `PathMetadata` reference observe `null` after valid derived deserialization. |
| EC4-07 | high | bad_spec | Range success validators do not prove every required root member was present, reproducing BH4-08. |
| EC4-08 | high | bad_spec | Range validation neither verifies base64 nor compares decoded byte count with `actualBytes`, so corrupt or inconsistent byte evidence can deserialize. |
| EC4-09 | medium | patch | Carried BH3-13/EC3-07: `SearchFolderFiles` still binds the tree example and reports tree limits; the attempted change incorrectly moved the search example onto `ListFolderFiles`. |
| EC4-10 | high | bad_spec | `FileSearchResult.items` retains tree's 2,000-item limit, four times the approved 500-result search bound. |
| EC4-11 | high | bad_spec | Generated search results use omission-permissive required properties and have no root presence validator, so missing items/page/limits/freshness can look like an empty/default result. |
| EC4-12 | high | bad_spec | A terminating fallback error can still abort parity iteration, reproducing BH4-20. |
| VG4-01 | high | bad_spec | Pre-verified: no wire-level test exercises 206, and the current generated branch demonstrably throws because the method return type cannot represent `FileRangeReadPartialResult`. |
| VG4-02 | medium | patch | Pre-verified: exact 416/503 adapter tests inject already-deserialized exceptions after the generated transport boundary, so response-type selection and wire deserialization are not covered end to end. |
| VG4-03 | medium | patch | Pre-verified: operation examples are not resolved and validated against their bound response schemas; the currently swapped tree/search references leave all existing assertions green. |
| VG4-04 | high | bad_spec | Pre-verified: parity aggregation is checked only as source text, with no failure-injection execution test; the real terminating-error path in BH4-20 is uncovered. |
| VG4-O1 | high | bad_spec | The current generated 206 branch throws a typed exception rather than returning a successful partial range, reproducing BH4-01/VG4-01. |
| VG4-O2 | medium | patch | Carried BH3-13/EC3-07: List and Search operation examples are swapped, and the search-bound tree example violates the narrowed search schema. |
| PA4-01 | low | patch | Independent diff audit found an unrelated empty `/Submodules/` folder entry added to `Hexalith.Folders.slnx`; deleting that one line restores the solution file without affecting project membership. |

**Iteration 4 grouped survivors:**

- `bad_spec`: make every conforming 200/206 response an SDK success through a common usable result; distinguish validated problem DTOs from arbitrary typed results; enforce exact JSON token types, identifier bounds, and string-only enums; validate complete mutation branches and complete range/search response evidence; synchronize base/derived policy values; enforce the 500-result search limit; honor absolute-size/413 precedence and exact projection; and isolate each parity lane from terminating errors with executable failure-injection proof.
- `patch`: reject explicit whitespace hashes; complete the vocabulary example; bind and schema-validate tree/search examples; update CLI category documentation; add real-client exact-error coverage; and remove the unrelated empty solution folder.
- `defer`: the pre-existing seekable-stream position loss and unbounded CLI/MCP input buffering are real but excluded from OQ2's focused adapter allowance.
- `reject`: cross-item batch uniqueness/aggregate enforcement remains carried from BH2-05 because the registered root policy evaluator, not ordinary array keywords, is the declared machine gate.

**Iteration 5 findings:**

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH5-01 | medium | bad_spec | The two new client media-type validators allow punctuation as the first type/subtype character, while `PutFileInline` and `PutFileStream` require each component to start with an ASCII alphanumeric character. |
| BH5-02 | high | bad_spec | Both validators call whitespace-tolerant `Convert.FromBase64String` without first enforcing the 349,528-character bound or canonical spelling, so schema-invalid padded inputs can allocate and pass decoded-byte/hash checks. |
| BH5-03 | high | bad_spec | The partial-range wire validator compares requested length to actual bytes but never rejects a requested window above 262,144 bytes. |
| BH5-04 | medium | bad_spec | Search validation equates item count with `limits.actualCount` but does not require the item count to fit within `page.limit`. |
| BH5-05 | false | reject | Although `ValidateTruncation` itself checks only the flag relationship, subsequent population through the generated string-enum converter rejects an unknown value such as `banana`; the claimed accepted outcome does not occur. |
| BH5-06 | medium | bad_spec | Invalid date text is rejected by generated `DateTimeOffset` population, but non-UTC offsets remain accepted despite the `UtcDateTime` contract explicitly requiring UTC. |
| BH5-07 | medium | bad_spec | The new legacy-union validator closes the root member set even though the referenced `ProblemDetails` schema deliberately permits extension members, rejecting contract-valid legacy responses such as those carrying `taskId`. |
| BH5-08 | medium | bad_spec | The legacy-union branch applies exact-envelope title/message maxima that the base `ProblemDetails` schema does not declare, rejecting otherwise contract-valid legacy errors. |
| BH5-09 | medium | bad_spec | Optional `detail` and `instance` members are not raw-token validated, so Json.NET can coerce numeric or Boolean values into strings before projection. |
| BH5-10 | medium | bad_spec | The exact and legacy validators check `type`/`instance` only as strings and do not enforce their declared URI-reference format. |
| BH5-11 | medium | defer | The generated base `ProblemDetails.Details` has represented metadata as `Dictionary<string,string>` since before OQ2 even though the shared schema permits numbers and Booleans; correcting that broad generated SDK mismatch is not caused by this story. |
| BH5-12 | medium | bad_spec | Exact 413 recognition uses `TryGetValue` on the generated default-comparer header dictionary, so a semantically identical differently-cased HTTP header is missed. |
| BH5-13 | false | reject | OpenAPI Response Header Objects have no standard `required` keyword; declaring the header on the exact 413 response and failing closed in the SDK when it is absent is the available contract/runtime enforcement. |
| BH5-14 | high | bad_spec | The helper/postprocessor target consumes and mutates the generated client but omits it from `Inputs`, allowing a dependency-created raw client to remain unprocessed when the helper output is already current. |
| BH5-15 | high | bad_spec | The maintained recovery command invokes only raw NSwag generation; after this change that skips both helper regeneration and required client postprocessing. |
| BH5-16 | medium | patch | The generation guide still says generation runs before compilation after this change intentionally detached every generation/verification target from ordinary builds. |
| BH5-17 | high | bad_spec | The postprocessor accepts zero matches for the exceptional 206 branch without proving the successful replacement exists, so generator-template drift can silently leave the valid 206 path exceptional. |
| EC5-01 | high | bad_spec | Verified with the changed call path: an over-1-MiB CLI upload throws `ArgumentOutOfRangeException`, which `CommandPipeline` does not normalize to the canonical 422 output. |
| EC5-02 | high | bad_spec | The equivalent MCP path does not catch the new over-1-MiB `ArgumentOutOfRangeException`, so the tool faults instead of returning canonical limit metadata. |
| EC5-03 | high | bad_spec | Serialization callbacks can wrap the new request validator's `JsonSerializationException` in `TargetInvocationException`; the CLI pipeline catches neither shape and can escape its bounded usage-error contract. |
| EC5-04 | high | bad_spec | The MCP pipeline likewise fails to normalize callback-wrapped request-validation failures into its bounded failure envelope. |
| EC5-05 | medium | bad_spec | Same verified case-insensitive response-header defect as BH5-12. |
| EC5-06 | false | reject | Rejecting `.git` and descendants is an explicit approved path-policy requirement and the verification plan requires client coverage for that rejection; schema permissiveness does not make the SDK guard wrong. |
| EC5-07 | high | bad_spec | Exact 404 validation accepts any bounded message even though the approved canonical policy requires the same approved safe message and no path/policy evidence, allowing adapters to echo a disclosure-bearing message. |
| EC5-08 | high | bad_spec | `JObject.Load` uses last-value-wins duplicate handling by default, so repeated exact members can cross a boundary described as closed and unambiguous. |
| EC5-09 | medium | bad_spec | Same verified media-type grammar mismatch as BH5-01. |
| EC5-10 | high | bad_spec | Same verified missing encoded-length/canonical-base64 guard as BH5-02, including attacker-controlled response allocation before rejection. |
| EC5-11 | high | bad_spec | Same verified missing partial-window maximum as BH5-03. |
| EC5-12 | medium | bad_spec | Same verified missing `items.Count <= page.limit` relationship as BH5-04. |
| EC5-13 | medium | bad_spec | NSwag hides search-item `path` and `kind` in the narrowed derived type; the wire converter never synchronizes those values into the base `FileMetadataItem`, so polymorphic callers observe defaults. |
| EC5-14 | medium | bad_spec | An omitted optional search `truncatedReason` becomes the non-nullable generated enum's first value, making an untruncated page appear to carry `result_count_limit`. |
| EC5-15 | medium | bad_spec | The search pagination schema permits a reason on an untruncated page while the new wire validator rejects it, leaving schema and SDK behavior inconsistent. |
| EC5-16 | medium | bad_spec | The search limit schema constrains the false branch but permits `not_truncated` when `isTruncated` is true, contrary to the SDK validator and policy relationship. |
| EC5-17 | medium | bad_spec | Required `PathMetadata.pathPolicyClass` remains nullable in OpenAPI while both the canonical policy and new SDK boundary reject null. |
| EC5-18 | high | bad_spec | Same verified postprocessor fail-open condition as BH5-17. |
| EC5-19 | high | bad_spec | Writing the helper output before postprocessing, combined with the missing generated-client input, can leave the next incremental run skipping a failed client repair. |
| EC5-20 | medium | defer | Carried BH3-19: the unrelated `/pushall` detached-HEAD preservation defect is pre-existing, outside OQ2, and still present in the cumulative baseline diff. |
| EC5-21 | high | defer | Carried BH3-18: the unrelated `/pushall` clean-merge validation defect is pre-existing, outside OQ2, and still present in the cumulative baseline diff. |
| VG5-01 | medium | patch | Pre-verified: no client/CLI/MCP test exercises reserved, traversal, trailing-dot, or invalid-display path rejection, so the independent runtime path-profile validators can regress while contract-regex tests stay green. |
| VG5-02 | medium | patch | Pre-verified: builder-created stream fixtures are always consistent, leaving every outbound declared/observed/top-level length and hash comparison untested for hand-built Add/Change DTOs. |
| VG5-03 | medium | patch | Pre-verified: malformed real-client range/search fixtures do not cover decoded bytes, complete/partial relationships, counts, limits, or truncation consistency, so the new response guards can be removed without current tests failing. |
| VG5-O1 | high | bad_spec | Direct code tracing and the reviewer's executed examples confirm that new `ArgumentOutOfRangeException` and callback-wrapped `JsonSerializationException` shapes bypass both CLI and MCP normalization. |
| VG5-O2 | medium | bad_spec | Same verified legacy-extension rejection as BH5-07: the new closed root allow-list contradicts `ProblemDetails.additionalProperties: true`. |

**Iteration 5 grouped survivors:**

- `bad_spec`: make raw wire validation exactly match the declared media/base64/path/date/URI grammars; close all range/search relationships and synchronize inherited DTO state; preserve schema-valid legacy Problem Details while keeping exact envelopes duplicate-safe and disclosure-safe; normalize every new client validation exception through CLI/MCP; recognize retry headers case-insensitively; and make generation/postprocessing atomic, incremental-safe, fail-closed, and accurately documented.
- `patch`: correct the stale generation guide and add independent negative coverage for path categories, hand-built stream evidence, and every range/search relationship.
- `defer`: the pre-existing broad `ProblemDetails.details` generated-type mismatch and the carried unrelated `/pushall` defects remain outside OQ2.
- `reject`: downstream enum population already rejects unknown truncation tokens; `.git` rejection is approved; and OpenAPI cannot declare a response header as required with the proposed keyword.

**Final iteration-5 review findings:**

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BHF-01 | medium | defer | Generic `HexalithFoldersApiException<ProblemDetails>` projection can preserve a body-level `success` category and yield a successful adapter outcome when an erroneous server sends that category with a matching HTTP status. The behavior exists unchanged at the OQ2 baseline and is broader than the focused exact-problem work. |
| BHF-02 | medium | defer | `HexalithFoldersGeneratedArtifacts.VerifyCurrent` does not inspect the principal generated client, so a developer invoking that helper alone can miss a manually changed client. This predates OQ2; the current CI generated-client lane separately performs byte-for-byte isolated regeneration and catches the stale client. |
| BHF-03 | false | reject | The range request's cross-property arithmetic is declared as operation semantics and enforced by both request boundaries; ordinary OpenAPI 3.1 JSON Schema cannot compare sibling numeric values, so schema-keyword validity alone is not operation acceptance. |
| BHF-04 | false | reject | Complete/partial range relationships and decoded-byte equality are declared response semantics and enforced before SDK population; ordinary JSON Schema cannot express sibling arithmetic or decoded-length equality, so the alleged contract-valid malformed response is not contract conforming. |
| BHF-05 | false | reject | Search count, page-limit, configured-limit, and truncation relationships are declared response semantics and enforced by the wire boundary; schema-keyword validity alone does not make a relationship-violating response contract conforming. |
| BHF-06 | false | reject | The two optional members use omission for the canonical wire state; JSON Schema tooling that ignores the NSwag compatibility annotation still rejects explicit null through the declared integer/string type, while generated code obtains the intended nullable CLR representation. No widened accepted wire value results. |
| BHF-07 | false | reject | `policySnapshot.binding` declares that runtime evaluation pins the active policy's immutable version and digest; it is not a fixed published digest field. The approved canonical artifact digest is intentionally bound in the governance evidence, as already resolved by BH2-12. |
| BHF-08 | medium | patch | The governance manifest's canonical-surface inventory omitted the extension vocabulary that defines and exemplifies the root file-policy value schema. The cited server path was inaccurate, but the omission at the actual Contracts path was real. |
| BHF-09 | low | reject | `SkipRestoreBuild` is presently a no-op, but every parity lane intentionally runs `--no-restore --no-build`, and both CI and the operator guide explicitly perform restore/build first. Removing the compatibility switch would also require changing this build's recorded verification command, which review findings may not do. |
| BHF-10 | medium | patch | The global dotnet preflight exited before report initialization, so a missing SDK produced no per-lane failure records despite the all-lanes reporting contract. |
| BHF-11 | medium | patch | A custom parity report destination was written correctly but its `report_path` evidence remained the hardcoded default, making self-test provenance inaccurate. |
| BHF-12 | high | defer | The unrelated `/pushall` skill's manifest permits only Git shell commands while its workflow requires collaboration/subagent operations, so runtimes honoring that manifest cannot execute the required orchestration. Agent-context fixes are always deferred. |
| BHF-13 | high | defer | The unrelated `/pushall` default-branch selection prefers any local `main`/`master` before the remote's advertised default, which can merge and prune against the wrong branch. Agent-context fixes are always deferred. |
| BHF-14 | high | defer | Carried BH3-19: the unrelated `/pushall` flow can commit dirty detached-HEAD work before checkout and strand the commit; the code still reads as previously triaged. |
| BHF-15 | high | defer | Carried BH3-18: the unrelated `/pushall` flow validates conflict resolutions but can push a clean merge that breaks the repository; the code still reads as previously triaged. |
| BHF-16 | false | reject | `docs/contract/sdk-generation-and-runtime-behavior.md` does not exist in the repository or reviewed diff, so the claimed stale statement is not present. |
| BHF-17 | false | reject | `_bmad-output/implementation-artifacts/oq2-file-api-surface-design-spec.md` does not exist in the repository or reviewed diff, so the claimed duplicate heading and command are not present. |
| ECF-01 | medium | patch | Exact generated problem subclasses hide the base `Title` and `Message`; reading those members through `ExactFileProblem` returned null and caused adapter projection to lose the approved text. |
| ECF-02 | medium | patch | The generated base `FileMutationRequest` serialization callback passes its own `FileOperationKind` as the expected value, allowing an undefined numeric enum unless the validator independently checks `Enum.IsDefined`. |
| ECF-03 | medium | patch | A filename-only `OutputReportPath` made `Split-Path -Parent` empty and directory creation fail before synthetic parity lanes ran. |
| ECF-04 | high | defer | Carried BH3-19: the unrelated `/pushall` dirty detached-HEAD defect remains unchanged and agent-context fixes are deferred. |
| ECF-05 | low | reject | Client and helper files are each published atomically, postprocessing completes before helper publication, and an interrupted local generation remains visible and safely rerunnable. Cross-file transactional publication would add substantial complexity for a recoverable developer-only state. |
| VGF-01 | medium | patch | Pre-verified: inbound path rejection existed, but no outbound mutation test proved traversal, `.git`, trailing-dot, reserved-name, and invalid-display-name DTOs fail before transport. |
| VGF-02 | medium | patch | Pre-verified: the search wire converter omitted `byteLength` from the required content-allowed file members, so a missing value could populate a default CLR length without a negative regression test. |
| VGF-03 | medium | patch | Pre-verified: exact-problem tests did not assert projected title/message, leaving the hidden-base-member defect from ECF-01 undetected. |

**Final iteration-5 grouped survivors:**

- `patch`: preserve canonical exact-problem text through projection; reject undefined mutation kinds and prove outbound path rejection; require search item byte length; include the extension vocabulary in governance provenance; and make parity prerequisite/custom-report evidence complete and truthful.
- `defer`: the two pre-existing generated/generic-problem verification issues and four unrelated `/pushall` agent-context defects remain outside this story.
- `reject`: cross-field semantics remain operation-level because ordinary JSON Schema cannot encode them; omission remains the canonical nullable wire state; policy snapshot digest selection remains runtime-owned; the unused parity compatibility switch has no execution impact; two cited files do not exist; and cross-file generation remains atomically published per artifact and safely rerunnable.

## Verification

**Post-review patch verification:**
- The complete solution rebuilt with 0 warnings and 0 errors. The focused policy/foundation/governance/parity-generator set passed 51/51, the full client suite passed 318/318, the parity workflow conformance class passed 7/7, and the governance wrapper passed 18/18 through the expected .NET 10 in-process fallback.
- The parity wrapper again recorded all 11 lanes: nine passed and the same two pre-existing, out-of-scope REST GoldenLifecycle and mixed-surface integration lanes failed on HTTP 400-versus-202/404 behavior. The approved policy digest remains `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`, both OpenAPI and parity fixtures contain 49 operations, `Hexalith.Folders.slnx` remains unchanged from baseline, and the baseline-wide whitespace check passed.

**Iteration-5 implementation results:**
- The approved canonical policy remains LF-stable at SHA-256 `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`; the OpenAPI and regenerated parity fixture retain all 49 public operations, and `Hexalith.Folders.slnx` has no OQ2 diff.
- The complete solution built with 0 warnings and 0 errors. Focused contract/governance/parity tests passed 57/57, full generated-client/upload tests passed 318/318, CLI tests passed 741/741, MCP tests passed 691/691, consumer-document tests passed 42/42, sample tests passed 10/10, and UI tests passed 523/523.
- Deterministic isolated regeneration from a missing client passed through the documented single helper target; the new drift negative control proved an unrecognized client shape fails before helper publication. Exact-envelope omissions and governed values, range/search relationships, legacy extensions, canonical wire grammars, and inherited generated members ran through the client suite.
- The governance wrapper passed its 18-test in-process fallback after the expected .NET 10 VSTest incompatibility. The parity wrapper recorded all 11 lanes: nine passed, while the unchanged, explicitly out-of-scope REST GoldenLifecycle and mixed-surface integration lanes retained their pre-existing HTTP 400-versus-202/404 failures.
- `git diff --check 822831269766c5824e122ddc725216d15f5374b3 -- .` passed.

**Iteration-4 implementation results:**
- The approved canonical policy remains LF-stable at SHA-256 `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`; the regenerated parity fixture retains all 49 public operations.
- The complete solution and maintained sample built with 0 warnings and 0 errors. Focused contract/governance/parity tests passed 57/57, generated-client/upload tests passed 55/55, CLI tests passed 739/739, MCP tests passed 689/689, and consumer-document tests passed 22/22.
- The governance wrapper passed its 18-test in-process fallback after the expected .NET 10 VSTest incompatibility. The executable parity failure-isolation control recorded all three synthetic lanes and preserved aggregate failure.
- The normal parity wrapper recorded all 11 lanes and ran the CLI and MCP lanes after earlier failures. Nine lanes passed; the unchanged, explicitly out-of-scope REST GoldenLifecycle and mixed-surface integration lanes remain failed on their pre-existing HTTP 400-versus-202/404 runtime behavior.
- `git diff --check 822831269766c5824e122ddc725216d15f5374b3 -- .` passed, and `Hexalith.Folders.slnx` has no OQ2 diff.

**Iteration-3 implementation results:**
- The approved canonical policy remains LF-stable at SHA-256 `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`; the evidence and planning surfaces retain version `1.1.0`, the three `Administrator` approvals dated 2026-09-14, and the explicit runtime/public-batch deferrals.
- The generated-artifact verification target passed, deterministic isolated regeneration passed, and the complete solution built with 0 warnings and 0 errors. The sample and maintained pattern-example projects also compile against the generated policy enum.
- Focused contract/foundation/governance/parity-generator tests passed 54/54; the parity-wrapper conformance tests passed 5/5; consumer-document conformance passed 22/22; and the governance wrapper passed its 18-test in-process fallback after the expected .NET 10 VSTest incompatibility.
- Focused generated-client/upload tests passed 51/51, CLI tests passed 37/37, MCP tests passed 19/19, and affected UI tests passed 107/107.
- The parity fixture was regenerated with 49 operations. The parity wrapper executed and reported all 11 lanes, preserved aggregate failure, and ran the CLI and MCP lanes successfully after the known REST integration failure. Nine lanes passed; the unchanged out-of-scope REST GoldenLifecycle lane and mixed-surface integration lane remain failed on pre-existing HTTP 400-versus-202/404 runtime behavior.
- `git diff --check 822831269766c5824e122ddc725216d15f5374b3 -- .` passed.

**Iteration-2 implementation results (historical; reverted for iteration 3):**
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
