---
baseline_commit: 0355fd1b121ae159c5e002ad1c6b0fed3af02af2
---

# Story 7.2: Configure production OIDC and secret store integration

Status: done

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

- [x] Wire production JWT bearer authentication in the server host (AC: 1, 2, 3, 4)
  - [x] Add server OIDC options, suggested `src/Hexalith.Folders.Server/Authentication/FoldersOidcOptions.cs`, bound from a non-secret section such as `Folders:Authentication`.
  - [x] Add a service registration extension, suggested `AddFoldersProductionAuthentication(...)`, that calls `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`.
  - [x] Set `MapInboundClaims = false` or otherwise prove `sub`, `eventstore:tenant`, and `eventstore:permission` are consumed without framework claim-type remapping drift.
  - [x] Configure `TokenValidationParameters` with the S-2 values from `docs/exit-criteria/s2-oidc-validation.md`.
  - [x] Set `JwtBearerOptions.AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` and `JwtBearerOptions.RefreshInterval = TimeSpan.FromMinutes(1)`.
  - [x] Add `app.UseAuthentication(); app.UseAuthorization();` in `src/Hexalith.Folders.Server/Program.cs` before `MapFoldersServerEndpoints()`.
  - [x] Update `TenantContextOptions` defaults or configuration so production tenant access uses `eventstore:tenant`, while Development/Test hermetic paths remain explicit and visibly non-production.
  - [x] Strengthen `FoldersAuthSchemeValidator` or add a new hosted validator so non-Development startup requires the JWT bearer scheme plus issuer/audience pins, not merely "some authentication scheme".

- [x] Preserve UI OIDC behavior while aligning production safety (AC: 2, 3, 4)
  - [x] Review `src/Hexalith.Folders.UI/CompositionRoot.cs` and `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs`.
  - [x] Keep the `hermetic-test` branch restricted to Development/Test.
  - [x] Ensure UI OIDC does not save or forward raw tokens into logs, app state, console payloads, or Folders projections. If `SaveTokens = true` remains necessary for backend calls, document why and cover it with metadata-only tests.
  - [x] Keep UI claim usage compatible with the server's authoritative `eventstore:tenant`/`sub` claim contract or explicitly map only in the UI circuit boundary.

- [x] Add Dapr secret-store production artifacts (AC: 5, 8)
  - [x] Add sanitized production fixture(s), suggested `deploy/dapr/production/secretstore.yaml` and/or a secret-scope section in `deploy/dapr/production/daprsystem.yaml`.
  - [x] Define the secret store component name used by code, but keep the backing provider pluggable: Kubernetes secret store is acceptable as a production baseline, with Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, or another Dapr-supported store allowed by configuration.
  - [x] Add secret scopes with `defaultAccess: deny` and explicit allowed synthetic secret reference IDs for `folders` and `folders-workers`; include the default Kubernetes store in deny scopes when limiting secret access.
  - [x] Bind sidecars to the relevant Dapr configuration through `deploy/dapr/production/sidecar-config-bindings.yaml` without changing Story 7.1's stable app IDs or Dapr access-control assumptions.
  - [x] Do not add local JSON secret files, real Kubernetes Secret manifests, real vault paths, provider tokens, OAuth client secrets, private keys, certificates, or production endpoints to source.

- [x] Implement reference-only provider credential resolution (AC: 5, 6, 7)
  - [x] Identify how `CredentialReferenceId` from `ConfigureProviderBinding` / `ProviderBindingConfigured` reaches provider runtime paths. Current provider creation/binding requests carry `ProviderBindingRef` but not `CredentialReferenceId`; bridge this intentionally through the organization provider binding read model or request contracts before resolving secrets.
  - [x] Add a provider-agnostic credential reference abstraction if needed, e.g. `IProviderCredentialReferenceResolver`, so GitHub and Forgejo do not duplicate Dapr secret-store policy logic.
  - [x] Implement GitHub and Forgejo Dapr-backed resolvers that use `DaprClient.GetSecretAsync(secretStoreName, credentialReferenceId, metadata, cancellationToken)` and return existing `GitHubCredentialLease` / `ForgejoCredentialLease` types.
  - [x] Map missing, denied, malformed, or unavailable secret-store responses to existing provider failure categories such as `ProviderConfigurationMissing`, `ProviderAuthenticationRequired`, `ProviderPermissionInsufficient`, `ProviderUnavailable`, or `ReconciliationRequired` without exposing the secret reference value to unauthorized callers.
  - [x] Keep unconfigured resolvers as the default fail-closed behavior unless production registration opts in.
  - [x] Ensure credential leases are disposed in every provider path and token strings are cleared, as current providers already attempt in `finally` blocks.

