# Remaining catalog and current-source qualification

The bounded host migration is implemented. It does not establish a working command catalog, current integrated-source qualification, or a new real-provider acceptance pass.

## Command admission ownership

Production callbacks now install the real SDK receiver catalog filter through the public convention. The Folders host uses the new supported SDK `AddEventStoreDomainServiceSecurity(IServiceCollection, IConfiguration)` overload. The overload owns catalog/filter/guard registration without replacing bespoke processors or handlers. No internal SDK type is registered by Folders.

The existing Folders command adapters implement `IIdempotencyIntentAdapter`, with no `IDeclaredIdempotencyIntentAdapter` declarations. `RouteCatalogAdmissionObservation.CaptureAsync` and the catalog-enabled `IdempotencyIntentAdapterRegistry` explicitly refuse undeclared adapters. Existing Folders canonical descriptors contain nullable values and separately bind semantic options and task/credential scope; the current declared field vocabulary is required string/int64/bool/string-list. Inventing a non-null declaration or changing canonical intent encoding would misstate existing admission semantics. This session made no such change.

The unchanged `ProductionProcessWireEventTests.SignedWorkloadAndHumanReceiptAreAcceptedByProductionProcessRegistration` remains a failing positive test: its older isolated host installs the canonical convention but does not enroll catalog guard dependencies. Giving it a fake guard or converting it into an expected failure would hide the command-positive gap. A genuine command positive and the strict durability lane need owner-authored command/admission declarations, supported compatible descriptor representation, actual catalog authority/observations and generation propagation. This is distinct from the passing projection/query component catalog lifecycle and the existing signed workload/receipt implementation.

The new product declaration inventories only the actual operational named projection. The callback query declaration belongs to the test assembly and is observed only when that exact test handler is registered. No test declaration enrolls a product command. The component catalog uses an in-memory serialized CAS authority cell and explicit Development trust, with independent loaded-handler/endpoint observations. Its component/ACL JSON is an isolated model and supplies no live Dapr topology provenance.

## Concurrent source integration blocker

External `/pushall` activity advanced the workspace and several root-declared submodules while verification ran. The task performed no staging, commit, push, checkout, dependency update, publication or deployment. `repositories-current.json` and the final audit retain observed heads and working trees. Some task-authored changes were recorded by those external commits.

The current full Server, Integration and AppHost build commands fail CS0246 at `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs:15,23`: `RouteOperationContract` is absent from the selected EventStore source. Tenants enables this source-only API; no supported opt-out was found. The evaluated local EventStore catalog version remains `3.128.4`. No compile constant, dependency pin or checkout was changed to mask this incompatibility.

The explicit fallback builds final consumer code with project-reference dependency outputs already selected before that incompatible source advance, `BuildProjectReferences=false`, Debug, `Version=3.128.4`. Those are selected-dependency binary results, not qualification of the current complete source graph. Source/runtime inventories and separate failed current-build receipts preserve the distinction.

## Live and other gates

No new strict authenticated real-provider durability command was run. Its final AppHost graph cannot build. The initial exact-target Aspire baseline also failed before startup with the recorded mixed Commons CS1704; it is not a strict acceptance attempt. No task-owned Aspire/Dapr/provider/Redis topology was launched.

Transient ENOSPC corrupted some broad-run evidence; zero-byte files and incomplete attempts remain preserved. Later qualification stages use a recorded task-owned RAM TMPDIR and retain complete XML/receipts. SDK artifacts were temporarily preserved byte-for-byte under RAM with their original path accessible, without deleting unrelated files or historical evidence.

The full Contracts suite retains six pending PD10 candidate/inventory/generator failures and five CI conformance failures that still expect `@main` after an external immutable workflow pin. Frozen candidates/manifests and CI pins are unchanged. Current-source integration, command-positive/full live qualification, SDK broad regressions, Release/package/PD10, SM4 acknowledgement performance and independent whole-story review remain open. No new SM4 live measurements or provider-success claim is supplied.
