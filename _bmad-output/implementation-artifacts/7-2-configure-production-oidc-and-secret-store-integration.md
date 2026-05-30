---
baseline_commit: 0355fd1b121ae159c5e002ad1c6b0fed3af02af2
---

# Story 7.2: Configure production OIDC and secret store integration

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform operator,
I want pluggable production OIDC and Dapr secret-store integration configured,
so that authentication and credential references work without storing secret material in Folders.

## Acceptance Criteria

> Epic 7.2 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given production identity and secret-store settings exist
> When services start
> Then JWT validation uses frozen S-2 parameters and secret access uses references only
> And no provider token or credential value is stored in Folders state.

Decomposed acceptance criteria:

1. `Hexalith.Folders.Server` registers real JWT bearer authentication outside Development using `Microsoft.AspNetCore.Authentication.JwtBearer`; `Program.cs` calls `UseAuthentication()` before `UseAuthorization()` and before mapped endpoints.
2. JWT validation implements the frozen S-2 parameter set: `ClockSkew = 30s`, expiration required, signed tokens required, issuer/audience/lifetime/signing-key validation enabled, issuer and audience pinned by environment configuration, JWKS `AutomaticRefreshInterval = 10m`, JWKS `RefreshInterval = 1m`, and JWT-only behavior with no token introspection.
3. Claim provenance is exact and non-pluggable: `sub` is the principal, `eventstore:tenant` is the authoritative tenant after EventStore claim transformation, and `eventstore:permission` gates command/query access. Payload, header, or query tenant values remain comparison inputs only.
4. Startup validation fails closed outside Development when the server lacks a JWT bearer scheme, issuer, audience, metadata address/authority, or production-safe HTTPS metadata settings. The current generic `FoldersAuthSchemeValidator` must become specific enough that any arbitrary auth scheme cannot satisfy production readiness.
5. Dapr secret-store production configuration exists as sanitized, repository-local conformance artifacts. It defines the secret store name, allowed secret references, and Dapr secret scopes without including real provider tokens, client secrets, private keys, certificates, tenant IDs, production URLs, or credential values.
6. Provider credential resolution uses explicit credential reference IDs from organization provider binding state and Dapr secret-store lookups only. Do not derive a secret name from `ProviderBindingRef`, `RepositoryBindingId`, tenant IDs, or provider target metadata.
7. GitHub and Forgejo provider adapters can resolve short-lived credential leases from Dapr secret-store references for readiness, repository creation, and repository binding, then clear lease values on disposal. Existing unconfigured resolvers remain fail-closed for hosts that do not opt into production credential resolution.
8. Tests and gate/docs evidence prove frozen OIDC parameters, fail-closed production startup, Dapr secret-scope shape, reference-only provider credential flow, metadata-only diagnostics, and absence of raw credential values in events, projections, logs, test fixtures, and deployment artifacts.

## Tasks / Subtasks

- [ ] Wire production JWT bearer authentication in the server host (AC: 1, 2, 3, 4)
  - [ ] Add server OIDC options, suggested `src/Hexalith.Folders.Server/Authentication/FoldersOidcOptions.cs`, bound from a non-secret section such as `Folders:Authentication`.
  - [ ] Add a service registration extension, suggested `AddFoldersProductionAuthentication(...)`, that calls `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`.
  - [ ] Set `MapInboundClaims = false` or otherwise prove `sub`, `eventstore:tenant`, and `eventstore:permission` are consumed without framework claim-type remapping drift.
  - [ ] Configure `TokenValidationParameters` with the S-2 values from `docs/exit-criteria/s2-oidc-validation.md`.
  - [ ] Set `JwtBearerOptions.AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` and `JwtBearerOptions.RefreshInterval = TimeSpan.FromMinutes(1)`.
  - [ ] Add `app.UseAuthentication(); app.UseAuthorization();` in `src/Hexalith.Folders.Server/Program.cs` before `MapFoldersServerEndpoints()`.
  - [ ] Update `TenantContextOptions` defaults or configuration so production tenant access uses `eventstore:tenant`, while Development/Test hermetic paths remain explicit and visibly non-production.
  - [ ] Strengthen `FoldersAuthSchemeValidator` or add a new hosted validator so non-Development startup requires the JWT bearer scheme plus issuer/audience pins, not merely "some authentication scheme".