- [x] Add tests and conformance fixtures (AC: all)
  - [x] Add server auth tests under `tests/Hexalith.Folders.Server.Tests/Authentication/` for S-2 `JwtBearerOptions`, missing issuer/audience startup failure, no introspection, claim mapping, and middleware order where practical.
  - [x] Add provider credential tests under `tests/Hexalith.Folders.Tests/Providers/{GitHub,Forgejo}/` using a fake Dapr secret accessor. Cover success, missing secret, denied secret, malformed multi-value secret, unavailable secret store, and cancellation propagation.
  - [x] Add static YAML tests, suggested `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ProductionSecretStoreConformanceTests.cs`, that validate production secret-store artifacts are sanitized, deny by default, include expected app IDs, and contain no credential-shaped values.
  - [x] Extend sentinel/security tests to prove `ProviderBindingConfigured`, organization provider binding replay state, provider readiness diagnostics, and repository provisioning outcomes store only credential reference IDs or redacted metadata, never resolved secret values.
  - [x] Add or update a focused gate script only if the existing safety/governance gates cannot cover the new conformance tests. Keep scripts hermetic: no production endpoints, no provider calls, no secret store network calls, no artifact upload, no recursive submodule setup.

- [x] Document operations handoff (AC: 2, 5, 8)
  - [x] Update `docs/exit-criteria/s2-oidc-validation.md` or add `docs/operations/production-identity-and-secrets.md` with configuration keys, required environment-owned issuer/audience values, allowed placeholder examples, and secret-store promotion steps.
  - [x] Document that Folders source stores credential references only; platform operators provision actual secret values in the configured Dapr secret store.
  - [x] Document failure categories and operator actions for missing/denied/unavailable credential references without including secret names beyond sanitized examples.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [x] Run the focused server auth, provider credential resolver, and secret-store conformance tests.
  - [x] Run relevant safety/governance gates that scan for secret leakage.
  - [x] Run `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src` and confirm no recursive submodule setup guidance was introduced.
  - [x] Run a secret-shaped scan across story-touched artifacts, at minimum for `ghp_`, `github_pat_`, `client_secret`, `private_key`, `BEGIN .*PRIVATE KEY`, `password=`, `token=`, and production-looking URLs.
  - [x] Record exact commands, pass/fail counts, and environment limitations in the Dev Agent Record.

## Dev Notes

### Critical Scope Boundaries

- This is production readiness/security configuration work, not a new product surface. Do not add REST/SDK/CLI/MCP operations, UI mutation paths, new provider capabilities, or new folder lifecycle semantics.
- Do not store or generate real OIDC client secrets, JWTs, refresh tokens, provider tokens, private keys, certificates, tenant data, provider payloads, repository names, or production URLs.
- Do not replace provider authorization with secret-store access. Secret retrieval happens only after existing layered authorization and provider binding/readiness checks have selected an authorized credential reference.
- Do not derive secret names from tenant IDs, folder IDs, repository IDs, provider target URLs, or `ProviderBindingRef`. Use the explicit `CredentialReferenceId` captured by organization provider binding state.
- Do not initialize nested submodules or add recursive submodule commands to CI, docs, or scripts.
- Current package pins are authoritative: .NET SDK `10.0.302`, `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.5`, Dapr packages `1.17.9`, Aspire Hosting `13.3.5`, xUnit v3 `3.2.2`, and YamlDotNet `18.0.0`.

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
- Prior verification succeeded in native WSL with SDK `10.0.302` and root-level submodules initialized. In restricted sandboxes, NuGet/network, `pwsh`, or VSTest sockets may block exact commands; record those limitations without claiming success.
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

Codex GPT-5

### Implementation Plan

- Wire server production JWT bearer authentication through `Folders:Authentication`, preserving Development/Test hermetic paths.
- Carry explicit `CredentialReferenceId` through readiness, repository creation, and repository binding provider paths.
- Add Dapr-backed provider credential resolution behind opt-in DI while keeping unconfigured resolvers fail-closed.
- Prove production Dapr secret-store artifacts are sanitized and scoped to explicit synthetic references only.
- Validate with focused auth, provider credential, Dapr conformance, safety invariant, restore, build, and static leakage scans.

