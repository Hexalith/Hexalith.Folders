# ADR 0005: Layered authorization and frozen OIDC validation

Date: 2026-05-31

Decision identifiers: `S-4`, `S-2`, `S-6`, `C9`. Implementing epics: Epic 2 (authorization) and Epic 7 (stories 7.1 and 7.2, production identity and secrets). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted. The layered authorization order and the frozen OIDC validation parameters have been in force since the Epic 2 authorization stories and were hardened for production in Epic 7.

## Context

Folder data is multi-tenant. The top safety invariant is zero cross-tenant leakage: no file, workspace, credential, repository, lock, commit, provider, audit, or cache resource may be touched before access is proven. Authorization must be ordered and fail-closed, and identity validation must not drift between environments.

Architecture decision `S-4` fixes the authorization layering. `S-2` freezes the production OIDC validation parameters. `S-6` (with `C9`) classifies sensitive metadata so paths, repository names, branch names, and commit messages stay tenant-sensitive and redacted in cross-tenant views.

## Decision

Authorization is a fixed, short-circuiting layer order, and OIDC validation parameters are frozen.

- `S-4`: the order is JWT validation, then Hexalith.EventStore claim transform, then the local tenant-access projection (fail-closed on stale freshness), then folder ACL, then EventStore validators, then production Dapr deny-by-default policy plus mTLS. Layers are never reordered or skipped for convenience, and no protected resource is touched before its layer passes.
- `S-2`: production OIDC uses `Microsoft.AspNetCore.Authentication.JwtBearer` with frozen parameters - `ClockSkew=30s`, `RequireExpirationTime=true`, `RequireSignedTokens=true`, `ValidateIssuer=true`, `ValidateAudience=true` - and JWKS refresh bounds. The OIDC client secret and provider credentials are referenced through the Dapr secret store, never inlined into config or code.
- `S-6` / `C9`: paths, repo names, branch names, and commit messages are classified `tenant-sensitive` by default; a per-tenant override may raise a class to `confidential`. Tenant authority comes only from authenticated context and claim-transform evidence; payload, query, and header values are comparison inputs.

The reserved `system` tenant is never used for managed tenant folder streams.

## Consequences

- Unauthorized and nonexistent resources are indistinguishable at the caller-visible boundary, so denial leaks no existence information.
- Identity validation behaves identically across environments because the validation parameters are frozen, not environment-tuned.
- The cost is rigidity: changing a layer order, an OIDC parameter, or a sensitivity class is a security-contract change requiring matched updates to validators, tests, and docs.

## Alternatives Considered

- Checking the folder ACL before tenant-access freshness was rejected because a stale or revoked tenant projection must fail closed before any folder-scoped read.
- Per-environment OIDC validation tuning was rejected by `S-2` because drift between environments is a security risk; the parameters are frozen instead.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The authorization order is enforced by the layered-authorization tests and the `dapr-policy-conformance` negative suite. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`.
