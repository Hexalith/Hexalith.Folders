# Provider Integration and Contract Testing

This document describes how Hexalith.Folders integrates with Git providers, how provider capability,
readiness, retryability, and failure evidence are modelled, and how the **GitHub** and **Forgejo** adapters
differ. It **reflects** the contracts — the provider port `IGitProvider`, the capability model under
`src/Hexalith.Folders/Providers/Abstractions/`, the GitHub adapter under
`src/Hexalith.Folders/Providers/GitHub/`, the Forgejo adapter under
`src/Hexalith.Folders/Providers/Forgejo/`, and the readiness query sources under
`src/Hexalith.Folders/Queries/ProviderReadiness/` — it never redefines them. Every example uses opaque
synthetic identifiers (for example `provider-001`, `tenant-001`, `correlation-001`) and placeholder hosts
only.

The API/SDK/CLI/MCP consumer manuals are already published by Story 7.13 — cross-link, not redefined here:
[`docs/sdk/api-reference.md`](../sdk/api-reference.md), [`docs/sdk/cli-reference.md`](../sdk/cli-reference.md),
[`docs/sdk/mcp-reference.md`](../sdk/mcp-reference.md), and [`docs/sdk/authentication.md`](../sdk/authentication.md).
Canonical error interpretation is in [`canonical-error-catalog.md`](canonical-error-catalog.md).

## Provider port and capability model

The provider boundary is the `IGitProvider` port (`ProviderFamily`, `ProviderKey`,
`DiscoverCapabilitiesAsync`, `CreateRepositoryAsync`, `ValidateRepositoryBindingAsync`,
`CompareCapabilityProfiles`). Capability discovery is **N-provider capable**: it is driven by
`ProviderOperationCatalog`, `ProviderCapabilityProfile`, `ProviderOperationCapability`,
`ProviderOperationSupport`, and `DefaultProviderCapabilityResolver`, and is **not hardcoded to GitHub plus
Forgejo**. Any provider that implements `IGitProvider` and reports capability evidence participates on equal
terms; GitHub and Forgejo are simply the two adapters that ship today.

Each provider reports support for the **ten canonical operations** in `ProviderOperationCatalog`. The
operation identifier is normalized through `ProviderOperationIdentifier` so adapter-specific aliases resolve
to one canonical id.

<!-- provider-capability-operations -->

| Operation | Purpose |
|---|---|
| `readiness_validation` | Validate that a provider binding can currently serve operations |
| `repository_creation` | Create a repository (idempotent on an existing equivalent) |
| `repository_binding` | Validate/bind an external repository reference |
| `branch_ref_inspection` | Inspect branch/ref state and policy |
| `workspace_preparation` | Prepare a working copy for mutation |
| `file_mutation_support` | Apply metadata-described file mutations |
| `commit_support` | Commit prepared changes |
| `status_query` | Query workspace/task status |
| `cleanup_expiration` | Expire/clean disposable working copies |
| `provider_support_evidence` | Publish capability/version evidence for the support read model |

Each `ProviderOperationCapability` records an operation id, a `ProviderOperationSupport` state
(`Supported`, `Unsupported`, `Partial`, `Emulated`, `Unavailable`), metadata-only `Limits` and `Constraints`
dictionaries, a default `Retryable` flag, and an optional `ProviderFailureCategory`. The
`ProviderRateLimitPosture` record carries a `Classification`, a `Retryable` flag, an optional `RetryAfter`,
and metadata — never a token or live endpoint.

## Credential references are metadata-only

Provider credentials are **references**, never stored secrets. They are resolved at use time through the
Dapr-backed `DaprProviderCredentialReferenceResolver` and `DaprProviderCredentialSecretStoreClient`
(`IProviderCredentialReferenceResolver` / `IProviderCredentialSecretStoreClient`). Folders state never stores
a token, and **no example in this repository publishes a token, a credential URL, or secret material**. The
credential intent is captured by `ProviderCredentialMode`, which has **exactly four members**, all
reference-only.

<!-- provider-credential-modes -->

| Credential mode | Posture |
|---|---|
| `None` | No credential reference resolved (unconfigured) |
| `AppInstallationReference` | Reference to an app-installation credential, resolved through the secret store |
| `UserDelegatedReference` | Reference to a user-delegated credential, resolved through the secret store |
| `ServiceAccountReference` | Reference to a service-account credential, resolved through the secret store |

The only provider-identity and target evidence ever surfaced is **safe, opaque metadata**:
`ProviderIdentityIdentifier`, `ProviderTargetEvidence`, and permission/rate-limit/version evidence records
(`GitHubPermissionEvidence`, `GitHubRateLimitEvidence`, `ForgejoPermissionEvidence`,
`ForgejoRateLimitEvidence`, `ForgejoVersionEvidence`). The credential validators
(`GitHubCredentialModeValidator`, `ForgejoCredentialModeValidator`) check that the resolved reference matches
an accepted mode; they never echo the secret.

