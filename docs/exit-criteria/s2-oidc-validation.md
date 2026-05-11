# S2 OIDC Validation

status: approved architecture parameter set; environment issuer and audience pins need human decision in deployment configuration
decision owner: Architect
approval authority: Architecture team + Security for environment pins
source inputs: Architecture Authentication and Security S-2, S-3 claim mapping, PRD Security and Tenant Isolation
last reviewed: 2026-05-11
open questions: Security must provide real issuer and audience values through deployment configuration outside this documentation-only story.

## Decision

Production authentication uses a pluggable OIDC provider through `Microsoft.AspNetCore.Authentication.JwtBearer` semantics. The validation behavior is frozen by architecture:

| Setting | Value | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| `ClockSkew` | `ClockSkew = TimeSpan.FromSeconds(30)` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| `RequireExpirationTime` | `RequireExpirationTime = true` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| `RequireSignedTokens` | `RequireSignedTokens = true` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| `ValidateIssuer` | `ValidateIssuer = true`; issuer pinned per environment by non-secret configuration | Architecture S-2 | approved for rule; needs human decision for environment value | Story 7.2 deployment configuration | 2026-05-11 |
| `ValidateAudience` | `ValidateAudience = true`; audience pinned per environment by non-secret configuration | Architecture S-2 | approved for rule; needs human decision for environment value | Story 7.2 deployment configuration | 2026-05-11 |
| `ValidateLifetime` | `ValidateLifetime = true` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| `ValidateIssuerSigningKey` | `ValidateIssuerSigningKey = true` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| JWKS automatic refresh | `AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| JWKS forced refresh minimum | `RefreshInterval = TimeSpan.FromMinutes(1)` after signature-validation failure | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |
| Token introspection | JWT-only; no introspection round trip | Architecture S-2 | approved | Story 7.2 production OIDC configuration | 2026-05-11 |

Syntactically valid non-production placeholders:

| Environment placeholder | Issuer placeholder | Audience placeholder | JWKS discovery placeholder | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|---|---|
| local-dev | `https://oidc.local.invalid/realms/hexalith-folders` | `api://hexalith-folders-local.invalid` | `https://oidc.local.invalid/realms/hexalith-folders/.well-known/openid-configuration` | Architecture S-2 and local Keycloak pattern | approved example only; needs human decision for real non-local values | Story 7.2 deployment configuration | 2026-05-11 |
| staging-template | `https://oidc.staging.invalid/realms/hexalith-folders` | `api://hexalith-folders-staging.invalid` | `https://oidc.staging.invalid/realms/hexalith-folders/.well-known/openid-configuration` | Architecture S-2 | needs human decision for real environment value | Story 7.2 deployment configuration | 2026-05-11 |
| production-template | `https://oidc.production.invalid/realms/hexalith-folders` | `api://hexalith-folders-production.invalid` | `https://oidc.production.invalid/realms/hexalith-folders/.well-known/openid-configuration` | Architecture S-2 | needs human decision for real environment value | Story 7.2 deployment configuration | 2026-05-11 |

Claim provenance is non-pluggable:

| Claim or source | Rule | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| `sub` | Principal identifier for authenticated actor | Architecture S-2 and S-3 | approved | Story 2.6 authorization enforcement and Story 7.2 OIDC configuration | 2026-05-11 |
| `eventstore:tenant` | Authoritative tenant after EventStore claim transformation | Architecture S-2 and S-3 | approved | Story 2.6 authorization enforcement | 2026-05-11 |
| `eventstore:permission` | Command/query access gate | Architecture S-2 and S-3 | approved | Story 2.6 authorization enforcement and Story 5 parity tests | 2026-05-11 |
| Payload tenant IDs | Inputs to validate, never authority | Architecture S-3 and project context tenant invariant | approved | Story 2.6 authorization enforcement and Contract Spine validation notes | 2026-05-11 |

## Rationale

The parameter set avoids vendor lock-in while removing ambiguity from JWT validation. Issuer and audience pinning prevents token-confusion attacks, and bounded JWKS refresh behavior reduces stale-key exposure after key rotation.

Examples use `.invalid` issuer hosts and placeholder audiences only. Real issuer, audience, client identifiers, signing keys, tokens, certificates, provider credentials, and tenant-specific values must enter through deployment configuration owned by later stories.

## Verification impact

Verification must reject production-looking issuer URLs, raw JWTs, private keys, certificate material, client credentials, tenant identifiers, and provider tokens. Later implementation tests must assert the frozen validation flags, JWKS refresh intervals, JWT-only behavior, and claim-provenance rules.

## Deferred implementation

This document does not add authentication middleware, package references, appsettings files, secret-store integration, runtime configuration, Keycloak setup, Entra ID setup, Auth0 setup, or deployment manifests.
