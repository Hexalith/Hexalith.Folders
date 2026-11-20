# Sprint change proposal for the Folders repository audit

**20 November 2026 · Applied to planning · Moderate scope**

Jerome requested “apply recommendations” after reviewing the audit of `5f39a1b8ae5f52919ba91d5a955fb8fd39077550`. The accepted change covers sprint planning and acceptance criteria. Release, v2 exposure and provider qualification each require their existing evidence. The first provider to exercise in a disposable repository is an open decision.

## 1. Issue summary

The deployed Server registers an executor that reports provider commits as unsupported. The golden lifecycle test substitutes a successful executor. A caller can complete that test without a commit ever appearing on a remote ref. Separately, Workers can acknowledge an event after a retryable Memories publish failure. These are delivery gaps in Stories 12.4 and 12.5. [Server registration](../../src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs) · [Golden lifecycle fixture](../../tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs) · [Workers subscription](../../src/Hexalith.Folders.Workers/FoldersWorkersModule.cs)

The audit's Debug build passed. Four Contracts tests failed both locally and in hosted CI. The packaged Release AppHost also failed to read its compiled route inventory, while the same case passed under Debug/source references. Three failing Contracts cases share a stale pending conformance inventory. The fourth found the shared Dapr integration default at `17.0.0-beta.1052` and the Folders override at `17.0.0-beta.1078`. The exact-source publication prerequisite therefore fails on this revision. [Hosted run 44606945137](https://github.com/Hexalith/Hexalith.Folders/actions/runs/44606945137)

## 2. Impact and approach

**Direct adjustment to existing work.** The PRD already requires a durable commit, authorized search and an auditable result. The architecture assigns persistence to EventStore and provider transport to the adapters. The UX specification already requires read-only diagnostics with visible freshness. The work changes how maintainers implement and prove those outcomes. A rollback would discard working content and authorization behavior without supplying the missing executor or retry. Narrowing the MVP to the passing synthetic lifecycle would drop the remote outcome the PRD requires. [PRD](prd.md) · [Architecture](architecture.md) · [UX specification](ux-design-specification.md)

**Dependencies.** Stories 12.4 and 12.5 share durable state, content and confidential-value prerequisites in 12.1–12.3 and 12.7. Delivering the executor precedes the full 3.14 provisioning journey and the 4.21 real-commit proof. The 10.8 search journey consumes 12.5. Story 13.4 depends on 12.1, 12.2 and 13.2. The live-write rollout uses 13.1 transport evidence. The production drill depends on the external EventStore recovery capability. Use the manifest for execution order and eligibility. A story's audit priority measures impact, not permission to bypass a prerequisite. [Execution manifest](planning-story-manifest.yaml)

## 3. Applied edits and acceptance checks

Finding numbers follow the audit. The first three entries are in recommended delivery order. Seven story criteria are in the canonical epic file. The release, generator-isolation, onboarding and shared-workflow maintenance actions are in the sprint tracker. [Canonical stories](epics.md) · [Sprint tracker](../implementation-artifacts/sprint-status.yaml)

| Finding and owner | Previous planning or observed behavior | Applied change and evidence required for closure |
| --- | --- | --- |
| 2. Release baseline · Release, Builds and AppHost maintainers | Earlier release work was recorded against other revisions. Current Release CI fails while Debug AppHost passes. | Open an exact-source release repair. Resolve the catalog/override, identify which packaged owner assembly or dependency fails, and record a bounded diagnostic. After the last bound source change, regenerate the **pending** inventory. Require the four Contracts regressions, packaged AppHost case and push CI on the repaired SHA to pass. |
| 1. Git lifecycle · Folders and provider maintainer, 12.4 → 3.14 and 4.21 | 12.4 already requires real commits and unknown-outcome recovery. The default composition supplies an unavailable executor. Provisioning events lack a dispatch to the existing manager. | Record recoverable intent before the remote write. Use normal production registration, an authorized lock, real staged bytes and a disposable remote. Interrupt after provider acceptance, replay and compare the ref, bytes, commit count and terminal task state. In 3.14 prove the Worker consumes a create/bind request and reaches one terminal result. If GitHub is selected, complete its live readiness probe. Record coverage separately for each provider. |
| 3. Indexing and removal · Workers, 12.5 → 10.8 | The existing story calls for durable egress. A retry result can still become HTTP 200 with no active recovery consumer in the shipped Workers host. | Require durable retry ownership before acknowledgment or signal retry to the broker. Exercise change and removal across publish refusal and restart. Observe one logical result per event, removal of the stale item and a draining backlog. |
| 4. Provider egress · Provider maintainer, 13.1 | S-9 already requires direct, policy-checked connections. Registered handlers inherit differing proxy or redirect settings. | Exercise both actual handler factories with controlled proxy and redirect destinations. Require the allowed direct connection and verify credential-bearing traffic stays with its approved authority. Qualify transport before enabling each provider's live writes. |
| 5. Audit reads · Server and console maintainers, 6.14 | 6.1 supplied the endpoints. Its tests seed process-local dictionaries. 6.14 required a populated journey without specifying how to prove the backing readers. | Register and populate durable metadata-only readers behind the existing audit and timeline ports. Read one accepted operation on a second replica and after an empty-checkpoint rebuild. Verify ordering, freshness, tenant denial and redaction. Preserve the console's existing access and display rules. |
| 6. Readiness · Service owners, 13.4 | Readiness evaluates a snapshot, but the normal Server and Workers supply fixed healthy inputs. | Exercise both hosts with real observations. Independently degrade Tenants, stall a projection past its lag threshold and restore each. Check missing/stale evidence, recovery on `/ready` and independent liveness. |
| 7. Generated SDK · SDK maintainer, immediate repair and 11.12 | The test redirects some outputs to scratch. NSwag can still rewrite the tracked client before the postprocessor restores it. | Isolate both output stages and verify byte parity. Observe the tracked hash during normal generation, a concurrent read and interruption. Apply the same condition to later generator modernization. Sequence the maintenance fix before the final pending inventory regeneration. |
| 8. Onboarding · SDK and documentation maintainers, 5.1 follow-up | The quickstart targets a default v1 host with a v2 client. The sample uses a literal fence after requesting the lock. | Document a runnable supported v1 path and the explicit identity and routing assumptions of the v2 fixture. Wait for lock application and use the observed fence after release/reacquisition. Verify the example on its stated host. Include .NET, PowerShell, source-mode flags and direct submodule setup. |

