---
title: 'Story 12.2 current SDK security enrollment'
status: 'approved-in-progress'
created: '2026-11-01'
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

# Owner decision

2026-11-01: The user replied **approve** to the bounded migration below. This authorizes
implementation and qualification of the listed Folders authentication, endpoint classification,
catalog integration and probe changes. The existing minimal SDK capability authorization remains
available if supported public composition needs a bounded compatibility repair. No deployment,
publication, package/gitlink update, commit, push, public v2 exposure or performance redesign is
authorized. The diagnostic packet remains unchanged as evidence of the pre-migration state.

# Decision requested

Approve a bounded Folders host migration to the existing EventStore HTTP security boundary,
including the explicit authentication and probe changes below, then resume current-source Story 12.2
qualification. This proposal records a concrete conflict between approved boundaries; it grants no
permission itself. Product/SDK source and the parent spec's frozen intent remain unchanged.

**Recommendation:** adopt the existing SDK boundary with explicit Folders endpoint classification.
Retain SDK workload, channel, catalog, tenant and replay guards. Preserve existing domain handlers,
authorization conjuncts, v1 operation/response contracts and disabled public v2. Amend only the
Folders route-behavior preservation requirement as needed for the listed authentication/probe changes.
No release, deployment, package/gitlink update, Git mutation, persistence-protocol change, provider
execution, admission redesign or performance redesign is proposed.

# Established failure and authority

The [new execution packet](evidence/story-12-2-selective-enrollment-20261101/README.md) reproduces
the production discovery failure: 0 passed, 1 failed, 0 skipped. `MapEventStoreDomainProjectionEndpoints`
unconditionally enters the shared HTTP boundary; the existing Folders host has not registered its
endpoint source. Registering the public HTTP service removes that exception, then the real public
inventory rejects 58 endpoint classifications across 55 paths. This is an executed diagnostic,
not an authenticated live pass or a complete inventory of every remaining migration issue.

The approved [original prerequisite](proposal-12-2-acceptance-prerequisites.md) already authorizes
qualifying/consuming a selective SDK projection/rebuild capability while preserving bespoke routes.
A compatible repair to that capability does not automatically need fresh scope approval. The new
decision is whether Folders may change its existing whole-host endpoint authentication and probe
behavior to satisfy the separately approved newer SDK boundary.

The decisive owner instruction is EventStore's
[`spec-5-14-authenticated-fallback-and-dapr-framework-route-protection.md`](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-5-14-authenticated-fallback-and-dapr-framework-route-protection.md),
approved 2026-10-29. Its frozen Always says: “Deny unclassified endpoints even with valid credentials.”
Its frozen new-endpoint matrix says: “Never silently classified.” It requires one credential kind per
operation and exact anonymous probes. The earlier
[`spec-5-5-internal-and-domain-service-trust-boundary.md`](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-5-5-internal-and-domain-service-trust-boundary.md)
also requires the same policy on pre-mapped `/project` and retains only `/health`, `/alive`, `/ready`
as anonymous probes. The current registration replaces default/fallback policies; HTTP and domain
middleware validate the complete effective endpoint set, including late replacements. Skipping that
validation, inventing a public exception, installing internal services or forging catalog metadata
would violate the owner boundary, not repair selective compatibility.

# Proposed bounded changes

The [per-path inventory](evidence/story-12-2-selective-enrollment-20261101/route-impact.json)
retains every observed finding. Counts below are endpoint findings, so GET/PUT or GET/POST paths
can appear more than once.

| Surface and observed findings | Owning files | Proposed classification and behavior |
| --- | --- | --- |
| Human v1 domain routes: 34; provider binding GET/PUT: 2; readiness: 2; console: 7; audit: 4 | `FoldersDomainServiceEndpoints.cs`, `ProviderReadinessEndpoints.cs`, `OpsConsoleDiagnosticsEndpoints.cs`, `AuditEndpoints.cs` under `src/Hexalith.Folders.Server/` | Require the existing named SDK human policy, with its explicit bearer scheme and credential-kind requirement. Retain all current folder/tenant/task/provider/audit/receipt checks and canonical v1 errors. Workload/channel credentials alone cannot reach these handlers. |
| Content disposition: 1; native evidence/task/file routes: 3 | `src/Hexalith.Folders.Server/Content/WorkspaceContentRetentionEndpoints.cs`, `WorkspaceNativeEndpoints.cs` | Apply the same named human classification; preserve the separately owned content/task semantics and effects. |
| Dapr discovery: 1 | `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs` | Use the SDK channel-only convention on `MapSubscribeHandler`; actual receiver token required in every environment. Inspect existing subscription callbacks for the same requirement. |
| Root description: 1 | `src/Hexalith.Folders.Server/FoldersServerModule.cs` | Protect `/` with the named human policy. Anonymous root reads change from their previous behavior. |
| Existing health paths: 3 | `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` | Keep `/health` explicitly anonymous. Add SDK-compatible anonymous `/alive` and `/ready` using the existing liveness/readiness checks. Preserve `/health/live` and `/health/ready` as authenticated aliases; they cannot remain anonymous under the SDK's exact-probe rule. |

