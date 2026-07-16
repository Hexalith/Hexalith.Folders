---
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-18'
workflow: 'bmad-correct-course'
status: 'implemented'
change_scope: 'minor'
---

# Sprint Change Proposal - Use Latest .NET

## 1. Issue Summary

The repository already targets `net10.0`, but `global.json` pinned the SDK to `10.0.103` while the current .NET 10 LTS servicing release metadata identifies SDK `10.0.302` and runtime `10.0.8` as the latest active .NET 10 release.

Local evidence:

- `Directory.Build.props` already defines `<TargetFramework>net10.0</TargetFramework>`.
- `global.json` previously pinned SDK `10.0.103`.
- `dotnet --info` shows SDK `10.0.302` and runtime `10.0.8` installed locally.
- `dotnet package list --project Hexalith.Folders.slnx --outdated` reported only `coverlet.collector` patch drift from `10.0.0` to `10.0.1`.

## 2. Impact Analysis

Epic impact: no epic or story restructuring is required. The existing PRD, epics, and architecture already establish .NET 10 as the intended platform baseline.

Artifact conflicts: no PRD, architecture, or UX conflict was found. The change aligns implementation configuration with the documented technology stack.

Technical impact: update the root SDK pin and the centrally managed test coverage collector patch version. No nested submodule initialization is required.

## 3. Recommended Approach

Use direct adjustment. This is a low-effort, low-risk correction that keeps the project on the active .NET 10 LTS servicing track without changing product scope.

Alternatives considered:

- Rollback: not applicable; no completed feature work needs to be reversed.
- MVP review: not needed; MVP scope remains unchanged.

## 4. Detailed Change Proposals

Root SDK selection:

OLD:

```json
"version": "10.0.103"
```

NEW:

```json
"version": "10.0.302"
```

Rationale: use the latest .NET 10 SDK installed locally and reported by Microsoft release metadata.

Central test package patch:

OLD:

```xml
<PackageVersion Include="coverlet.collector" Version="10.0.0" />
```

NEW:

```xml
<PackageVersion Include="coverlet.collector" Version="10.0.1" />
```

Rationale: close the only package patch drift reported by `dotnet package list --outdated`.

## 5. Implementation Handoff

Scope classification: minor.

Route to: Developer agent for direct implementation.

Success criteria:

- `dotnet --info` selects SDK `10.0.302` from the repository root.
- `dotnet restore Hexalith.Folders.slnx` succeeds.
- `dotnet build Hexalith.Folders.slnx` succeeds or reports only pre-existing source issues unrelated to SDK selection.