**Related work.** The release owner routes the mutable CodeQL and Dependency Review workflow references to CiCd for reviewed commit pinning. The documentation owner cross-links ADR 0001 to the implemented actor-backed path. Split large endpoint or processor files by the operation already under change, keeping shared policy and error mapping centralized. Justify each split by a concrete change or test. The transport and recovery work can reuse existing ports, projections and the indexing bridge.

## 4. Delivery order, effort and open decisions

| When | Work and estimated engineering effort | Dependency or trade-off |
| --- | --- | --- |
| First | Release diagnosis and SDK isolation, 1–3 days for initial repairs. Onboarding, ½–1 day. Shared workflow pin review, ½–1 day. | Compare package-mode Release with passing Debug first. Estimate the remaining AppHost fix once the failing dependency is identified. CiCd owns workflow changes. |
| Next eligible delivery | Transport qualification, 2–4 days. First provider lifecycle, 1–3 weeks. Index recovery, 3–7 days. Readiness, 2–4 days. | Jerome and the provider maintainer select the disposable provider fixture. One successful provider can establish a vertical slice. Both provider contract suites still apply to readiness claims. Workers can proceed alongside the executor when their shared prerequisites are met. |
| Operational qualification | First durable audit slice, 1–2 weeks. Preproduction and recovery evidence, 1–3 weeks after dependencies arrive. | Confirm the target cluster, rendered overlays, two replicas and a durable broker. Exercise a real provider, measure throughput and lag, and time the regional-loss drill against the five-minute RPO and four-hour RTO. External availability affects calendar duration. |

The EventStore callback-token omission is a **suspected deployment gap**. The checked-in file contains patch fragments. Story 13.7 now checks the rendered sidecar and application references together and exercises the callback. A platform overlay may already provide the pair. If a deployed service already recovers indexing failures or supplies the audit projection, its owner can satisfy the relevant story by demonstrating the same end-to-end recovery and tenant checks. [Production profile](../../docs/deployment/supported-mvp-profile.md) · [Patch fragments](../../deploy/dapr/production/sidecar-config-bindings.yaml)

## 5. Handoff and workflow record

The release maintainer owns the first repair. Folders, Workers and SDK maintainers own the story criteria and maintenance actions above. The platform operator supplies the rendered workload and recovery evidence. Product owns provider selection and any later v2 or release decision. The project owner can review all four changed artifacts in the working tree.

| Correct Course checklist | Disposition |
| --- | --- |
| 1.1–1.3 · Trigger, problem, evidence | Complete. The exact-SHA audit and hosted run identify the affected workflows. |
| 2.1–2.5 · Epic scope and sequencing | Complete. Existing story identities cover the runtime work. The maintenance items carry the immediate repairs. |
| 3.1–3.4 · Product, architecture, UX and other artifacts | Complete. Story criteria, manifest amendment and tracker updated. PRD scope, architecture boundaries and console interaction design continue to apply. |
| 4.1–4.4 · Options and decision | Direct adjustment selected. Rollback and scope reduction do not resolve the observed failures. |
| 5.1–5.5 · Proposal and handoff | Complete. Owners, effort, dependencies and evidence are recorded above. |
| 6.1–6.5 · Review, authorization and tracking | Applied in batch under Jerome's 20 November instruction. Existing story lifecycle states and execution ranks are preserved. Provider choice, runtime results and deployment evidence remain open. |

### Verification of the planning changes

- Parsed both YAML files and compared them with `HEAD`. All 163 manifest rows, the execution graph, historical amendments, lifecycle statuses and prior tracker actions matched. Verified both epic-file digests in the new amendment and all relative links in this proposal.
- Regenerated the pending PD10 inventory to scratch before and after the edits. The two generated files were byte-identical. The existing conformance drift remains with the release-repair owner.
- Ran the existing Debug test assemblies using `-class '*.NfrTraceabilityConformanceTests'` and `-class '*.OpsConsoleWireflowNotesTests'`. Both passed 17 of 17. The first NFR run saw a PowerShell child exit 134. It passed with writable XDG paths for the child process.
