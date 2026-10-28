---
title: 'Harden safety gate inputs and bounded diagnostics'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete five deferred safety-gate items: DW-72 (duplicate JSON properties), DW-73 (Linux diagnostic paths), DW-232 (encoding handling), DW-244 (bounded JSON/YAML parsing), and DW-245 (validated diagnostic fields). The gate must reject ambiguous or unsupported inputs with fixed metadata-only diagnostics while continuing to scan the checked-in fixtures and artifacts. Use strict UTF-8 with an optional UTF-8 BOM as required by repository configuration, rather than guessing another encoding. Apply size/depth bounds to structured fixtures and keep parser details, paths, and payloads out of errors. Preserve existing channel and sentinel semantics. Commit and push this work on main, preserving and excluding the six pre-existing submodule changes as authorized by the user.

</frozen-after-approval>

## Implementation Notes

- Investigation: all five gaps are in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`. Reuse the existing gate class and runner selection; partial files may separate input helpers and regression tests without changing project references or public APIs. Update `docs/contract/safety-invariant-ci-gates.md` and selected ledger entries.
- Existing `AssertMetadataOnly` already rejects home-directory paths; add remaining temporary and variable-data roots. Existing source decoding silently replaces invalid bytes; strict UTF-8 should fail safely. All JSON consumers, including lazy exclusion loading, must share duplicate/depth checks. YAML needs bounded preflight before constructing representation nodes.
- No unresolved intent gaps or irreversible implementation effects. Footprint: test class and focused partial files, gate documentation, ledger, this spec. Push is explicitly authorized. No dependency, contract, runtime, or submodule edits are needed.
- Verification: build the Contracts.Tests project in Debug; run the safety class through the direct xUnit assembly runner. Verify negative controls for duplicate/escaped keys, encodings, depth/size limits, and invalid diagnostic fields, plus all existing safety tests. Run relevant formatting and normal commitlint before pushing.
- Implemented shared strict UTF-8 reading, recursive JSON duplicate checks, bounded YAML preflight, approved diagnostic validation, and Linux/checkout path detection. Added partial input helper and regression files without changing the gate runner or project references; documented all limits.
- Baseline: `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --configuration Debug --no-restore -m:1` passed, with an inherited MSB3277 Aspire.Hosting version conflict warning; baseline safety runner passed 11 tests.
- Runtime baseline blocker: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` exited 2: AppHost build failed with CS1704, duplicate Hexalith.Commons.UniqueIds source/package references in Server. `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json --non-interactive` confirmed no running AppHost. This test-only change does not need a running topology.
- Focused verification: the same Debug project build passed; `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests` passed all 28 tests, including the 11 original tests and 17 new regression cases.
- Formatting: `dotnet format whitespace tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --verify-no-changes --no-restore --include tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.Inputs.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.InputTests.cs` passed. `git diff --check` and commitlint validation passed. Remote main matched the starting commit `d7e0b3f307e4cd7e740b9f38923c86f8b5c30847` after fetch.
- Independent blind review returned five findings. All were checked and patched; no review findings were deferred. Added the helper and regression partials to both the assertion-messages artifact sources and the inventory include roots so extraction retains existing scan coverage.
- Final verification after review corrections: the focused Debug build passed (same inherited MSB3277 warning); all 35 safety tests passed with zero errors, failures, skips, or unrun cases; focused whitespace verification passed. The malformed Unicode cases now prove fixed messages and no unsafe inner exceptions.

## Review Triage Log

- R1 — medium, patch: JSON escaped lone surrogate property names reproduced a raw decoding exception during duplicate validation. Normalize deferred string-decoding errors, validate scalar strings as well as names, and add four high/low-surrogate regression cases.
- R2 — medium, patch: YAML out-of-range Unicode escapes reproduced ArgumentOutOfRangeException outside the YamlException catch. Normalize parser argument errors and add three invalid-code-point regression cases.
- R3 — medium, patch: moving diagnostic code to partial files removed it from the explicit assertion-messages inventory. Enroll both partial files in artifact sources and include roots; the full gate now validates and scans them.
- R4 — low, patch: Unicode line and paragraph separators bypassed char.IsControl and could enter diagnostic hints. Reject both separator characters and add negative controls; document the constraint.
- R5 — low, patch: a second FileStream.Length read could allocate beyond the checked size during concurrent growth. Capture and validate the length once, allocate from that same value, and retain the trailing-byte growth check. Existing file-boundary tests remain green.