- [ ] Preserve UI OIDC behavior while aligning production safety (AC: 2, 3, 4)
  - [ ] Review `src/Hexalith.Folders.UI/CompositionRoot.cs` and `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs`.
  - [ ] Keep the `hermetic-test` branch restricted to Development/Test.
  - [ ] Ensure UI OIDC does not save or forward raw tokens into logs, app state, console payloads, or Folders projections. If `SaveTokens = true` remains necessary for backend calls, document why and cover it with metadata-only tests.
  - [ ] Keep UI claim usage compatible with the server's authoritative `eventstore:tenant`/`sub` claim contract or explicitly map only in the UI circuit boundary.

- [ ] Add Dapr secret-store production artifacts (AC: 5, 8)
  - [ ] Add sanitized production fixture(s), suggested `deploy/dapr/production/secretstore.yaml` and/or a secret-scope section in `deploy/dapr/production/daprsystem.yaml`.
  - [ ] Define the secret store component name used by code, but keep the backing provider pluggable: Kubernetes secret store is acceptable as a production baseline, with Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, or another Dapr-supported store allowed by configuration.
  - [ ] Add secret scopes with `defaultAccess: deny` and explicit allowed synthetic secret reference IDs for `folders` and `folders-workers`; include the default Kubernetes store in deny scopes when limiting secret access.
  - [ ] Bind sidecars to the relevant Dapr configuration through `deploy/dapr/production/sidecar-config-bindings.yaml` without changing Story 7.1's stable app IDs or Dapr access-control assumptions.
  - [ ] Do not add local JSON secret files, real Kubernetes Secret manifests, real vault paths, provider tokens, OAuth client secrets, private keys, certificates, or production endpoints to source.

- [ ] Implement reference-only provider credential resolution (AC: 5, 6, 7)
  - [ ] Identify how `CredentialReferenceId` from `ConfigureProviderBinding` / `ProviderBindingConfigured` reaches provider runtime paths. Current provider creation/binding requests carry `ProviderBindingRef` but not `CredentialReferenceId`; bridge this intentionally through the organization provider binding read model or request contracts before resolving secrets.
  - [ ] Add a provider-agnostic credential reference abstraction if needed, e.g. `IProviderCredentialReferenceResolver`, so GitHub and Forgejo do not duplicate Dapr secret-store policy logic.
  - [ ] Implement GitHub and Forgejo Dapr-backed resolvers that use `DaprClient.GetSecretAsync(secretStoreName, credentialReferenceId, metadata, cancellationToken)` and return existing `GitHubCredentialLease` / `ForgejoCredentialLease` types.
  - [ ] Map missing, denied, malformed, or unavailable secret-store responses to existing provider failure categories such as `ProviderConfigurationMissing`, `ProviderAuthenticationRequired`, `ProviderPermissionInsufficient`, `ProviderUnavailable`, or `ReconciliationRequired` without exposing the secret reference value to unauthorized callers.
  - [ ] Keep unconfigured resolvers as the default fail-closed behavior unless production registration opts in.
  - [ ] Ensure credential leases are disposed in every provider path and token strings are cleared, as current providers already attempt in `finally` blocks.

- [ ] Add tests and conformance fixtures (AC: all)
  - [ ] Add server auth tests under `tests/Hexalith.Folders.Server.Tests/Authentication/` for S-2 `JwtBearerOptions`, missing issuer/audience startup failure, no introspection, claim mapping, and middleware order where practical.
  - [ ] Add provider credential tests under `tests/Hexalith.Folders.Tests/Providers/{GitHub,Forgejo}/` using a fake Dapr secret accessor. Cover success, missing secret, denied secret, malformed multi-value secret, unavailable secret store, and cancellation propagation.
  - [ ] Add static YAML tests, suggested `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ProductionSecretStoreConformanceTests.cs`, that validate production secret-store artifacts are sanitized, deny by default, include expected app IDs, and contain no credential-shaped values.
  - [ ] Extend sentinel/security tests to prove `ProviderBindingConfigured`, organization provider binding replay state, provider readiness diagnostics, and repository provisioning outcomes store only credential reference IDs or redacted metadata, never resolved secret values.
  - [ ] Add or update a focused gate script only if the existing safety/governance gates cannot cover the new conformance tests. Keep scripts hermetic: no production endpoints, no provider calls, no secret store network calls, no artifact upload, no recursive submodule setup.

- [ ] Document operations handoff (AC: 2, 5, 8)
  - [ ] Update `docs/exit-criteria/s2-oidc-validation.md` or add `docs/operations/production-identity-and-secrets.md` with configuration keys, required environment-owned issuer/audience values, allowed placeholder examples, and secret-store promotion steps.
  - [ ] Document that Folders source stores credential references only; platform operators provision actual secret values in the configured Dapr secret store.
  - [ ] Document failure categories and operator actions for missing/denied/unavailable credential references without including secret names beyond sanitized examples.

