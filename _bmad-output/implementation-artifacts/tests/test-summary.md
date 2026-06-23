# Test Automation Summary

> Canonical latest-run summary for Story 8.3. Durable per-story copy: [`8-3-test-summary.md`](./8-3-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/8-3-wire-exercise-cross-surface-parity-and-close-claim.md`
**Feature under test:** the gateway-exception → REST problem mapping that surfaces canonical `folder_acl_denied` (403) for a propagated aggregate-gate ACL rejection (`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — `SafeGatewayReasonCode` + `ToArchiveGatewayProblem`).
**Framework:** xUnit v3 + Shouldly + `WebApplication`/loopback-Kestrel route tests — the project's existing API-conformance pattern. No new framework introduced.

## Gap discovered & auto-applied

Production `SafeGatewayReasonCode` maps **six** aliases to canonical `folder_acl_denied`, but the new
`ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` theory exercised only **four**.
The two ACL-evidence-family aliases `AclEvidenceForeignFolder` and `AclEvidenceUnsupportedAction` were untested
production branches.

**Fix (auto-applied):** added two `[InlineData]` rows to the existing theory in
`tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`. Alias coverage is now 6/6.

## Generated Tests

### API / route-level Tests
- [x] `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs` — `folder_acl_denied` gateway-rejection theory extended 4 → **6** variants.

## Coverage

- `SafeGatewayReasonCode` `folder_acl_denied` aliases: **6/6** (was 4/6).
- Story 8.3 ACs 1–4 wire/route-level covered; AC5 is docs-only.

## Validation

```
dotnet build tests/Hexalith.Folders.Server.Tests  → 0W / 0E
dotnet test  tests/Hexalith.Folders.Server.Tests  → Passed! 535/0 (+2 alias variants; theory 6/6 green)
```

See [`8-3-test-summary.md`](./8-3-test-summary.md) for the full per-AC breakdown.