## GitHub integration behavior

The GitHub adapter (`GitHubProvider`) talks to GitHub through `OctokitGitHubApiClient`
(`IGitHubApiClient` / `IGitHubApiClientFactory`). The **Octokit** dependency is the architecture-sanctioned
implementation detail and stays **inside the GitHub provider boundary** — it does not leak into the provider
abstractions. Credentials are resolved by `DaprBackedGitHubCredentialResolver` and validated by
`GitHubCredentialModeValidator`; permission and rate-limit evidence are captured by `GitHubPermissionEvidence`
and `GitHubRateLimitEvidence`.

`GitHubReadinessMapper` and `GitHubFailureMapper` translate raw `GitHubApiFailureCondition` outcomes into the
**stable `ProviderFailureCategory` vocabulary** — never raw Octokit payloads. Notable mappings: primary and
secondary rate limits both map to `ProviderRateLimited`; server unavailability maps to `ProviderUnavailable`;
a not-found-or-hidden resource maps to `ProviderPermissionInsufficient` (so a caller cannot probe for a
resource it cannot see); branch-protection conflict maps to `ProviderConflict`; and a **timeout during a
mutation or an unexpected transport failure maps to `UnknownProviderOutcome`** with a
`reconciliation_required_metadata_only` remediation code — the system never silently retries a mutation whose
outcome is unknown.

## Forgejo integration behavior, supported versions, and drift

The Forgejo adapter (`ForgejoProvider`) uses a **typed HTTP** client (`ForgejoHttpApiClient`,
`IForgejoApiClient` / `IForgejoApiClientFactory`) rather than a third-party SDK. Credentials are resolved by
`DaprBackedForgejoCredentialResolver` and validated by `ForgejoCredentialModeValidator`;
`ForgejoReadinessMapper` and `ForgejoFailureMapper` map `ForgejoApiFailureCondition` outcomes into the same
stable `ProviderFailureCategory` vocabulary.

Forgejo support is pinned to **exactly three versions** in `ForgejoSupportedVersionCatalog`, each backed by a
checked-in `swagger.v1.json` snapshot under `tests/contracts/forgejo/<version>/`.

<!-- forgejo-supported-versions -->

| Version | Family | Support class | Snapshot |
|---|---|---|---|
| `15.0.2` | 15.0 | latest-stable-lts | `tests/contracts/forgejo/15.0.2/swagger.v1.json` |
| `14.0.5` | 14.0 | n-1-discontinued-reference | `tests/contracts/forgejo/14.0.5/swagger.v1.json` |
| `11.0.14` | 11.0 | older-lts | `tests/contracts/forgejo/11.0.14/swagger.v1.json` |

Forgejo classifies drift explicitly: `ForgejoApiFailureCondition.VersionIncompatible` and
`SchemaDriftBreaking` both map to `ReconciliationRequired`, and a cross-origin redirect maps to
`ProviderReadinessFailed`. **An unsupported or failing provider version cannot report ready** — readiness
validation fails closed rather than guessing. Forgejo additionally distinguishes a missing repository and a
missing branch/path (both `ProviderValidationFailed`) and an unsupported capability
(`UnsupportedProviderCapability`), distinctions the GitHub mapper does not draw.

## GitHub versus Forgejo capability differences

The two adapters are **not at parity**; each records provider-specific evidence. This table compares them by
dimension and never claims parity where only provider-specific evidence exists.

<!-- github-forgejo-capability-differences -->

| Dimension | GitHub | Forgejo |
|---|---|---|
| Supported operations | Octokit-backed catalog evidence | Typed-HTTP catalog evidence |
| Branch/ref behavior | Branch-protection conflict via Octokit | Branch/path and protection distinctions via typed HTTP |
| File limits | Reported as capability `Limits` metadata | Reported as capability `Limits` metadata |
| Credential mode | App-installation / user-delegated / service-account references | App-installation / user-delegated / service-account references |
| Version/capability metadata | No pinned version catalog (hosted) | Three pinned versions with swagger snapshots |
| Rate-limit posture | Primary and secondary rate limits both retryable | Single rate-limit condition, retryable |
| Readiness behavior | Fails closed on unavailable evidence | Fails closed; unsupported version cannot be ready |
| Repository create/bind | Idempotent on existing equivalent | Idempotent on existing equivalent |
| File/commit/status behavior | Mapped through `GitHubFailureMapper` | Mapped through `ForgejoFailureMapper` |
| Unknown outcome handling | Timeout/transport map to `unknown_provider_outcome` | Timeout/cancellation/transport map to `unknown_provider_outcome` |
| Drift evidence | Not applicable (hosted API) | Version-incompatible and schema-drift map to `reconciliation_required` |