- [ ] Verification (AC: all)
  - [ ] Run `dotnet restore Hexalith.Folders.slnx`.
  - [ ] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [ ] Run the focused server auth, provider credential resolver, and secret-store conformance tests.
  - [ ] Run relevant safety/governance gates that scan for secret leakage.
  - [ ] Run `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src` and confirm no recursive submodule setup guidance was introduced.
  - [ ] Run a secret-shaped scan across story-touched artifacts, at minimum for `ghp_`, `github_pat_`, `client_secret`, `private_key`, `BEGIN .*PRIVATE KEY`, `password=`, `token=`, and production-looking URLs.
  - [ ] Record exact commands, pass/fail counts, and environment limitations in the Dev Agent Record.

## Dev Notes

### Critical Scope Boundaries

- This is production readiness/security configuration work, not a new product surface. Do not add REST/SDK/CLI/MCP operations, UI mutation paths, new provider capabilities, or new folder lifecycle semantics.
- Do not store or generate real OIDC client secrets, JWTs, refresh tokens, provider tokens, private keys, certificates, tenant data, provider payloads, repository names, or production URLs.
- Do not replace provider authorization with secret-store access. Secret retrieval happens only after existing layered authorization and provider binding/readiness checks have selected an authorized credential reference.
- Do not derive secret names from tenant IDs, folder IDs, repository IDs, provider target URLs, or `ProviderBindingRef`. Use the explicit `CredentialReferenceId` captured by organization provider binding state.
- Do not initialize nested submodules or add recursive submodule commands to CI, docs, or scripts.
- Current package pins are authoritative: .NET SDK `10.0.300`, `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.5`, Dapr packages `1.17.9`, Aspire Hosting `13.3.5`, xUnit v3 `3.2.2`, and YamlDotNet `18.0.0`.

### Current State To Preserve

- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` currently registers only authentication core services with `AddAuthentication()` and says concrete JWT/OIDC schemes are Story 7.2 work. Replace that placeholder with production-safe host composition, but keep tests able to register explicit test schemes.
- `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs` currently checks for any auth scheme outside Development. Strengthen it so production cannot pass with a cookie, hermetic, or unrelated scheme.
- `src/Hexalith.Folders.Server/Authentication/HttpContextEventStoreClaimTransformEvidenceAccessor.cs` already reads `eventstore:tenant`, `eventstore:permission`, and `sub`/`NameIdentifier`; preserve that authorization contract.
- `src/Hexalith.Folders.Server/Authentication/TenantContextOptions.cs` defaults `TenantClaimType` to `tenant_id`. That may be acceptable for test hosts, but production must align with `eventstore:tenant` from S-2/S-3 or explicitly configure the difference.
- `src/Hexalith.Folders.UI/CompositionRoot.cs` already has real OIDC for the Blazor console and a Development/Test-only hermetic branch. Preserve the branch restriction and do not make UI auth the server API auth implementation.
- `ConfigureProviderBinding` and `ProviderBindingConfigured` already capture `CredentialReferenceId` as metadata-only state. Keep it as a reference; never replace it with resolved secret material.
- `GitHubProvider` and `ForgejoProvider` already resolve credentials through injectable resolver interfaces and dispose credential leases in `finally` blocks. Extend this path instead of bypassing the provider ports.
- `UnconfiguredGitHubCredentialResolver` and `UnconfiguredForgejoCredentialResolver` fail closed. Keep them useful for hermetic tests and hosts without production secret-store configuration.
- Story 7.1 created production Dapr artifacts under `deploy/dapr/production/` and validated stable app IDs: `eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui`. Secret-store work must preserve those IDs and bindings.

### Architecture Compliance

- Production authorization order is contractual: JWT validation -> EventStore claim transform -> tenant-access projection freshness -> folder ACL -> EventStore validators -> Dapr deny-by-default policy evidence.
- S-2 production OIDC is pluggable across Keycloak, Microsoft Entra ID, Auth0, or any compliant OIDC provider exposing discovery metadata, but validation behavior is frozen by architecture.
- S-5 provider credential storage permits credential references only through Hexalith.Tenants or Dapr secret store; raw credential values must not enter Folders events, projections, logs, traces, metrics, diagnostics, docs examples, or tests.
- Aggregate handlers must remain pure. Do not call Dapr, HTTP, file I/O, secret stores, or provider APIs inside aggregate handlers.
- Dapr secret stores are components with names used by applications when accessing secrets. Dapr secret scopes are needed because configured stores otherwise expose all secrets available in that store to the app.
- In Kubernetes, Dapr automatically provisions a default `kubernetes` secret store. When limiting secret access, include that default store in restrictive scopes so it does not remain an unscoped fallback.

### Suggested Implementation Shape

Use this shape as guidance, not as copy/paste:

```csharp
services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidc.Authority;
        options.Audience = oidc.Audience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
        options.AutomaticRefreshInterval = TimeSpan.FromMinutes(10);
        options.RefreshInterval = TimeSpan.FromMinutes(1);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = oidc.ValidIssuer,
            ValidateAudience = true,
            ValidAudience = oidc.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
        };
    });
