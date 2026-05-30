# Production Identity And Secrets

This handoff defines the production contract for Folders API authentication and provider credential references.

Folders source and durable state store references only. Platform operators provision real values in the configured OIDC provider and Dapr secret store.

## OIDC Configuration

`Hexalith.Folders.Server` reads non-secret JWT bearer settings from `Folders:Authentication`:

| Key | Required outside Development/Test | Notes |
|---|---:|---|
| `Authority` | yes, unless `MetadataAddress` is set | HTTPS issuer authority for OIDC discovery. |
| `MetadataAddress` | yes, unless `Authority` is set | HTTPS discovery document address when it differs from authority. |
| `ValidIssuer` | yes | Environment-owned issuer pin. |
| `Audience` | yes | Environment-owned audience pin. |
| `RequireHttpsMetadata` | yes | Must be `true` outside Development/Test. |

JWT validation is frozen by `docs/exit-criteria/s2-oidc-validation.md`: 30 second clock skew, expiration required, signed tokens required, issuer/audience/lifetime/signing-key validation enabled, JWKS automatic refresh at 10 minutes, forced refresh minimum at 1 minute, and JWT-only validation with no token introspection.

Claim provenance is fixed:

| Claim | Production role |
|---|---|
| `sub` | Authenticated principal. |
| `eventstore:tenant` | Authoritative tenant after EventStore claim transformation. |
| `eventstore:permission` | Command/query access gate. |

Payload, header, route, and query tenant values are comparison inputs only.

## Dapr Secret Store

Production Dapr artifacts live under `deploy/dapr/production/`:

| Artifact | Purpose |
|---|---|
| `secretstore.yaml` | Sanitized Dapr secret store component named `folders-provider-credentials`. |
| `accesscontrol.yaml` | Includes deny-by-default secret scopes for `folders` and `folders-workers`. |
| `sidecar-config-bindings.yaml` | Binds the stable Dapr app IDs to their production configuration names. |

Synthetic allowed reference IDs are present only as conformance placeholders:

- `github-app-installation-ref-synthetic`
- `forgejo-user-delegated-ref-synthetic`

Operators replace or template those references during environment promotion. Real provider values, vault paths, client credentials, private keys, certificates, tenant IDs, and production URLs must remain outside this repository.

## Provider Credential Resolution

Provider adapters use the explicit `CredentialReferenceId` captured by organization provider binding state. They do not derive a secret name from `ProviderBindingRef`, `RepositoryBindingId`, tenant IDs, provider target metadata, or repository data.

The production resolver calls Dapr with:

- secret store: `Folders:ProviderCredentials:SecretStoreName` (default `folders-provider-credentials`)
- secret key: the explicit `CredentialReferenceId`
- expected returned value key: `Folders:ProviderCredentials:AccessTokenKey` (default `access_token`)

Unconfigured hosts fail closed. Production hosts opt in by registering Dapr-backed provider credential resolution.

## Failure Categories

| Condition | Category | Operator action |
|---|---|---|
| Missing reference or empty secret response | `ProviderConfigurationMissing` | Provision or correct the credential reference in the environment secret store. |
| Dapr secret-scope denial | `ProviderPermissionInsufficient` | Check Dapr secret scopes for the app ID and store. |
| Multi-value or malformed secret response | `ProviderAuthenticationRequired` | Replace the secret value with the expected single access-token entry. |
| Dapr sidecar or backing store unavailable | `ProviderUnavailable` | Check Dapr sidecar health and backing secret-store availability. |

Diagnostics may include category, reason code, app ID, synthetic reference examples, and correlation ID. They must not include resolved credential values.
