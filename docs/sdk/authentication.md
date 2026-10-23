# Hexalith.Folders Consumer Authentication

Status: Story 7.13 consumer reference.

This guide explains how consumers authenticate to the Folders surface and how the platform derives authority
from token claims. It **references** the existing bearer-token handlers and the frozen OIDC validation
parameters rather than restating their implementation; token-resolution logic lives in code, not in docs.

All examples are **metadata-only** and use `.invalid` placeholder issuers and audiences. Real issuer and
audience values are deployment configuration and never appear in documentation. Never place secrets, bearer
tokens, credential material, or local absolute paths here.

See also: [API & SDK reference](./api-reference.md) · [CLI reference](./cli-reference.md) ·
[MCP reference](./mcp-reference.md) · [auth/ACL decision flow](../diagrams/auth-acl-decision-flow.md).

## Legacy adapter safety and bearer transport

The proprietary CLI and MCP adapters are retained for bounded safety and continuity work.
`Hexalith.McpCli` remains the target Hexalith CLI/MCP owner. This local consolidation does not
authorize enrollment, deployment, adapter retirement, Story 11.6 closure, or removal of the Story 1.17
prerequisite and delivery hold.

Bearer destinations must be absolute HTTPS URLs, or HTTP URLs whose host is exactly `localhost`
(case-insensitive) or a literal loopback IP address (`127/8` or `::1`, as classified by
`IPAddress.IsLoopback`). DNS lookalikes, remote HTTP, other schemes, and relative or missing
addresses are rejected before transport credential acquisition or attachment. The guard checks each
actual request URI as well as startup/base-address configuration, so an unsafe absolute request cannot
override a safe base. Automatic HTTP redirects are disabled; redirect responses follow the existing
SDK response projection without forwarding the token.
HTTP loopback requests bypass configured proxies; HTTPS requests retain ordinary proxy behavior.

Rejection uses the constant diagnostic `Bearer transport requires HTTPS or HTTP loopback.` It never
includes the destination, query, credentials, or a local path. The CLI returns configuration exit code
64; MCP exits 1 for invalid startup configuration or returns per-call `usage_error`, retaining an
available correlation ID. Credential precedence and whitespace behavior remain surface-specific, and
lower-priority sources remain unread after a credential is found.

## Bearer-token handler pattern

Authentication is intentionally **outside** the SDK. `AddFoldersClient` registers the typed `IClient` as a
typed `HttpClient`; you attach a bearer-token `DelegatingHandler` that sets
`Authorization: Bearer <token>` on each outgoing request. The handler is the single place token acquisition
lives — do not reimplement token resolution in application code or in documentation examples.

The repository ships three reference handlers, one per adapter. Read them for the canonical pattern; do not
copy their logic into new code:

- SDK sample: `samples/Hexalith.Folders.Sample/BearerTokenHandler.cs`
- CLI: `src/Hexalith.Folders.Cli/Composition/BearerTokenHandler.cs`
- MCP: `src/Hexalith.Folders.Mcp/Composition/BearerTokenHandler.cs`

The [SDK quickstart](./quickstart.md#1-register-the-typed-client-di) shows where the handler attaches on the
`IHttpClientBuilder` returned by `AddFoldersClient`.

## OIDC token validation (frozen S-2 parameters)

The Folders server validates incoming JWTs against the frozen S-2 parameters. These are **cited** here from
the authoritative source, [`docs/exit-criteria/s2-oidc-validation.md`](../exit-criteria/s2-oidc-validation.md);
that document governs any change.

- `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true`,
  `ValidateIssuerSigningKey = true`.
- `RequireSignedTokens = true`, `RequireExpirationTime = true`.
- `ClockSkew = 30 seconds`.
- JWKS signing keys refresh automatically every **10 minutes**; forced refresh is floored at **1 minute** to
  bound key-rollover propagation without enabling refresh storms.
- **JWT-only**: validation is local signature/claim validation with no token-introspection round trip.

Issuer and audience are placeholders in all documentation, e.g. issuer
`https://oidc.production.invalid/realms/hexalith-folders` and audience
`api://hexalith-folders-production.invalid`. Real values are configured per deployment (see below).

## Claim-provenance contract

Authority is derived from authenticated claims after the EventStore claim transform — never from request
payloads, headers, or query values:

| Claim | Role |
|---|---|
| `sub` | Principal identifier of the authenticated actor. |
| `eventstore:tenant` | **Authoritative tenant**, established by the EventStore claim transform. |
| `eventstore:permission` | Command/query **access gate**. |
| payload / header / query tenant values | **Comparison inputs only** — validated against authority, never treated as authority. |

A request that carries a tenant in its body, a header, or a query string does not thereby gain authority over
that tenant; those values are compared against `eventstore:tenant` and rejected on mismatch. See the
[auth/ACL decision flow](../diagrams/auth-acl-decision-flow.md) for the full layered-authorization order.

## Production token acquisition

Production issuer/audience/authority configuration and secret handling are documented in
[`docs/operations/production-identity-and-secrets.md`](../operations/production-identity-and-secrets.md). The
`Folders:Authentication` configuration keys are:

- `Authority`
- `MetadataAddress`
- `ValidIssuer`
- `Audience`
- `RequireHttpsMetadata`

Acquire production tokens through your deployment's OIDC provider per that operations document; do not embed
tokens in configuration files, scripts, or source.

## CLI and MCP credential sourcing

The CLI and MCP adapters resolve the bearer token without you hand-coding a handler:

- **CLI** — three-layer precedence (highest first): `HEXALITH_TOKEN` environment variable →
  `~/.hexalith/credentials.json` per-tenant section (selected by `HEXALITH_TENANT`, else `default`) →
  `--token` / `-t` flag. Values are preserved without trimming. See the [CLI reference](./cli-reference.md#credential-precedence-three-layers).
- **MCP** — the stdio server resolves its token through its own
  `Composition/BearerTokenHandler.cs`: `HEXALITH_TOKEN` → inline `folders:auth:token` → token file
  (environment path `HEXALITH_FOLDERS_AUTH_TOKENFILE` takes precedence over `folders:auth:tokenFile`).
  Selected token values are trimmed. Supply credentials through server configuration, never on the JSON-RPC channel.

In every adapter, the resolved token only sets the `Authorization` header; tenant and principal authority
still come exclusively from the validated claims described above.