The probe change requires aligning `deploy/observability/production/observability.yaml`,
`docs/operations/production-observability.md`, affected runbooks and health/routing conformance tests
to `/alive` and `/ready`. This changes tracked configuration only; deployment is excluded. Preserve
the existing readiness dependency checks, degradation semantics and bounded status-only bodies.
Inspect all tracked health-path consumers before changing any path. Shared ServiceDefaults also
serves Workers, so verify both hosts and retain their independently required subscription policy.

In `FoldersServerHostComposition.cs` and `FoldersServerServiceCollectionExtensions.cs`, enroll the
SDK-owned HTTP/domain security and catalog facilities through supported public registration. Keep
the current keyed bespoke processor and existing `/process`, `/query`, legacy `/project` and SDK
named/rebuild/discovery routes. Apply the public SDK canonical-route convention to custom POST
callbacks; a bare workload policy does not install the mandatory catalog filter. The source currently
registers that filter/guard only inside the canonical SDK builder's private core. Qualify whether its
public composition preserves the bespoke registrations; if a selective registration seam is needed,
hand that minimal capability to the EventStore owner under the already-approved prerequisite scope.
Do not duplicate its implementation in Folders or register its internal types. No SDK security guard
may be removed to make consumer tests pass.

These catalog/registration consequences are established by source inspection; the 58-finding public
probe does not execute canonical POST callbacks or prove that classification alone restores the host.

# Qualification after approval

1. Freeze final source/project graph and runtime identities; use isolated Debug outputs and all-Hexalith
   project references, with matching assembly identities as documented in the prior consumer packet.
2. Execute `FoldersServerHostCompositionTests.ProtectedProductionDiscoveryAdvertisesOnlyProcessorBackedNamedAndSharedRoutes`
   through the real production pipeline, and assert both public SDK inventories are empty. Prove
   unclassified/late endpoints still deny every credential before handler work and weak canonical
   overrides fail. Exercise the actual catalog guard without a production bypass.
3. Add focused production-pipeline controls for human/workload/channel/mixed credentials on each route
   group, actual sidecar-token discovery/subscription controls, exact anonymous probes and authenticated
   historical health aliases/root. Pin v1 safe error bodies and zero protected observation/effects on
   denial. Preserve existing `/project` body/no-operational-write and task-folder authority assertions.
4. Run affected Server, Workers, Integration and Contracts projects individually, including existing
   health/routing/observability, workload/receipt, provider-binding, named-rebuild and operational
   regressions. Run affected SDK registration/security cases in its owner repository if its seam changes.
5. Execute unchanged strict authenticated real-provider durability acceptance on the final audited
   runtime. Reuse prior expensive capacity coverage only when its exact source/runtime identities remain
   valid; otherwise requalify affected 10,001/32,769 production-handler histories. Retain failed attempts,
   unchanged assertions/deadlines/retries, owned-resource cleanup and SM4 measurements.

Until these checks pass, current-source integration remains incomplete. The earlier Task 36 check stays
bound to its selected historical candidate. SDK broad regression, Release/package/PD10, acknowledgement
performance and independent whole-story review retain their separate open dispositions.

# Alternative disposition

If the Folders authentication/probe changes are declined, retain the current-source blocker and hand
the precise conflict to the EventStore/Folders security owners for an explicitly approved compatibility
contract. The earlier selected-candidate packet remains historical evidence; selecting old binaries is
not current-source qualification. No SDK guard weakening or unapproved endpoint exemption is implied.


# Implementation disposition — 2026-11-01

The approved source migration is implemented and the proposal remains `approved-in-progress` for qualification. The [enrollment packet](evidence/story-12-2-security-enrollment-20261101/README.md) records the passing selected-dependency security/Worker/history/health controls, the one remaining signed-command positive failure, 11 broad Contracts failures, and failed current integrated-source builds after external dependency changes. Accurate command/admission declarations and the full real-provider lane remain open; no new approval is inferred or required for work already within this approved boundary. The [handoff](evidence/story-12-2-security-enrollment-20261101/command-catalog-handoff.md) separates the incompatible source graph and remaining command-catalog prerequisite from the completed source enrollment. No new live pass, deployment, publication, package/gitlink update, Git mutation, v2 exposure or performance redesign is claimed.
