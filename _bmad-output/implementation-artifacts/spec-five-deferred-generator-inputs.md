---
title: 'Harden parity-oracle generator inputs for five deferred items'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete DW-78, DW-81, DW-82, DW-84, and DW-86 as one bounded generator input-integrity change. Approval evidence must remain inside the repository using platform-appropriate path comparison and reject symlink traversal. Baseline initialization must refuse an existing target unless `--force-baseline-overwrite` is explicit, while still validating the candidate before writing. Audit metadata keys must be unique under the existing strict lowercase vocabulary. Parameter normalization must reject malformed leading separators/digits and invalid identifiers rather than silently discarding characters. Malformed read-consistency metadata must fail with an actionable `prerequisite-drift` diagnostic before output is written.

Preserve valid generated parity bytes and historical baseline protection. Add focused CLI regressions, update generator usage documentation and the five ledger entries, run the relevant checks, review the result, then commit and push on `main` as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation: `tests/tools/parity-oracle-generator/Program.cs` owns all five validation seams; `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` already invokes the compiled tool and checks byte stability. Reuse that harness and preserve the schema/contract fixtures.
- The 2026-08-25 decision on DW-81 already selects explicit force for overwriting. No unresolved intent choices, migrations, dependency changes, or deployment are needed. The only external action is the user's requested push after verification.
- Starting revision: `ed50d2a557edadd7a4c2e294ff75edc46962c8c3` on clean `main`.

- Implemented all five seams in the generator, CLI regressions in the existing parity test class, usage updates in the tool README and three contract guides, and ledger resolutions. No generated fixture or source-contract bytes changed.
- Generator and Contracts.Tests Debug builds succeeded. The latter reports an existing MSB3277 Aspire.Hosting 13.6.3/13.6.4 assembly conflict warning; no build errors. Formatting checks for both changed C# files passed.
- Verification: `dotnet build tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj --no-restore -v minimal` and `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -v minimal` succeeded. Direct execution with `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class '*ParityOracleGeneratorTests' -noLogo` passed all 55 tests, zero skipped or failed, including byte-stability against the committed fixtures. Targeted `dotnet format whitespace ... --no-restore --verify-no-changes --include ...` checks passed for both changed C# files; `git diff --check` passed.

- Review patches: inspect original approval path segments before collapsing parents; reject linked baseline outputs; validate names in referenced parameter components while retaining existing alias-based field inference; reject YAML null freshness values; use absolute audit-key regex anchors. Diagnostics now identify the operation or path-parameter source. Added focused regressions for these paths.

## Review Triage Log

- Medium, patched — approval paths containing `linked/../approval.md` normalized away the linked component before inspection. The link walker now examines original path segments; the regression also preserves separate inside/outside approval files.
- Medium, patched — unquoted or explicitly tagged YAML null freshness values were treated as nonempty text. Validation now recognizes YAML null values, with plain/case/tilde/tagged regressions.
- False, rejected — the multiline-freshness subclaim does not violate the vocabulary: `freshnessBehavior` is a string without a single-line constraint, and the generator never renders that value. Positive regressions preserve valid multiline and quoted `null` strings.
- Medium, patched — component parameter names bypassed validation because only reference aliases were normalized. Local component references are now resolved and their actual names validated; malformed-reference-name regressions cover the common IdempotencyKey component.
- Medium, patched — forced baseline output could alias the candidate through a file or directory symlink. Linked output components are rejected before directory creation or file opening; both regressions assert candidate bytes remain unchanged.
- Medium, patched — the existing `$` audit-key anchor accepted a final newline during baseline initialization. Absolute anchors reject the malformed key before replacement, with a forced-initialization regression preserving existing bytes.
- Low, patched — generic new diagnostics made malformed metadata hard to locate among operations. Read-consistency/audit messages now include the operation ID; parameter messages include their operation/path source and referenced component pointer.
- The first expanded run passed 66/68 tests; the two new linked-output cases reached an unrelated missing historical-input exception during baseline rendering before reaching the path guard. Moved linked-output validation ahead of baseline rendering, retaining candidate inventory validation before any write.


## Final Verification

- After review patches, the generator Debug build and both targeted whitespace checks passed. The Contracts.Tests Debug build passed with the pre-existing MSB3277 warning noted above.
- Final direct-runner execution of `ParityOracleGeneratorTests` passed 68/68 tests, zero failed or skipped, in 56.101 seconds. Valid generated oracle and baseline bytes remain identical to the committed fixtures.
- All verified review findings were patched; the multiline-string subclaim was rejected with vocabulary evidence. No new work was deferred.
- `git diff --check` and `node_modules/.bin/commitlint --edit /tmp/folders-five-deferred-generator-commit.txt` passed. The user authorized the final push to `main`.