### Debug Log References

- 2026-05-30 - Loaded BMAD dev-story workflow, checklist, customization, sprint status, story file, and project-context facts.
- 2026-05-30 - Verified story was already `in-progress` with baseline commit `0355fd1b121ae159c5e002ad1c6b0fed3af02af2`; preserved it.
- 2026-05-30 - Compared implementation state against baseline and found existing 7.2-shaped server auth, provider credential, Dapr artifact, docs, and tests.
- 2026-05-30 - Fixed production Dapr secret scopes so `folders` and `folders-workers` both have explicit allowed synthetic credential references while other app IDs deny default Kubernetes secret access.
- 2026-05-30 - Fixed server build issues: imported the authentication extension namespace in `Program.cs`, avoided nullable assignment to `JwtBearerOptions.MetadataAddress`, and registered the OIDC options validator with a typed descriptor.
- 2026-05-30 - Fixed focused tests to assert fail-closed `OptionsValidationException` for missing issuer/audience production configuration.
- 2026-05-30 - Fixed safety inventory include-root drift for audit endpoint/projection sources so the safety invariant gate can scan its covered artifacts.
- 2026-05-30 - `dotnet test` and PowerShell gate scripts that invoke VSTest abort in this sandbox with `System.Net.Sockets.SocketException (13): Permission denied`; used the xUnit v3 in-process runner for executable test assemblies.

### Completion Notes List

- Registered production JWT bearer authentication with S-2 frozen validation parameters, no inbound claim remapping, issuer/audience pins, JWKS refresh intervals, and production startup validation.
- Preserved UI OIDC behavior, kept `hermetic-test` restricted to Development/Test, and documented why server-side Blazor keeps tokens in the protected auth session for backend calls.
- Added sanitized Dapr production secret-store evidence and secret scopes for `folders` and `folders-workers` with deny-by-default behavior and no raw provider credentials.
- Implemented reference-only Dapr-backed GitHub/Forgejo credential resolution from explicit `CredentialReferenceId` values, with lease disposal clearing token strings and unconfigured resolvers remaining fail-closed.
- Carried credential references through provider readiness, repository creation, repository binding, and worker provisioning request paths.
- Added and ran focused server auth, provider credential, Dapr conformance, worker propagation, and safety invariant tests.
- Verification results:
  - `dotnet restore Hexalith.Folders.slnx -m:1 --verbosity minimal` passed.
  - `dotnet build Hexalith.Folders.slnx --no-restore -m:1 --verbosity minimal` passed with 0 warnings and 0 errors.
  - `Hexalith.Folders.Server.Tests ... FoldersProductionAuthenticationTests` passed: 6 total, 0 failed.
  - `Hexalith.Folders.Contracts.Tests ... DaprPolicyConformanceTests` passed: 8 total, 0 failed.
  - `Hexalith.Folders.Tests ... DaprBackedProviderCredentialResolverTests` passed: 8 total, 0 failed.
  - `Hexalith.Folders.Workers.Tests ... RepositoryProvisioningProcessManagerTests` passed: 8 total, 0 failed.
  - `Hexalith.Folders.Contracts.Tests ... SafetyInvariantGateTests` passed: 11 total, 0 failed.
  - Diff-level recursive submodule scan passed: no `git submodule update --init --recursive` or `--recursive` introduced.
  - Diff-level secret-shaped scan passed: no `ghp_`, `github_pat_`, `client_secret`, `private_key`, private-key block, `password=`, `token=`, or production-looking URL introduced.

### File List

