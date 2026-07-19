# Reconciliation — Sprint Change Proposal: Builds Package Version Centralization

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06-builds-package-version-centralization.md`  
**Source date:** 2026-07-06  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **No-op for the PRD.** Retain the proposal as implementation/architecture evidence; do not add an FR, NFR, release item, or PRD addendum entry.

## Source purpose and status

The source records a minor correction to NuGet package-version ownership. Hexalith.Folders imported `references/Hexalith.Builds/Props/Directory.Packages.props` but also retained local `PackageVersion` rows and overrides, creating two potential sources of truth. The proposed direct adjustment makes the root `Directory.Packages.props` import-only, enables `CentralPackageTransitivePinningEnabled`, moves/defaults package-version ownership to Hexalith.Builds, and redirects the Octokit package guard to the shared source.

The proposal classifies the change as **Minor**, low effort, low risk, with no timeline impact and a developer-direct route. Its checklist says the adjustment was implemented and verified through solution restore, solution build, focused package-guard tests, and client-generation tests. This reconciliation treats that as the source's reported status; it does not independently re-run those implementation checks.

The source explicitly concludes that product epic scope, PRD requirements, architecture boundaries, and UX behavior are unchanged. The affected delivery area is Story 1.2 root configuration/submodule policy.

## Product-level requirements and decisions relevant to the PRD

The proposal reinforces, but does not introduce, these product-level qualities:

- Build, dependency, package, and generated SDK artifacts have an identifiable source of truth.
- Package/dependency changes remain subject to automated verification and generated-artifact review.
- Dependency ownership drift should not undermine repeatable restore/build behavior.

The concrete decision to make Hexalith.Builds the package-version owner is a repository/toolchain decision, not a user-visible Folders capability, product behavior, scope boundary, safety invariant, or public contract. It therefore does not warrant a Functional Requirement.

## Implementation, architecture, and story detail that stays out of the PRD

The following material is useful implementation evidence but should remain in the sprint-change proposal, architecture/project context, Story 1.2, and tests rather than the PRD:

- The exact path `references/Hexalith.Builds/Props/Directory.Packages.props`.
- The import-only shape of the root `Directory.Packages.props`.
- The MSBuild property `CentralPackageTransitivePinningEnabled`.
- Removal of local `PackageVersion` rows and local overrides.
- Hexalith.Tenants as the repository-layout precedent.
- Avoidance of duplicate central-package-management entries from source-referenced submodules.
- The Octokit guard-test file path and `File.ReadAllText(...)` implementation.
- The exact restore/build commands and flags (`-m:1`, `-p:NuGetAudit=false`, `--no-restore`).
- The direct-adjustment route, Story 1.2 mapping, effort/risk estimate, and developer handoff.

These items describe how the repository satisfies existing quality expectations. They do not define what the Hexalith.Folders product must do for users or consumers.

## Already-covered PRD content

No numbered FR directly maps to this proposal, and none should: package-file ownership is not a product capability. The relevant existing coverage is:

1. **Project Classification — Project Context** (`prd.md`, `## Project Classification`): the PRD is a brownfield living product contract, current repository contracts and approved governance artifacts take precedence over historical delivery prose, and the PRD remains authoritative for product intent, release scope, actors, and user-visible outcomes. This places build-file mechanics outside the product contract while allowing the repository convention to evolve.
2. **Architecture Decisions Needed Next** (`prd.md`, `### Architecture Decisions Needed Next`): “The PRD defines product outcomes rather than implementation mechanisms.” Central package management and ownership are exactly such implementation mechanisms.
3. **NFR — Security and Tenant Isolation** (`prd.md`, `### Security and Tenant Isolation`, package/build artifact bullet): “Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.” A single Hexalith.Builds version source directly supports the provenance half of this requirement.
4. **NFR — Verification Expectations** (`prd.md`, `### Verification Expectations`): each NFR category requires an automated or documented validation path, and security verification includes dependency/package scanning and generated-artifact review. The proposal's restore, build, guard, and generation checks are delivery evidence under this existing expectation.
5. **PRD/Memlog authority boundary** (`.memlog.md`, decision “Contract authority is separated by concern…”): the PRD owns product intent and scope while technical contract and implementation artifacts own their respective mechanics. The proposal does not cross or alter that authority boundary.

## Genuine PRD gaps

**None.** The source explicitly reports no product or UX change, and comparison confirms that conclusion. The current PRD already states the relevant artifact-provenance and verification outcomes without freezing a particular MSBuild layout or shared-submodule path.

Adding a central-package-management requirement would make the PRD less durable by promoting a replaceable build mechanism into a product contract. No new success metric, NFR category, open release item, actor outcome, journey, capability, public schema, or compatibility promise is introduced.

## Conflicts with memlog or prior decisions

**None found.** The memlog contains no prior decision assigning NuGet package-version ownership to the Folders root or prohibiting Hexalith.Builds centralization. The change is consistent with:

- the living brownfield-contract classification;
- the established separation between product outcomes and implementation mechanisms; and
- the existing artifact provenance and package-verification expectations.

The source's statement that PRD scope is unaffected also avoids reopening any finalized product decision or OQ1–OQ10 release item.

## Recommended stable-ID edits or additions

- **FR edits:** None.
- **New FRs:** None.
- **NFR edits/additions:** None. Preserve the existing `Security and Tenant Isolation` artifact-provenance bullet and `Verification Expectations` package-scanning bullet as mechanism-neutral requirements.
- **Success metric, journey, or open-item edits:** None.
- **Renumbering:** None; all existing stable FR, UJ, SM, C, and OQ identifiers remain unchanged.

If administrative source tracking is updated during the parent PRD edit, this proposal may be recorded as a reconciled input without changing normative PRD content. That bookkeeping is not a stable-ID requirement change.

## Qualitative ideas the FR structure might otherwise drop

The source carries several maintainability ideas worth preserving in technical records even though they do not belong in an FR:

- **One source of truth:** default NuGet pins should have a single clear owner.
- **Ecosystem consistency:** Folders should follow the same shared-build convention as peer Hexalith repositories such as Tenants.
- **Guards follow ownership:** validation should inspect the actual authoritative file rather than a historical local location.
- **Drift prevention:** local overrides and duplicate CPM rows create silent divergence risk even when restore/build currently passes.
- **Confidence through focused evidence:** restore/build plus package guards and client-generation checks make the low-risk classification credible.

These are technical maintainability and governance qualities, not user-experience tone, workflow feel, or product differentiation. The sprint-change proposal already preserves them, so creating `addendum.md` solely for this input would duplicate the authoritative technical record.

## Concise disposition

**No-op for `prd.md`; no addendum and no open item.** Keep the source as the implementation/architecture handoff and verification record. Optionally mark it as a reconciled input in PRD metadata/history during the aggregate update, but do not change or add stable product requirements.
