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
  `--token` / `-t` flag. See the [CLI reference](./cli-reference.md#credential-precedence-three-layers).
- **MCP** — the stdio server resolves its token through its own
  `Composition/BearerTokenHandler.cs`; supply credentials through the server's configured environment, never
  on the JSON-RPC channel.

In every adapter, the resolved token only sets the `Authorization` header; tenant and principal authority
still come exclusively from the validated claims described above.