## Provider readiness, retryability, and remediation

Readiness is decided by `ProviderReadinessValidationService` and surfaced through the read model
(`IProviderSupportEvidenceReadModel`, `ProviderSupportEvidenceQueryHandler`). The result is one of **seven
`ProviderReadinessResultCode` values**, including the stale/unavailable/read-model-unavailable cases that keep
readiness honest when projection evidence is not fresh.

<!-- provider-readiness-result-codes -->

| Result code | Meaning |
|---|---|
| `Allowed` | Binding is ready for the requested capability |
| `AuthenticationRequired` | Credential reference is missing or unresolved |
| `AuthorizationDenied` | Resolved identity lacks the required permission |
| `ValidationFailed` | Requested capability or evidence failed validation |
| `ProjectionStale` | Readiness evidence projection is stale |
| `ProjectionUnavailable` | Readiness evidence projection is unavailable |
| `ReadModelUnavailable` | The provider-support read model is unavailable |

Provider failures are classified into the **fifteen `ProviderFailureCategory` members**, each with a stable
`ToCategoryCode()` snake_case code and a default retryability from `IsRetryableByDefault()`. Only
`ProviderUnavailable`, `ProviderRateLimited`, and `ProviderTransientFailure` are **retryable by default**;
every other category is not, because retrying them could duplicate work or mask a real fault. Domain services
translate readiness/provider failures into `FolderResultCode` values, which the server then maps to canonical
categories (see [`canonical-error-catalog.md`](canonical-error-catalog.md)).

<!-- provider-failure-categories -->

| Category | Category code | Retryable by default |
|---|---|---|
| `None` | `none` | no |
| `UnsupportedProviderCapability` | `unsupported_provider_capability` | no |
| `ProviderUnavailable` | `provider_unavailable` | yes |
| `ProviderAuthenticationRequired` | `provider_authentication_required` | no |
| `ProviderConfigurationMissing` | `provider_configuration_missing` | no |
| `ProviderPermissionInsufficient` | `provider_permission_insufficient` | no |
| `ProviderRateLimited` | `provider_rate_limited` | yes |
| `ProviderValidationFailed` | `provider_validation_failed` | no |
| `ProviderConflict` | `provider_conflict` | no |
| `ProviderReadinessFailed` | `provider_readiness_failed` | no |
| `ProviderFailureKnown` | `provider_failure_known` | no |
| `ProviderTransientFailure` | `provider_transient_failure` | yes |
| `UnknownProviderOutcome` | `unknown_provider_outcome` | no |
| `ReconciliationRequired` | `reconciliation_required` | no |
| `InternalError` | `internal_error` | no |

## Known provider failure handling and no silent retry

The adapters classify known provider failures into stable categories so operators can diagnose them without
reading payloads: **timeout**, **401/403 authentication or permission**, **404 / missing repository**,
**409 / repository conflict**, **429 / rate limit**, **5xx / unavailable**, **branch protection**,
**stale clone**, **credential revocation**, **provider drift**, **unknown outcome**, and
**reconciliation required**. Rate-limit, unavailable, and transient failures are retryable by default; the
rest are not.

When a provider outcome is **unknown or ambiguous** — for example a mutation that timed out, was cancelled
mid-flight, or returned an unexpected transport error — the adapter records `unknown_provider_outcome` or
`reconciliation_required`, **not** a guessed success or failure. The system **must not silently retry** when
retrying could duplicate a repository, a file change, or a commit; such outcomes are surfaced truthfully and
wait for reconciliation. Provider drift runbooks and alert/rollback procedures are owned by Story 7.17 and are
out of scope here.

## Metadata-only policy

This document, the provider evidence records, and the gate report are output channels subject to the
metadata-only invariant: no secrets, bearer tokens, credential material, raw file contents, base64 file bytes,
diffs, provider payloads, raw provider responses, real repository URLs, embedded-credential URLs, production
hosts, environment dumps, stack traces, tenant data, or host-absolute paths. Examples use opaque synthetic
identifiers and placeholder hosts only.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-provider-error-docs-gates.ps1
```

The gate runs the `ProviderErrorDocsConformanceTests` and writes a metadata-only report to
`_bmad-output/gates/provider-error-docs/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`) when the
shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate falls back to
the xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm the report reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the capability operations, credential modes, readiness result
codes, provider failure categories, and supported Forgejo versions stay synchronized with their sources.
Rerun the gate after any change to the provider abstractions, the GitHub or Forgejo adapters, the readiness
sources, or `ForgejoSupportedVersionCatalog`. The static gate runs in the `contract-spine` CI lane and through
the baseline CI Contracts.Tests filter; it is not promoted to a new top-level `ci.yml` lane, to
`release-packages.yml`, or to scheduled workflows.
