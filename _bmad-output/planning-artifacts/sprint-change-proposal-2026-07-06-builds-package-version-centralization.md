# Sprint Change Proposal - Builds Package Version Centralization

**Date:** 2026-07-06
**Project:** Hexalith.Folders
**Prepared for:** Jerome
**Scope classification:** Minor
**Recommended route:** Developer direct implementation

## 1. Issue Summary

Hexalith.Folders carried NuGet package-version rows in its root `Directory.Packages.props` even though Hexalith repository guidance says package versions should come from `references/Hexalith.Builds/Props/Directory.Packages.props`, matching the `Hexalith.Tenants` pattern.

The trigger was the request to use the Hexalith.Builds package-version file like Tenants. The concrete issue was root-level version ownership drift: Folders imported the Builds package file but still maintained local `PackageVersion` entries and local overrides.

## 2. Impact Analysis

Epic impact: Story 1.2 root configuration/submodule policy is the affected implementation area. No product epic scope, PRD requirement, architecture boundary, or UX behavior changes are required.

Story impact: Existing scaffold/root-configuration validation remains valid, but package-version guards must read the shared Builds package file when checking a package whose version is now centralized there.

Artifact conflicts: PRD and UX are unaffected. Architecture already requires central package management and Hexalith.Builds reuse. The project context explicitly says `Directory.Packages.props` should centralize package versions with no inline package versions.

Technical impact: Restore/build must resolve all direct Folders package references from Hexalith.Builds. Source-referenced submodules must not produce duplicate CPM `PackageVersion` entries.

## 3. Recommended Approach

Use Direct Adjustment.

The appropriate fix is to keep Folders' root `Directory.Packages.props` as an import-only file, add `CentralPackageTransitivePinningEnabled`, and rely on `Hexalith.Builds` for NuGet package versions. Any tests that assert package pinning should inspect the shared Builds package file rather than expecting local Folders version rows.

Effort: Low.
Risk: Low, mitigated by solution restore/build and focused guard/generation tests.
Timeline impact: None.

## 4. Detailed Change Proposals

### Root package configuration

OLD:

```xml
<ItemGroup Label="CLI">
  <PackageVersion Include="System.CommandLine" Version="2.0.9" />
</ItemGroup>
```

NEW:

```xml
<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
```

The root file now imports Hexalith.Builds package versions and no longer owns local package-version rows.

### Shared Builds package versions

OLD:

```text
Some Folders package references had no shared Hexalith.Builds package-version row.
```

NEW:

```text
Hexalith.Builds owns the shared package-version rows needed by Folders restore/build.
```

This makes the Builds package-version file the single source for Folders' default NuGet package pins.

### Guard test

OLD:

```csharp
File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
```

NEW:

```csharp
File.ReadAllText(Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"));
```

The Octokit package guard now checks the actual source of truth.

## 5. Checklist Outcome

- [x] Trigger and context understood: root NuGet package-version ownership drift.
- [x] Epic/story impact assessed: Story 1.2 configuration policy only.
- [x] PRD/architecture/UX conflicts checked: no product or UX changes required.
- [x] Path forward selected: Direct Adjustment.
- [x] Implementation handoff defined: Developer direct implementation.
- [x] Verification performed: restore, build, and focused tests passed.

## 6. Implementation Handoff

Developer responsibilities:

- Keep root `Directory.Packages.props` aligned with the Tenants import-only pattern.
- Keep package-version ownership in `references/Hexalith.Builds/Props/Directory.Packages.props`.
- Keep package guard tests pointed at the actual package-version source of truth.

Success criteria:

- `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` passes.
- `dotnet build Hexalith.Folders.slnx --no-restore -m:1 -p:NuGetAudit=false` passes.
- Focused package guard and client generation tests pass.
