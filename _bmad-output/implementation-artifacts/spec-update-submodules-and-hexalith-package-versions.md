---
title: 'Update root submodules and Hexalith package versions'
type: 'chore'
created: '2026-09-05'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '108d4504f317a77e58b2c6e94e8d6364b02d33e1'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-update-dotnet-packages-submodules.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Root submodule gitlinks and Hexalith NuGet pins can lag upstream. Fresh fetches show `references/Hexalith.Memories` behind `origin/main` by one fast-forward commit while the other seven root gitlinks and published Hexalith family pins already match their remotes and the Builds catalog.

**Approach:** Advance only root-declared gitlinks that lag a fetched fast-forward `origin/main`, re-verify Hexalith package currency against the Builds-owned catalog (no local overrides), and synchronize active guidance if pointers change. Leave catalog/SDK/AppHost pins unchanged unless fresh evidence shows Folders-owned skew.

</frozen-after-approval>

## Implementation Notes

- Fresh-fetched all eight root remotes. Only `references/Hexalith.Memories` lagged: advanced parent gitlink `396f8381c5627424b67b1b55441ceff7d782e1c1` → `2f85536d650283548fdc617f2e35b12620b3ca8d` after `git merge-base --is-ancestor` confirmed fast-forward (tip is CI/BMAD skill fix; no `src/` API delta).
- Hexalith NuGet currency: sampled family IDs matched catalog pins; full gate is the Builds audit (286 packages / 141 families), not the sample list. Builds gitlink already at `origin/main`; no catalog edit. SDK `10.0.400` and AppHost SDK `13.5.3` unchanged.
- Commands run: `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-consumer-package-authority.ps1 -RepositoryRoot . -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props` (32 projects); `…/validate-package-version-exceptions.ps1 -InventoryPath …/package-version-exceptions.json -CatalogPath …/Directory.Packages.props` (15 exceptions; no `-WorkspaceRoot`); `…/validate-package-version-audit.ps1 -CatalogPath …/Directory.Packages.props -AuditPath …/package-version-audit.json` (286/141 passed).
- Nested worktrees remain uninitialized (root-only update; no recursive/`--remote`). Pre-existing EventStore nested FrontComposer/Memories object skew vs root gitlinks was not changed and was not treated as a blocker for a Memories-only root bump.
- `dotnet restore Hexalith.Folders.slnx` succeeded; `dotnet build Hexalith.Folders.slnx --no-restore` failed on pre-existing errors in unchanged `ForgejoProvider.cs` (CS1513/CS1519) and unchanged EventStore `AggregateActor.cs` (`InspectPublicationRecoverySaveFailureAsync`). Not introduced by this gitlink bump.
- Synced `_bmad-output/project-context.md` Last Updated to 2026-09-05 (technology guidance already listed current Hexalith pins / eight roots).

## Review Triage Log

- Blind Hunter: omitted oneshot template sections — **false** (oneshot route deletes Boundaries/Code Map/Tasks/Verification by design).
- Blind Hunter: status in-progress while notes claim completion — **low**/process; fixed by setting `status: done` at finalize.
- Blind Hunter: split staging across gitlink/guidance/spec — **medium**/patch; stage all three together before commit.
- Blind Hunter: date-only project-context sync insufficient — **false** (guidance content already current; date stamp is the intentional sync).
- Blind Hunter: PolymorphicSerializations missing from init list — **medium**/defer (pre-existing doc drift).
- Blind Hunter: validator commands not recorded — **low**/patch; added exact commands to Implementation Notes.
- Blind Hunter: nested FrontComposer/Memories coherence unchecked — **medium**/defer (EventStore nested skew pre-exists; out of scope for Memories-only root bump).
- Blind Hunter: incomplete Hexalith family enumeration — **low**/patch; clarified sample vs full audit gate.
- Blind Hunter: no Debug build after gitlink advance — **medium**/patch; recorded restore success and pre-existing build failures.
- Blind Hunter: no Always/Never in frozen Intent — **false** (oneshot omits Boundaries section).
- Blind Hunter: nested safety under-documented — **low**/patch; expanded to root-wide uninitialized nested worktrees.
- Blind Hunter: Intent still says Memories lags — **false** (frozen Problem is planning-time state; do not edit frozen block).
- Blind Hunter: prior-spec verification checks not carried forward — **low**/rejected (oneshot keeps notes lean; merge-base and validators recorded).
- Blind Hunter: no Conventional Commit plan — **low**/process; commit created at finalize with commitlint.
- Blind Hunter: EventStore AggregateActor build break — **high**/defer (unchanged EventStore tip; not caused by this change).
