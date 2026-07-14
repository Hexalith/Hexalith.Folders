# ADR 0007: Container deployment with Dapr sidecars and stable app IDs

Date: 2026-05-31

Decision identifiers: `I-2`, `I-3`, `I-4`, `I-1`. Implementing stories: Epic 7 stories 7.1 and 7.3 (container images, Dapr app IDs, and deployment topology). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted. The container-per-service model, the deny-by-default Dapr policy, and the stable app IDs were finalized for production in Epic 7 stories 7.1 and 7.3.

## Context

Hexalith.Folders runs as several cooperating services that communicate through Dapr. Deployment must be portable across environments, secure by default in production, and identical in service identity between local and production so access-control configuration is portable. The local Aspire topology must mirror the production wiring.

Architecture decisions `I-2`, `I-3`, and `I-4` pin the production hosting model, the Dapr access-control posture, and the stable service identities. Decision `I-1` pins the local Aspire orchestration that mirrors them.

## Decision

Each service ships as one container image with a Dapr sidecar, production Dapr is deny-by-default with mTLS, and service identities are stable across environments.

- `I-2`: one Docker image per service (`folders` server, `folders-workers`, `folders-ui`), Kubernetes-friendly but not Kubernetes-required, with a Dapr sidecar alongside each container.
- `I-3`: local Dapr uses a development access-control config; production uses deny-by-default plus mTLS, validated by the `dapr-policy-conformance` negative test suite.
- `I-4`: Dapr app IDs are stable across environments - `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui` - so access-control YAML is portable; the AppHost fails fast if the access-control configuration is missing.
- `I-1`: the local `.NET` Aspire topology in `Hexalith.Folders.Aspire` mirrors the production state-store and pub/sub wiring.

Staging and production manifests keep the same app IDs and production config names while selecting environment-owned image references.

## Consequences

- Access-control configuration is portable because app IDs do not change between environments.
- Production is secure by default: a missing or misconfigured policy fails the `dapr-policy-conformance` suite rather than silently allowing traffic.
- The cost is that app IDs and production config names are stable contracts; renaming a service is a coordinated change across images, Dapr config, and the access-control YAML.

## Alternatives Considered

- A single combined container for all services was rejected because it couples scaling and failure domains and breaks the per-service Dapr sidecar model in `I-2`.
- Environment-specific app IDs were rejected by `I-4` because they make the deny-by-default access-control YAML non-portable and error-prone across environments.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The deployment posture is enforced by `tests/tools/run-dapr-policy-conformance-gates.ps1` and the container-image gate. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
