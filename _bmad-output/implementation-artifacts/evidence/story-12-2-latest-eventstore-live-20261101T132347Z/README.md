# Story 12.2 current-source live qualification

This packet follows the [latest EventStore intake](../story-12-2-latest-eventstore-20261101T122955Z/README.md).
The non-recursive fast-forward-only pull was already current at
`f704401ff9033919b1e9925564d814d7410bfe3d`; its sibling Tenants fallback is verified.
The previous missing-type and signed-command component blockers are superseded. Story 12.2 remains
in progress because strict live acceptance is still failed.

## Composition corrections

The first unchanged real-provider run failed during startup: the Folders EventStore gateway had not
enrolled `UseEventStoreHttpHostSecurity()`, and Tenants discovery reported `channel-unconfigured`.
The test exhausted its existing startup deadline: one failure, zero passes/errors/skips, 249.092 seconds.
Its source/runtime fingerprints, safe diagnostics, exact command and cleanup audit are retained.

`src/Hexalith.Folders.EventStore/Program.cs` now mirrors the owning SDK's supported HTTP host
composition: routing/security enrollment, human documentation/hub policies, framework channel
classification, actor callback metadata, projection-notification convention and channel-token health.
Folders' adapters and safe-denial handler remain in place. `src/Hexalith.Folders.AppHost/Program.cs`
uses the existing public SDK API to give Tenants and its own sidecar one generated per-run secret token.
No SDK source, admission semantics, provider behavior, live assertions, deadlines or retries changed.

The new gateway test exercises the real production entry point and keeps both actual SDK HTTP and
authorization startup validators. Both public endpoint inventories are empty; human docs/hub positive
and absent, channel, wrong-kind and mixed-credential denial controls execute. The AppHost model test
checks shared secret-parameter identity without resolving or retaining the generated value.

## Executed results and build limits

| Selection | Result |
| --- | --- |
| Conventional AppHost.Tests, Tenants host and Memories host Debug/source builds | Zero warnings/errors |
| Corrected conventional AppHost.Tests build | Zero warnings/errors |
| Focused gateway startup/security and safe-denial chain | 5 passed; zero failures/errors/skips; 0.963 seconds |
| AppHost HTTP/rebuild helpers and token model | 23 passed; zero failures/errors/skips; 1.073 seconds |
| Corrected strict real-provider acceptance | 0 passed, 1 failed; zero errors/skips; 35.590 seconds |

Builds use `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
-p:HexalithEventStoreVersion=3.130.2 -m:1 -nr:false` with conventional outputs required by the
unchanged runtime-binding fixture. They do not use `BuildProjectReferences=false`. Tenants/Memories
runtime hosts were built separately because their Aspire metadata suppresses automatic builds.

The full gateway test project fails compilation at `NativeTaskAdmissionTests.cs:49`, CS7036: its old
two-argument `IdempotencyIntentAdapterRegistry` construction no longer matches the public SDK API.
The current registry additionally requires a real catalog authority and declared admission adapters.
That fixture remains unchanged. `focused-gateway-project.xml` records the temporary narrow project
linking the actual host-composition tests/factory against production projects; its 5 passes do not
qualify the full gateway suite. Original build, model-fixture and focused-test failed attempts are retained
as separate controls. Exact expanded commands and XML-derived case outcomes accompany this packet.

## Current live blocker

The corrected strict run passes all seven sidecar readiness milestones. Authenticated
`BootstrapGlobalAdmin` then returns HTTP 503, public code `unavailable`, instead of required HTTP 202.
The exact SDK refusal is `RouteCatalogUnavailableException`:

> An independently bound local catalog host identity and profile are required.

The stack reaches `RouteCatalogAuthority.RequireLocalIdentity` through `AcquireCoreAsync`,
`AcquireAsync`, `RouteCatalogInvocationHandler.SendAsync` and
`DomainServiceWorkloadAssertionHandler.SendAsync`. `corrected-catalog-refusal-diagnostics.json`
retains the safe stack and readiness facts. This fails before post-bootstrap Folders commands;
no six-concern durability, retained task closure, restart/rebuild/provider pass or SM4 result is claimed.

Genuine SDK catalog identity/profile, activation/propagation and compatible command/admission
declarations remain an owning integration prerequisite, as distinguished in the
[earlier handoff](../story-12-2-security-enrollment-20261101/command-catalog-handoff.md).
No authority was fabricated, guard removed or declaration invented to obtain a passing run.
The intake packet's 11 PD10/CI conformance failures remain separate, alongside SDK broad regressions,
Release/package/PD10, SM4 acknowledgement performance and independent whole-story review.

## Binding, privacy and cleanup

The corrected run freezes 18,833 source/config inputs and 969 runtime files across eight separate
output groups. Its end audit reports zero source/runtime drift and unchanged heads for all 13
repositories. Thirty-five actual mapped-module records from the owning live processes are retained
separately. The focused gateway project's assets and final runtime fingerprints are recorded separately;
they are not a pre-run whole-input audit for that earlier focused execution. The first attempt's
18,832 source inputs and its runtime groups also had zero drift at
that attempt's end; those historical fingerprints are not a claim that subsequent source is unchanged.

Both attempts removed their exact disposable containers and temporary directories; `aspire ps`
returned `[]` afterward. Raw live stdout/XML remain only in ignored local artifacts, with their paths
and SHA-256 values recorded. This packet retains sanitized diagnostics and XML-derived summaries;
no credentials, dashboard login URLs, protected bodies or generated token values are included.
`final-audit.json` independently checks current inputs and four prior enrollment/intake packet seals. `SHA256SUMS` seals
every file in this packet except the manifest itself. No task commit, staging, push or publication occurred.
