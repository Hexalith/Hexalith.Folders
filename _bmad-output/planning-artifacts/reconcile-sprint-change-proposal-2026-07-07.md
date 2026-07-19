---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md
source_date: 2026-07-07
source_status: approved-no-action
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: no-prd-change
---

# Reconciliation — Canonical `.slnx` Inventory Drift

## Source purpose and status

This correct-course proposal investigates a reported drift between `Hexalith.Folders.slnx`, the scaffold conformance test's pinned solution inventory, and the root-policy project inventory while preserving the intentionally curated subset of submodule projects.

The approved result is **no action—already reconciled**. Jerome approved the no-op close on 2026-07-07. Direct comparison and a live scaffold-test run showed that the alleged drift did not exist: the solution and both pinned inventories matched, the curated EventStore subset was intact, and the relevant test lane was green. The trigger came from stale project-memory wording already superseded by the Story 11.1 baseline and a later memory correction. No code, pin, sprint-status, PRD, architecture, epic, or UX change was authorized.

## PRD-relevant product decisions

There is no new product decision or requirement.

The only PRD-relevant interpretation is negative: this source's “canonical inventory” means a repository build/scaffold project list. It is not the OpenAPI Contract Spine, the C13 operation/surface inventory, or a product capability denominator. A green or red `.slnx` pin does not add, remove, or redefine user-visible scope.

The proposal also reinforces a general evidence discipline already compatible with the PRD: current repository evidence supersedes stale historical prose, and a non-reproducible premise must not be converted into a product or delivery change.

## Already covered in the current PRD and memlog

- **Project Context** already says current repository contracts and approved governance artifacts take precedence over historical delivery-status prose. That authority rule prevents a stale memory note from redefining current scope.
- **MVP Contract Summary**, **Public Surfaces**, **CM4**, **FR3**, **FR47–FR51**, and exit criterion **C13** define the actual product-level contract/parity inventory. None refers to solution-project membership.
- **C13** explicitly says the generated Contract Spine operation inventory—not a hard-coded count in the PRD—is the binding surface denominator. The `.slnx` counts in this source are unrelated.
- The memlog's authority decisions consistently reserve the PRD for product intent/scope and the Contract Spine/C13 artifacts for machine-contract and surface coverage. It contains no decision making `.slnx` membership a product requirement.
- The current PRD has no claim that all projects present in a submodule must appear in the root solution, so the intentionally curated subset creates no product-contract gap.

## Genuine PRD gaps

None.

The source explicitly finds no defect and proposes no product, scope, or artifact change. Adding solution counts, project paths, submodule membership, or scaffold-pin requirements to the PRD would turn volatile repository structure into product scope and confuse build governance with C13 product-contract governance.

## Conflicts and supersession

### 1. The triggering defect is superseded as a false premise

The claimed pre-existing `.slnx` inventory red was contradicted by exact inventory comparison, baseline history, and a green 10-test live run. Do not carry the defect statement into the PRD, memlog, open items, or release blockers.

### 2. Historical counts and SHAs are evidence, not durable requirements

The 50 solution entries, 29 root-policy projects, 31 Folders-owned entries, 19 submodule entries, curated 8-of-19 EventStore subset, and baseline SHAs describe the July 7 tree. They may change legitimately through lockstep repository work and must not be pinned in product prose.

### 3. “Inventory drift” must not be conflated with CM4/C13 drift

The current PRD's **CM4** and **C13** concern Contract Spine operations and cross-surface product behavior. The source concerns `.slnx`/`.csproj` membership only. No CM4, C13, FR, or surface-conformance update follows from this no-op investigation.

### 4. Residual stale memory is outside PRD reconciliation

The source notes one residual historical phrase saying there was a pre-existing inventory red; rewording it was explicitly deferred. That stale phrase remains superseded by current evidence but is project-memory hygiene, not a PRD open question.

## Implementation, test, and repository-governance detail that stays out of the PRD

- `Hexalith.Folders.slnx`, `ScaffoldContractTests`, `ExpectedSolutionProjects`, `ExpectedRootPolicyProjects`, `SolutionContainsOnlyCanonicalBuildableProjects`, and `RepositoryRoot()` walkers are build/test implementation.
- Exact solution/project counts, exclusion rules, EventStore disk/project ratios, file hashes/SHAs, byte-identical comparisons, and `diff` output are verification details.
- The curated root/submodule project selection and rule that `.slnx` plus pinned arrays change in one commit are repository governance.
- Story 11.1 pin-map sections, future Story 11.5/11.8/11.9 examples, memory-file wording, and sprint-status non-change are delivery context.
- The 10/10 test result and specific `dotnet test` filter are session evidence.

## Recommended stable-ID edits or additions

- **Add no FR, NFR, OQ, journey, metric, or exit criterion; renumber nothing.**
- Retain **CM4**, **FR3**, **FR47–FR51**, and **C13** unchanged; they govern product contract/surface parity, not solution structure.
- Do not introduce a PRD “solution inventory” ID or copy the July 7 project counts into any stable product requirement.
- If future project-structure changes alter product surfaces or public contract behavior, reconcile the affected existing stable IDs based on that actual product change. A `.slnx`-only lockstep update remains outside the PRD.

## Qualitative ideas at risk

- A no-op is the correct outcome when current evidence disproves the trigger; acting anyway can create the drift the change request sought to prevent.
- A curated solution is intentionally not a mirror of every project present in every submodule.
- Current executable evidence and baseline comparisons outrank stale memory phrasing.
- Real structural changes must update the solution and its conformance pins atomically so the master lock stays meaningful.
- Build/scaffold inventory integrity and product contract/surface integrity are both important but are different governance domains.
- Historical verification counts are useful provenance, not enduring product commitments.

## Disposition

**No PRD change.** Accept the source as an approved verification-only no-op and as evidence that the July 7 `.slnx` drift premise was false. Preserve the current PRD and memlog. Keep solution curation, pinned arrays, baseline counts, stale memory cleanup, and lockstep repository guardrails in project governance and tests.
