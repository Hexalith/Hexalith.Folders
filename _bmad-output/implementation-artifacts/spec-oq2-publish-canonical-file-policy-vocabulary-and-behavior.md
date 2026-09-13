---
title: 'Publish OQ2 canonical file-policy vocabulary and behavior'
type: 'feature'
created: '2026-09-13'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '822831269766c5824e122ddc725216d15f5374b3'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ2 blocks Stories 12.1, 12.3, and 4.20 because exact file-policy vocabulary and behavior remain provisional across the canonical contract, OpenAPI, and tests.

**Approach:** Publish a versioned canonical policy, bind PM, Architecture, and Security approval to its digest, align contract-facing artifacts, and add offline cross-surface conformance gates without claiming the downstream runtime implementation is complete.

## Boundaries & Constraints

**Always:** Preserve authorization/path policy before observation, atomic change-set validation, relative forward-slash paths without retargeting, C4 limits, D-9's 262,144-byte transport boundary, metadata-only diagnostics, and reopen-on-digest-change governance.

**Never:** Implement runtime content/workspace/provider/query behavior, CLI/MCP behavior, OQ3, broad PD10 cleanup, or claim FR32-FR35 runtime evidence.

## Approved Decisions

- Use the conservative path profile: ASCII `A-Z a-z 0-9 . _ - /`, 500 characters maximum, NFC declaration, ordinal-ignore-case collision detection, and rejection of every touched symlink/reparse entry or ancestor without following it.
- Bound mutations to 1 MiB per file, 100 changes, and 10 MiB aggregate. Strict UTF-8 with an optional BOM is content-readable; binary and other encodings may be mutated but remain metadata-only.
- Use server-owned classes `content_allowed`, `metadata_only`, `excluded`, and `restricted`. A bounded include allowlist is required, exclusions always win, re-inclusion is unsupported, and invalid or unavailable policy fails closed.
- Return 404 `tenant_access_denied/resource_unavailable` for missing, excluded, restricted, sensitivity-denied, and unauthorized paths. Reserve 416 for an authorized, visible, unsatisfiable range.
- Record `Administrator` as PM, Architecture, and Security approver on 2026-09-13, following the OQ1/OQ8 precedent.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Canonical path | Authorized policy-valid relative path | Server classifies; caller spelling is preserved | No retargeting |
| Ambiguous path | Traversal, link/reparse target, reserved name, or case alias | Reject before observation; apply nothing | No path echo |
| Content boundary | Text, binary, encoded, or oversized content | Apply one cross-surface classification/size matrix | No truncation |
| Hidden content | Missing, excluded, restricted, sensitivity-denied, or unauthorized path | Use one approved non-enumerating public outcome | No existence, policy, count, or path disclosure |
| Approved evidence | Matching policy version/digest and three approval records | OQ2 is closed as a governed design decision | Runtime gaps remain explicit |

</frozen-after-approval>

## Code Map

- `docs/contract/file-context-contract-groups.md` -- versioned canonical policy; close OQ2 TODOs but retain unrelated runtime deferrals.
- `docs/contract/oq2-file-policy-evidence.yaml` -- new digest/approval manifest with runtime posture.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- align `PathMetadata`, content semantics, routing, and examples; regenerate derived files only if needed.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/{FileContextContractGroupTests,GovernanceCompletenessGateTests}.cs` -- exact policy and bounded governance gates, reusing C7/OQ8 patterns.
- `docs/contract/governance-and-completeness-ci-gates.md`, `.gitattributes`, and planning PRD/architecture/epics/memlog -- bind LF-stable evidence and synchronize closure without claiming runtime completion or editing the stale generated manifest.
- `src/Hexalith.Folders/**`, server, workers, providers, and UI -- do not change; downstream stories own their fail-closed gaps.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/contract/file-context-contract-groups.md`, `docs/contract/oq2-file-policy-evidence.yaml`, `.gitattributes`, and planning artifacts -- publish the policy, bind three approvals, and synchronize closure.
- [ ] `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and the two mapped contract test files -- align semantics/examples and add positive/negative conformance gates.

**Acceptance Criteria:**
- Given the approved decisions, when the canonical document and OpenAPI are inspected, then behavior is closed, bounded, cross-surface, and has no OQ2 placeholder.
- Given missing, mismatched, stale, extra, or incomplete approval evidence, when offline gates run, then OQ2 fails closed with bounded metadata-only diagnostics.
- Given OQ2 governance is approved, when planning and evidence are inspected, then OQ2 is closed while Stories 12.1/12.3/4.20 and FR32-FR35 runtime proof remain incomplete.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1` -- expected: zero warnings/errors.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: OQ2 version/digest/approval checks pass.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests` -- expected: focused policy and governance tests pass.
- `git diff --check` -- expected: no whitespace errors.
