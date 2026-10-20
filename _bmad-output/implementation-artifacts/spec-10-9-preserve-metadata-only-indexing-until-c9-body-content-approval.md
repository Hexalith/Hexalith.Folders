---
title: 'Story 10.9: Preserve metadata-only indexing until C9 body-content approval'
type: 'bugfix'
created: '2026-10-19'
status: 'ready-for-dev'
baseline_commit: 'b49b0b206238056b3aa3c963edc44b1b6fc0f5cc'
route: 'dispatch'
review_loop_iteration: 0
story_key: '10-9-authorized-body-content-materialization-c9-gated'
context:
  - '_bmad-output/implementation-artifacts/epic-10-context.md'
  - '_bmad-output/planning-artifacts/authority-relock/2026-09-17/A4-PD5.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved metadata-only guard permits replacement materializers and forwards arbitrary curated or archived documents to Memories.

**Approach:** Reject replacements before construction or invocation, and validate both publication paths against the 10.6 metadata vocabulary. Use the stable, content-free reason `metadata_only_policy_violation`.

## Boundaries & Constraints

**Always:** Follow A4-PD5 and current Epic 10. Preserve authorization precedence, C4, opaque identity, nine approved attributes, stable event IDs, hard deletion, and safe archive fallback. Keep MetadataDerived registered and FailClosed constructible. Preserve the old frozen spec, approved tracker history, and EventStore user edits.

**Never:** Add body readers, bytes to requests, embeddings, RAG, snippets, enablement flags, tenant exceptions, or C9 approval. Change public contracts, dependencies, authorization, or shared platform plumbing. Future body capability needs a new approved requirement/story/rank/dependency path and C9 release evidence. Hermetic tests cannot prove deployed FR58.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Metadata | Real 10.6 document | Existing canonical tokens and attributes publish | Existing transport outcomes |
| Replacement | Type/factory/instance/duplicate, before/after registration | Reject startup and manager resolution before replacement executes | Stable policy/configuration error |
| Contamination | Extra text/keys, altered allowed values, identity mismatch | No upsert or archive publication; no rejected-content evidence | Nonretryable policy reason |
| Archive | Valid metadata or absent legacy evidence | Approved archive or existing identity-only fallback | Preserve pruning |
| Denial/cancellation | Denied policy or cancelled caller | No materialization on denial; cancellation propagates | Existing precedence |

</frozen-after-approval>

## Code Map

Worker paths below resolve under `src/Hexalith.Folders.Workers/`; test paths under `tests/Hexalith.Folders.Workers.Tests/`.

- `FoldersWorkersModule.cs`: TryAdd accepts overrides; manager resolution currently constructs them. Existing validate-on-start pattern is in `FoldersTenantEventSubscriptionOptionsValidator.cs`.
- `SemanticIndexing/MetadataDerivedSemanticIndexingContentMaterializer.cs`: canonical 10.6 tokens; no content source.
- `SemanticIndexing/MemoriesSemanticIndexingPort.cs`: copies upsert and persisted archive text/attributes unchecked.
- `src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs`: reuse shared keys unchanged.
- `SemanticIndexingEndpointE2ETests.cs`: host fixture replaces the materializer; adapt to the real implementation.

## Tasks & Acceptance

**Execution:**
- [ ] Add `SemanticIndexing/SemanticIndexingMaterializerRegistrationGuard.cs` and `SemanticIndexing/SemanticIndexingMetadataOnlyOptionsValidator.cs`; wire startup validation and guarded manager resolution in `FoldersWorkersModule.cs`. Inspect live descriptors without resolving replacements; require exactly one MetadataDerived type descriptor, no factory/instance. Validate before constructing manager dependencies.
- [ ] Add `SemanticIndexing/SemanticIndexingMetadataOnlyPolicy.cs`: exact canonical text, approved classifications, closed attributes, identity-bound values. Reuse canonical formation with the existing materializer where needed; no new public API.
- [ ] Update `SemanticIndexing/MemoriesSemanticIndexingPort.cs`: reject invalid upserts and preserved archives without echoing content. Keep safe absent-evidence fallback and hard removal.
- [ ] Add `SemanticIndexingMetadataOnlyPolicyTests.cs`: body, snippet, path, URI, diff, prompt, provider, and credential sentinels in text, extra keys, allowed-value substitutions, and archive evidence. Assert stable nonretryable failure and zero Dapr calls.
- [ ] Extend `SemanticIndexingWorkerRegistrationTests.cs`: pre/post overrides, factories, instances, duplicates, startup, resolution, zero construction/invocation, and approved publication fixtures. Adapt `SemanticIndexingEndpointE2ETests.cs` host fixtures; retain policy-deny/C4 coverage.

**Acceptance Criteria:**
- Given replacement registration, when startup or production manager resolution runs, then failure precedes its factory or materialization code.
- Given valid or contaminated documents, when either publication path runs, then only approved metadata publishes; rejected content never enters errors or indexed evidence.
- Given denial, cancellation, removal, or absent archive evidence, when processed, then existing safe behavior passes regression coverage.
- Given unavailable deployed verification, when reporting results, then identify that evidence gap separately from hermetic results and FR58 acceptance.

## Implementation Notes

Planning: A4-PD5 and A8 supersede the older incomplete-only scope. Preserve their approved lifecycle decisions. HEAD advanced externally to the recorded baseline during investigation. No intent gaps or irreversible actions; footprint is Worker policy, registration, publication, and tests.

## Spec Change Log

## Review Triage Log

## Verification

- Build `tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj` in Debug with project references; run its built xUnit host directly.
- Build/run existing `ContextSearchQueryHandlerTests` and `SafetyInvariantGateTests` through their project hosts with direct class filters.
- Inspect actual Worker HTTP-host negatives. Record exact environment blockers and missing deployed DCP evidence; do not seed or bypass prerequisites.
