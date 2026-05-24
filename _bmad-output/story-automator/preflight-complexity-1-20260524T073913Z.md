**Story Complexity Matrix**

| Story | Title | Score | Level | Reasons |
|-------|-------|------:|-------|---------|
| 1.1 | Establish a consumer-buildable module scaffold | 1 | Low | Configuration/feature flags |
| 1.2 | Establish root configuration and submodule policy | 0 | Low | - |
| 1.3 | Seed minimally valid normative fixtures | 5 | Medium | Authorization/permissions; Complex error handling; Integration testing required; Configuration/feature flags |
| 1.4 | Author Phase 0.5 Pre-Spine Workshop deliverables | 0 | Low | - |
| 1.5 | Finalize idempotency equivalence and adapter parity rules | 1 | Low | Complex error handling |
| 1.6 | Author Contract Spine foundation and shared extension vocabulary | 4 | Medium | Backend + Frontend combined; Complex error handling; Performance optimization |
| 1.7 | Author tenant, folder, provider, and repository-binding contract groups | 7 | Medium | Authorization/permissions; Backend + Frontend combined; Complex error handling; Performance optimization; Elevated AC count (7) |
| 1.8 | Author workspace and lock contract groups | 5 | Medium | Authorization/permissions; Backend + Frontend combined; Complex error handling |
| 1.9 | Author file mutation and context query contract groups | 4 | Medium | Authorization/permissions; Complex error handling; Performance optimization; Rate limiting/throttling; Pure refactor (no behavior change) |
| 1.10 | Author commit and workspace-status contract groups | 1 | Low | Complex error handling |
| 1.11 | Author audit and ops-console query contract groups | 3 | Low | Authorization/permissions; Performance optimization |
| 1.12 | Wire NSwag SDK generation with idempotency helpers | 2 | Low | Encryption/security; Complex error handling |
| 1.13 | Generate the C13 parity oracle | 3 | Low | Authorization/permissions; Integration testing required |
| 1.14 | Wire Contract Spine drift and generated-client CI gates | 4 | Medium | Authorization/permissions; Breaking/migration change |
| 1.15 | Wire safety invariant CI gates | 3 | Low | Authorization/permissions; Accessibility requirements |
| 1.16 | Wire exit-criteria and parity completeness gates | 2 | Low | Caching layer; Complex error handling |
| 2.1 | Stand up domain service host with Tenants integration | 0 | Low | - |
| 2.2 | Implement Organization aggregate ACL baseline | 6 | Medium | Real-time communication; Complex database operations; Authorization/permissions |
| 2.3 | Create folders within a tenant | 5 | Medium | Complex database operations; Authorization/permissions; Logging/monitoring/observability |
| 2.4 | Grant and revoke folder access | 2 | Low | Authorization/permissions |
| 2.5 | Inspect effective permissions | 2 | Low | Authorization/permissions |
| 2.6 | Enforce layered authorization with safe denials | 4 | Medium | Authentication system; Authorization/permissions |
| 2.7 | Inspect folder lifecycle and binding status | 2 | Low | Authorization/permissions |
| 2.8 | Archive folders with audit preservation | 0 | Low | - |
| 2.9 | React to Tenants events through Worker handlers | 5 | Medium | Real-time communication; Authorization/permissions; Complex error handling |
| 3.1 | Configure provider binding and credential reference | 2 | Low | Authorization/permissions |
| 3.2 | Define IGitProvider port and capability model | 1 | Low | Configuration/feature flags |
| 3.3 | Implement GitHub provider adapter | 0 | Low | - |
| 3.4 | Implement Forgejo provider adapter and drift detection | 6 | Medium | Backend + Frontend combined; Rate limiting/throttling; Integration testing required; Configuration/feature flags; Elevated AC count (9) |
| 3.5 | Validate provider readiness with safe diagnostics | 1 | Low | Complex forms |
| 3.6 | Create a new repository-backed folder | 5 | Medium | Real-time communication; Authorization/permissions; Complex error handling |
| 3.7 | Bind an existing repository to a folder | 2 | Low | Authorization/permissions |
| 3.8 | Define branch and ref policy | 2 | Low | Authorization/permissions |
| 3.9 | Inspect tenant and per-provider readiness evidence | 0 | Low | - |
| 4.1 | Implement Folder aggregate state machine with C6 transition matrix | 3 | Low | Complex database operations; Complex state management |
| 4.2 | Prepare workspace from a ready repository-backed folder | 2 | Low | Complex state management; Complex error handling |
| 4.3 | Acquire task-scoped workspace lock | 0 | Low | - |
| 4.4 | Inspect lock state and release the workspace lock | 2 | Low | Authorization/permissions |
| 4.5 | Enforce workspace path policy before file mutations | 0 | Low | - |
| 4.6 | Add and change files with inline and streamed content transport | 3 | Low | Real-time communication; Encryption/security |
| 4.7 | Remove files with metadata-only events and provider-safe ordering | 3 | Low | Infrastructure changes; Complex error handling; Accessibility requirements; Pure refactor (no behavior change) |
| 4.8 | Query file context with policy boundaries | 2 | Low | Authorization/permissions |
| 4.9 | Inspect workspace and projection currency | 2 | Low | Authorization/permissions |
| 4.10 | Surface workspace cleanup status without repair automation | 0 | Low | - |
| 4.11 | Propagate idempotency keys, correlation, and task IDs | 1 | Low | Complex error handling |
| 4.12 | Commit workspace changes with unknown-outcome reconciliation | 0 | Low | - |
| 4.13 | Surface canonical errors and operational evidence after failure | 3 | Low | Authorization/permissions; Encryption/security |
| 4.14 | Emit metadata-only audit and observability | 2 | Low | Encryption/security; Logging/monitoring/observability |
| 4.15 | Validate lifecycle replay and projection determinism | 5 | Medium | Real-time communication; Complex database operations; Configuration/feature flags |
| 4.16 | Validate lifecycle security boundaries | 4 | Medium | Authorization/permissions; Complex test setup; Configuration/feature flags |
| 4.17 | Seed lifecycle capacity test harness | 0 | Low | - |
| 5.1 | Ship SDK convenience helpers, samples, and quickstart | 1 | Low | Complex error handling |
| 5.2 | Implement CLI commands with behavioral-parity rules | 1 | Low | Complex error handling |
| 5.3 | Implement MCP tools, resources, and failure kinds | 0 | Low | - |
| 5.4 | Consume parity oracle in CLI and MCP tests | 5 | Medium | Real-time communication; Authorization/permissions; Integration testing required |
| 5.5 | Validate golden lifecycle parity across REST and SDK | 2 | Low | Authorization/permissions; Complex error handling; Pure refactor (no behavior change) |
| 5.6 | Validate behavioral parity across CLI and MCP | 1 | Low | Complex error handling |
| 5.7 | Validate mixed-surface handoff scenario | 1 | Low | Complex error handling |
| 6.1 | Audit and operation-timeline query endpoints | 0 | Low | - |
| 6.2 | Scaffold FrontComposer-hosted read-only operations console | 4 | Medium | Complex database operations; Authentication system |
| 6.3 | Render operator-disposition labels as primary visual | 1 | Low | Configuration/feature flags |
| 6.4 | Implement sensitive-metadata redaction affordance | 1 | Low | Configuration/feature flags |
| 6.5 | Author console diagnostic wireflow notes | 4 | Medium | Complex forms; Accessibility requirements; Uncertain/research-heavy scope; Elevated AC count (9) |
| 6.6 | Build folder and workspace diagnostic pages | 4 | Medium | Authorization/permissions; Backend + Frontend combined |
| 6.7 | Build provider readiness and support diagnostic pages | 4 | Medium | Authorization/permissions; Backend + Frontend combined |
| 6.8 | Build audit and operation-timeline diagnostic pages | 5 | Medium | Authorization/permissions; Encryption/security; Backend + Frontend combined |
| 6.9 | Implement incident-mode last-resort read path | 2 | Low | Authorization/permissions |
| 6.10 | Enforce console performance and perceived-wait UX | 2 | Low | Performance optimization; Complex forms |
| 6.11 | Verify no-mutation enforcement and accessibility | 5 | Medium | Authorization/permissions; Accessibility requirements; Logging/monitoring/observability; Elevated AC count (9) |
| 7.1 | Deploy production Dapr deny-by-default access control | 7 | Medium | Webhook/async processing; Authorization/permissions; Encryption/security; Infrastructure changes |
| 7.2 | Configure production OIDC and secret store integration | 3 | Low | Authentication system; Complex forms |
| 7.3 | Build container images with stable Dapr app IDs | 2 | Low | Infrastructure changes |
| 7.4 | Consolidate baseline build and unit CI gates | 0 | Low | - |
| 7.5 | Consolidate contract and parity CI gates | 3 | Low | Authorization/permissions; Integration testing required |
| 7.6 | Consolidate security and redaction CI gates | 3 | Low | Caching layer; Infrastructure changes |
| 7.7 | Add capacity-smoke CI gate | 1 | Low | Performance optimization |
| 7.8 | Wire scheduled drift and policy-conformance workflows | 5 | Medium | Authorization/permissions; Integration testing required; Breaking/migration change |
| 7.9 | Publish traceable NuGet release packages | 2 | Low | Integration testing required; Configuration/feature flags |
| 7.10 | Calibrate capacity tests and pin C1/C2/C5 targets | 1 | Low | Performance optimization |
| 7.11 | Enforce C3 retention and tenant-deletion behavior | 0 | Low | Logging/monitoring/observability; Pure refactor (no behavior change) |
| 7.12 | Wire production observability and alerts | 2 | Low | Complex error handling; Logging/monitoring/observability |
| 7.13 | Publish API, SDK, CLI, and MCP consumer references | 2 | Low | Backend + Frontend combined |
| 7.14 | Publish operations and audit documentation | 3 | Low | Authorization/permissions; Logging/monitoring/observability |
| 7.15 | Publish provider and error documentation | 2 | Low | Integration testing required; Configuration/feature flags |
| 7.16 | Publish NFR traceability bridge | 6 | Medium | Authorization/permissions; Performance optimization; Accessibility requirements; Integration testing required; Configuration/feature flags |
| 7.17 | Publish ADR set and maintenance runbooks | 4 | Medium | Infrastructure changes; Complex error handling; Logging/monitoring/observability |

**Summary:**
- Low: 61 stories
- Medium: 25 stories
- High: 0 stories