- `_bmad-output/implementation-artifacts/7-2-configure-production-oidc-and-secret-store-integration.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/dapr/production/accesscontrol.yaml`
- `deploy/dapr/production/secretstore.yaml`
- `docs/exit-criteria/s2-oidc-validation.md`
- `docs/operations/production-identity-and-secrets.md`
- `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs`
- `src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/Authentication/FoldersOidcOptions.cs`
- `src/Hexalith.Folders.Server/Authentication/TenantContextOptions.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`
- `src/Hexalith.Folders.Server/Program.cs`
- `src/Hexalith.Folders.Testing/Providers/ProviderCapabilityTestData.cs`
- `src/Hexalith.Folders.UI/CompositionRoot.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `src/Hexalith.Folders/Providers/Abstractions/DaprProviderCredentialReferenceResolver.cs`
- `src/Hexalith.Folders/Providers/Abstractions/DaprProviderCredentialSecretStoreClient.cs`
- `src/Hexalith.Folders/Providers/Abstractions/FoldersProviderCredentialOptions.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderCredentialReferenceResolver.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderCredentialSecretStoreClient.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfileFactory.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialReferenceResolutionRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialReferenceResolutionResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialSecretLookupResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/Forgejo/DaprBackedForgejoCredentialResolver.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoCredentialResolutionRequest.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/DaprBackedGitHubCredentialResolver.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubCredentialResolutionRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/Authentication/FoldersProductionAuthenticationTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/DaprBackedProviderCredentialResolverTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs`
- `tests/fixtures/safety-channel-inventory.json`

## Senior Developer Review (AI)

**Reviewer:** jpiquot — 2026-05-30 (story-automator adversarial review, auto-fix mode)

**Outcome:** Approved. All Story 7.2 acceptance criteria are met, the review fixes below are verified, and no CRITICAL issue attributable to Story 7.2 remains. One pre-existing, out-of-scope test failure unrelated to this story is noted below.

### ⚠️ Out-of-scope observation (pre-existing, NOT a Story 7.2 blocker)

The full `Hexalith.Folders.Contracts.Tests` run is **91 total, 4 failed**:

- `FileContextContractGroupTests.FileContextContractNotes_RecordDeferredOwnersAndNegativeScope`
- `CommitStatusContractGroupTests.CommitStatusContractNotes_RecordDeferredOwnersAndNegativeScope`
- `TenantFolderProviderContractGroupTests.ContractGroupOperations_PreserveNegativeScope`
- `AuditOpsConsoleContractGroupTests.AuditOpsConsoleNegativeScope_DoesNotAddRuntimeAdaptersGeneratedClientsUiOrCi`

**Root cause (confirmed):** these are stale **negative-scope guards from Stories 1.7 / 1.10 / 1.11** that assert the CLI was not yet built. They fail with `Directory.Exists(forbiddenRoot) should be False but was True` / `Story 1.10 negative scope: CLI commands (Hexalith.Folders.Cli/Commands/**/*.cs) must not be added`, because `src/Hexalith.Folders.Cli/Commands/**` now exists (10 command files: CommandFactory, CommandPipeline, CommandOptions, and the Workspace/File/Folder/Provider/Context/Commit/Audit commands). The failing test files are unmodified vs HEAD, and **nothing in Story 7.2 touches the CLI** — 7.2 adds production auth and Dapr secret-store credential resolution only. This is therefore a **pre-existing repo-health issue unrelated to Story 7.2**: a later CLI story added `Hexalith.Folders.Cli/Commands` without updating the epic-1 contract-spine negative-scope allow-lists. The 7.2-specific gates (`DaprPolicyConformanceTests`, `SafetyInvariantGateTests` incl. the new provider-credential channel) all pass. **Suggested separate follow-up (not owned by 7.2):** update the 1.7/1.10/1.11 negative-scope guards so they reflect that the CLI now legitimately exists.

Review method: 6 parallel adversarial review dimensions (AC 1–4 auth, AC 5–6 secret store, AC 7 provider credential propagation, AC 8 test quality, AC 8 secret-leakage, task-audit/correctness), each finding independently verified, then every confirmed finding re-checked against the current working tree before acting.

### Fixes applied automatically

1. **[CRITICAL → fixed] Production code used `CreateForTesting()` factory** (`DaprBackedGitHubCredentialResolver.cs`, `DaprBackedForgejoCredentialResolver.cs`). The credential leases exposed only a `private` constructor plus a public `CreateForTesting(...)` factory, so production resolvers minted leases through a test-named factory. Changed `GitHubCredentialLease`/`ForgejoCredentialLease` constructors to `internal` (with argument validation) and switched both resolvers to `new …Lease(result.AccessToken!)`. `CreateForTesting` now appears only in the lease concurrency tests, where its name is accurate.
2. **[HIGH → fixed] Secret-scope denial (403) collapsed into "unavailable"** (`DaprProviderCredentialSecretStoreClient.cs`). All `DaprApiException`s mapped to `Unavailable`, so the resolver's `Denied → ProviderPermissionInsufficient` branch (and AC 6's denied-secret mapping) was unreachable in production. Added a gRPC `PermissionDenied` detector (`IsPermissionDenied`, via `Grpc.Core`) so a scope denial now returns `Denied` → `ProviderPermissionInsufficient`, distinct from transient unavailability.
3. **[MEDIUM → fixed] Inconsistent legacy-config fallback** (`FoldersAuthenticationServiceCollectionExtensions.cs`). `ResolveOidcOptions` (used by `AddJwtBearer`) omitted the `RequireHttpsMetadata` legacy fallback present in the options `Configure` lambda. Added the matching fallback so both configuration paths behave identically.
4. **[HIGH → fixed] Provider credential source not covered by the safety gate** (`tests/fixtures/safety-channel-inventory.json`). The new credential-handling source under `src/Hexalith.Folders/Providers/Abstractions` was absent from `include_roots`, so the permanent forbidden-value gate never scanned the most secret-sensitive code. Added that root and a `provider-credential-references` channel (`scan_forbidden_values: true`); `SafetyInvariantGateTests` passes 11/11 with the new scan.
5. **[MEDIUM → fixed] Stale verification counts** in this record (FoldersProductionAuthenticationTests 4→6, DaprBackedProviderCredentialResolverTests 7→8) corrected to match the current test files.

### Findings reviewed and rejected as false positives (verified against current working tree)

- *"Test expects wrong exception type" (alleged CRITICAL).* `ProductionValidatorShouldRejectMissingIssuerOrAudiencePins` correctly expects `OptionsValidationException`: `FoldersAuthSchemeValidator.StartAsync` reads `oidcOptions.CurrentValue`, which triggers the `IValidateOptions` validator and throws before the later `InvalidOperationException`. All 6 `FoldersProductionAuthenticationTests` pass.
- *"Missing denied / cancellation resolver tests."* Both already exist (`ReferenceResolverShouldMapDeniedSecretWithoutExposingReferenceValue`, `ReferenceResolverShouldPropagateCancellationBeforeSecretLookup`).
- *"RepositoryProvisioningProcessManagerTests missing credential-reference assertion."* The assertion exists (`sent.CredentialReferenceId.ShouldBe("credential-ref-1")`). (Several finder agents read the pre-commit revision rather than the working tree.)

### Residual action items (non-blocking, MEDIUM)

- [ ] [AI-Review][MEDIUM] Add a direct unit test for `DaprProviderCredentialSecretStoreClient` exception mapping (empty→Missing, gRPC `PermissionDenied`→Denied, other→Unavailable, cancellation re-throw). The new 403→Denied logic is currently only covered indirectly through the resolver's fake client. [`src/Hexalith.Folders/Providers/Abstractions/DaprProviderCredentialSecretStoreClient.cs`]

### Verification (review)

- `dotnet build Hexalith.Folders.slnx --no-restore` — 0 errors, 0 warnings.
- Green (xUnit v3 in-process runner; VSTest sockets are blocked in this sandbox): `FoldersProductionAuthenticationTests` 6/6; `DaprBackedProviderCredentialResolverTests` 8/8; `GitHub`/`Forgejo` `CredentialLeaseConcurrencyTests` 3/3 each; `GitHubProviderTests` and `ForgejoProviderTests` all green; `RepositoryProvisioningProcessManagerTests` 8/8.
- `Hexalith.Folders.Contracts.Tests` full assembly: **91 total, 4 failed** — `DaprPolicyConformanceTests` (8) and `SafetyInvariantGateTests` (including the new `provider-credential-references` channel) pass. The 4 failures are the stale epic-1 (1.7/1.10/1.11) CLI negative-scope guards described in the out-of-scope observation above, unrelated to Story 7.2.

### Change Log

- 2026-05-30 - Implemented production OIDC, Dapr secret-store reference resolution, provider credential reference propagation, conformance tests, safety inventory coverage, and operations documentation for Story 7.2.
- 2026-05-30 - Senior Developer Review (AI): auto-fixed lease production factory usage, Dapr 403→denied mapping, OIDC legacy-config fallback symmetry, and provider-credential safety-gate coverage; corrected stale test counts; status set to done. Noted 4 pre-existing, out-of-scope Contracts.Tests failures (stale epic-1 CLI negative-scope guards) that are unrelated to Story 7.2.
