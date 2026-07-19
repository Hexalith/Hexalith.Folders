---
title: 'Fix HXF-OPS-001 submodule policy contract'
type: 'bugfix'
created: '2026-07-19'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: 'cfe830b410bce6e04308ea67c3492eca6bc8bdfd'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `ScaffoldContractTests.SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup` requires the Folders-specific root-submodule initialization command in `AGENTS.md` and `CLAUDE.md`. Those files are universal, synchronized entry points whose own contract requires repository-specific guidance to remain in repository documentation, so the current assertion makes the baseline red while enforcing the wrong ownership boundary.

**Approach:** Split the scaffold assertion into universal-policy and repository-setup document roles. Require the explicit root-only initialization command in `README.md` and `tests/README.md`, require the non-recursive safeguard across all universal and repository documents, and guard universal entry points against acquiring the repository-specific command.

## Boundaries & Constraints

**Always:** Preserve the current eight-entry root `.gitmodules` inventory contract; keep nested/recursive initialization forbidden by default; include `.github/copilot-instructions.md` with the synchronized universal entry points; retain the existing broad policy-document scan for unsafe recursive setup commands; use ordinal, case-insensitive document checks consistent with the surrounding test.

**Ask First:** Any change to repository documentation, universal entry-point text, `.gitmodules`, CI workflows, dependency pins, or submodule checkouts requires separate confirmation.

**Never:** Add the Folders-specific initialization command to `AGENTS.md`, `CLAUDE.md`, or `.github/copilot-instructions.md`; weaken or remove recursive-command violation detection; initialize, update, reset, or edit any submodule; overwrite the existing Story 12.6 bookkeeping changes or the externally advanced EventStore/FrontComposer checkouts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Repository setup documentation | `README.md` or `tests/README.md` | Complete non-recursive command names all required root submodules and the document forbids recursive default setup | Missing command or prohibition fails with the document name |
| Universal entry point | `AGENTS.md`, `CLAUDE.md`, or Copilot instructions | Contains the universal recursive-init prohibition but no Folders-specific canonical command | Repository-specific command or missing prohibition fails with the document name |
| Unsafe setup guidance | Any scanned policy document contains an unapproved recursive default command | Existing violation scan reports the path and line | Test remains red until unsafe guidance is removed or explicitly recognized by existing policy rules |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- owns the failing submodule-policy assertion, canonical root inventory, and recursive-command policy scan.
- `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` -- synchronized universal entry points that must remain repository-agnostic.
- `README.md`, `tests/README.md` -- repository-specific setup documentation that already contains the canonical root-only command.

## Tasks & Acceptance

**Execution:**
- [ ] `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- separate universal and repository document assertions, accept the universal “never use recursive” wording, include the Copilot entry point, and add an absence guard for repository-specific commands in universal files.

**Acceptance Criteria:**
- Given the current correctly separated documentation, when the focused scaffold test runs, then it passes without changing any documentation or submodule checkout.
- Given any repository setup document loses the complete root-only command, when the assertion runs, then it fails and identifies that document.
- Given any universal entry point gains the Folders-specific command or loses the recursive-init prohibition, when the assertion runs, then it fails and identifies that document.
- Given unsafe recursive default guidance appears in the existing scanned policy surface, when the scaffold test runs, then the existing violation detector still fails with path and line evidence.

## Spec Change Log

## Design Notes

Treat command presence and prohibition presence as separate predicates. This reflects two distinct ownership contracts and avoids another all-documents loop that accidentally imposes repository details on universal files. Preserve the canonical inventory constant and recursive violation detector as the existing sources of truth; this correction changes only which documents must satisfy which predicate.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --configuration Release --no-restore` -- expected: build succeeds with zero warnings and errors.
- `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Release/net10.0/Hexalith.Folders.Testing.Tests.dll -method 'Hexalith.Folders.Testing.Tests.ScaffoldContractTests.SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup'` -- expected: one focused test passes.
- `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Release/net10.0/Hexalith.Folders.Testing.Tests.dll -method 'Hexalith.Folders.Testing.Tests.ScaffoldContractTests.RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption'` -- expected: the recursive-guidance negative control passes.
- `git diff --check` -- expected: no whitespace errors.
