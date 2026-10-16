---
title: 'Fix the Story 1.17 SignalR NuGet audit build failure'
type: 'bugfix'
created: '2026-10-15'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user requested the reported build fix">

## Intent

The required Folders Release/package build fails with three NU1903 errors because UI test dependencies resolve Microsoft.AspNetCore.SignalR.Common 10.0.12. Use Microsoft's patched 10.0.13 package and its minimum required dependency closure from the owning Hexalith.Builds central catalog. Preserve NuGet auditing, warnings as errors, existing work, all unrelated package versions, historical evidence, V1Only, unset T0 and the Story 1.17 tracker.

Verify the exact previously failing build without disabling audit, check the resolved packages, run relevant existing tests and catalog validators, and record the result in current readiness evidence. This is a local dependency and build fix; no commit, publication, deployment or migration window is requested.

</frozen-after-approval>

## Implementation Notes

- Microsoft advisory GHSA-334f-vvp4-7g6p identifies Common 10.0.13 as patched. Its net10.0 dependency requires Options 10.0.13, which requires DependencyInjection.Abstractions 10.0.13. The existing centralized lower pins must move with it to preserve transitive pinning rules.
- Add the Common security pin and update only those two supporting pins in Builds. Preserve the catalog's UTF-8 BOM and CRLF bytes. Existing catalog validation and the actual restored package graph verify the configuration; no new implementation-mirroring test is needed.
- The original Story 1.17 scope and its operational entry gates remain open. Keep earlier dated evidence intact and append a bounded correction after validation.
- Completed the three catalog selections, preserving all other entries and the original BOM/CRLF format. The exact required Folders Release/package build passes with zero warnings/errors and effective audit/all, transitive pinning and warnings-as-errors enabled.
- Verification: catalog validation/authority pass; UI 552/552, startup/authorization 29/29, routing/golden 114/114, Projects adapters 39/39, conformance 40/40, runbook 10/10 and governance 23/23 + 7/7 + 3/3 pass. The full Projects Release/package build also passes with its retained audit environment pin. All three affected graphs and runtime DLLs select patched Common 10.0.13.
- Updated current readiness/discovery/migration/runbook status, reproduced the 229-artifact candidate twice and refreshed only its current planning provenance binding. Historical records, frozen original scope and tracker remain intact. Detailed evidence: [SignalR audit fix](evidence/story-1-17-signalr-audit-fix-2026-10-15.json).
- No commit or push was requested; the catalog fix remains reviewable in its owning Builds working tree. Only this bounded prerequisite is complete; production migration and Story 1.17 closure remain open.

## Review Triage Log

- Low, patched: the catalog comment originally implied protection for all transitive consumers. Clarified that it requires CentralPackageTransitivePinningEnabled=true; effective Folders settings and the three actual restored/runtime graphs prove that requirement is met. Existing consumer/sample opt-in defaults are preserved. No findings are deferred.