```

The real implementation should validate required options at startup, use central options classes, and avoid hard-coded environment values. If the OIDC provider uses a metadata address different from `Authority`, make it explicit in non-secret configuration and still pin issuer/audience.

### Testing Requirements

- Prefer xUnit v3 + Shouldly. Use fake Dapr secret accessors rather than real Dapr sidecars for PR tests.
- Tests must not require Keycloak, Entra ID, Auth0, GitHub, Forgejo, production secret stores, Kubernetes, provider credentials, or network calls.
- Test diagnostics may name option keys, synthetic app IDs, synthetic secret reference IDs, failure categories, and correlation IDs. They must not print token values, raw secret dictionaries, full provider payloads, production URLs, or local absolute paths.
- Secret-store conformance tests should parse YAML semantically with YamlDotNet, not rely only on string checks.
- If adding a gate script, follow `tests/tools/run-dapr-policy-conformance-gates.ps1` style: metadata-only report, exit-code propagation, optional restore/build skip, and no recursive submodules.

### Previous Story Intelligence

- Story 7.1 completed production Dapr deny-by-default artifacts and conformance tests. Reuse `deploy/dapr/production/`, `tests/fixtures/`, `docs/operations/`, and the existing contract-spine workflow style where applicable.
- Story 7.1 review found that control-plane configuration, sidecar binding evidence, and pub/sub scopes were easy to under-specify. Apply the same standard to secret-store scopes: prove both the component and the application binding/scope.
- Story 7.1's live `daprd`/kind 403 gate remains intentionally deferred to Story 7.8. Do not smuggle live secret-store/provider checks into PR CI for this story.
- Prior verification succeeded in native WSL with SDK `10.0.300` and root-level submodules initialized. In restricted sandboxes, NuGet/network, `pwsh`, or VSTest sockets may block exact commands; record those limitations without claiming success.
- Existing dirty worktree item `_bmad-output/story-automator/orchestration-7-20260530-075630.md` is unrelated. Do not include it in this story's File List.

### Git Intelligence Summary

- Recent commits show Epic 7 is actively adding production evidence:
  - `0355fd1 feat(story-7.1): deploy production Dapr deny-by-default access control`
  - `c973300 feat(dapr): Implement deny-by-default access control and mTLS configuration for production`
  - `3d0cc42 feat: add MCP configuration and preflight snapshot files`
  - `aa07653 BMAD 6.8.0`
  - `b971dee chore: remove story-automator orchestration output files`
- Keep this story scoped to authentication configuration, Dapr secret-store references/scopes, provider credential resolver integration, conformance tests, and operations docs.

### Project Structure Notes

- Likely UPDATE files:
  - `src/Hexalith.Folders.Server/Program.cs` - add authentication/authorization middleware in correct order.
  - `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` - replace placeholder auth registration or compose production auth extension.
  - `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs` - strengthen fail-closed production validation.
  - `src/Hexalith.Folders.Server/Authentication/TenantContextOptions.cs` - align production claim defaults/configuration with `eventstore:tenant` and `sub`.
  - `src/Hexalith.Folders.UI/CompositionRoot.cs` and `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs` - review/adjust only as needed for production OIDC safety.
  - `src/Hexalith.Folders/Providers/GitHub/*Credential*` and `src/Hexalith.Folders/Providers/Forgejo/*Credential*` - add Dapr-backed resolvers while preserving unconfigured fail-closed defaults.
  - `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs` and `ProviderRepositoryBindingRequest.cs` - update only if needed to carry explicit `CredentialReferenceId`.
  - `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs` - update only if the process manager must pass credential references to provider requests.
- Likely NEW files:
  - `src/Hexalith.Folders.Server/Authentication/FoldersOidcOptions.cs`
  - `src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs`
  - `src/Hexalith.Folders/Providers/Abstractions/IProviderCredentialReferenceResolver.cs` or a similarly narrow abstraction
  - `deploy/dapr/production/secretstore.yaml`
  - `docs/operations/production-identity-and-secrets.md`
  - focused tests under `tests/Hexalith.Folders.Server.Tests/Authentication/`, `tests/Hexalith.Folders.Tests/Providers/`, and possibly `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.2`] - Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication-&-Security`] - S-2 production OIDC, S-3 claim mapping, S-4 authorization layering, S-5 credential references only.
- [Source: `docs/exit-criteria/s2-oidc-validation.md`] - Approved frozen OIDC parameter set and placeholder issuer/audience values.
- [Source: `_bmad-output/project-context.md#Framework-Specific-Rules`] - Stable Dapr app IDs, production authorization order, Contract Spine, metadata-only rules.
- [Source: `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`] - Current auth placeholder and domain-service registration.
- [Source: `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs`] - Current generic non-Development auth-scheme startup check.
- [Source: `src/Hexalith.Folders.Server/Authentication/HttpContextEventStoreClaimTransformEvidenceAccessor.cs`] - Existing `eventstore:tenant`, `eventstore:permission`, and principal claim extraction.
- [Source: `src/Hexalith.Folders.UI/CompositionRoot.cs`] - Existing UI OIDC and Development/Test-only hermetic auth branch.
- [Source: `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBinding.cs`] and [Source: `src/Hexalith.Folders/Aggregates/Organization/ProviderBindingConfigured.cs`] - Existing metadata-only credential reference capture.
- [Source: `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`] and [Source: `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs`] - Existing provider credential resolver seam and lease disposal pattern.
- [Source: `_bmad-output/implementation-artifacts/7-1-deploy-production-dapr-deny-by-default-access-control.md#Previous-Story-Intelligence`] - Story 7.1 Dapr production artifact and verification lessons.
- [Source: Microsoft Learn, `JwtBearerOptions.TokenValidationParameters`, checked 2026-05-30] - `JwtBearerOptions` exposes token validation parameters for identity-token validation. https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions.tokenvalidationparameters?view=aspnetcore-10.0
- [Source: Microsoft Learn, `JwtBearerOptions.AutomaticRefreshInterval` and `RefreshInterval`, checked 2026-05-30] - .NET 10 JWT bearer options expose automatic metadata refresh and forced-refresh minimum intervals. https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions.automaticrefreshinterval?view=aspnetcore-10.0 and https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions.refreshinterval?view=aspnetcore-10.0
- [Source: Microsoft Learn, "Configure JWT bearer authentication in ASP.NET Core", checked 2026-05-30] - APIs should validate signature, issuer, audience, and expiration for JWT bearer tokens. https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- [Source: Dapr docs, "Secret store components", checked 2026-05-30] - Dapr secret stores are named components used by applications to access secrets. https://docs.dapr.io/operations/components/setup-secret-store/
- [Source: Dapr docs, "How To: Use secret scoping", checked 2026-05-30] - Secret scopes restrict which secrets an app can read from stores. https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-scopes/
- [Source: Dapr docs, "How-To: Reference secrets in components", checked 2026-05-30] - Components should use `secretKeyRef` and `auth.secretStore` instead of inline production secret values. https://docs.dapr.io/operations/components/component-secrets/
- [Source: Dapr docs, "Kubernetes secrets", checked 2026-05-30] - Kubernetes deployments get a default `kubernetes` secret store, which must be included when limiting access. https://docs.dapr.io/reference/components-reference/supported-secret-stores/kubernetes-secret-store/

## Dev Agent Record

### Agent Model Used

Codex GPT-5 (story context generation)

### Debug Log References

- 2026-05-30 - Created story context for Story 7.2 from BMAD create-story workflow.
- Loaded workflow customization: no activation prepend/append steps; persistent project-context fact loaded from `_bmad-output/project-context.md`.
- Loaded whole planning artifacts: `_bmad-output/planning-artifacts/epics.md`, `prd.md`, `architecture.md`, `ux-design-specification.md`; selectively extracted Epic 7, S-2/S-5/S-4, deployment structure, and project structure.
- Loaded previous story `_bmad-output/implementation-artifacts/7-1-deploy-production-dapr-deny-by-default-access-control.md` and recent git history for Dapr production artifact lessons.
- Inspected current implementation files for server authentication placeholder, UI OIDC branch, organization credential reference state, provider credential resolver seams, and repository provisioning request flow.
- Checked current official Microsoft Learn and Dapr docs on 2026-05-30 for JWT bearer option names, JWKS refresh intervals, Dapr secret store components, secret references, secret scoping, and Kubernetes default secret store behavior.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story includes explicit anti-leak, anti-reinvention, production-startup, and Dapr secret-scope guardrails.
- Story status set to ready-for-dev.

### File List

- `_bmad-output/implementation-artifacts/7-2-configure-production-oidc-and-secret-store-integration.md`
