# Provider Compatibility Catalog

This catalog is the canonical, versioned record of the compatibility assumptions the provider
adapters enforce. It publishes the product/instance identity, call-ceiling, and readiness-outcome
profiles for both governed providers, records every unenforced ceiling as a numbered gap, and carries
the OQ4 governance record. Forgejo remains pending live operator evidence, and product release
acceptance still requires the remaining open release items.

## Catalog identity and OQ4 governance

- Catalog version: `1.0.0`
- Canonical path: `docs/contract/provider-compatibility-catalog.md`
- Evidence package: `docs/contract/oq4-provider-compatibility-evidence.yaml` binds this catalog version, its SHA-256 digest, the published ceilings, the recorded gaps, and the authority records.
- Governed provider profiles: `github` and `forgejo`.
- OQ4 status: approved
- OQ4 approval record: 2026-09-05 by jpiquot; GitHub profile in this catalog is the approved compatibility profile for Stories 3.10 and 3.11. Forgejo live evidence and full provider-ready closure remain separate.
- OQ4 authority approval: Provider, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the Provider authority.
- OQ4 authority approval: Architecture, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the Architecture authority.
- OQ4 authority approval: PM, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the PM authority.
- OQ4 reopen policy: any change to the canonical catalog content, the catalog version, the SHA-256 digest, the required authority set, an approver identity, or an approval date reopens Provider, Architecture, and PM approval.
- OQ4 runtime posture: this catalog closes the OQ4 compatibility-catalog decision and criterion C12 on the narrowed evidence standard below. It completes no dependent story and it claims no credentialed live provider run.
- C12 evidence standard: C12 is closed on hermetic-PR-gate provider contract evidence plus scheduled containerized and fixture drift evidence. Credentialed live provider runs against GitHub and Forgejo are reported as explicitly not run and remain residual provider-ready debt.

## Product and instance identity profile

| Field | github | forgejo |
| --- | --- | --- |
| Provider family key | `github` | `forgejo` |
| Instance model | the single hosted `api.github.com` service; no self-hosted enterprise base URL is an accepted target | an operator-supplied authorized HTTPS base URL, escaped per segment, with redirects rejected |
| Product identity header | `Hexalith-Folders` | `Hexalith-Folders` |
| API surface identity | `X-GitHub-Api-Version: 2022-11-28` on every owned request, one dated REST API behind the pinned SDK | `forgejo-rest-v1` observed through `/version` against an exact supported product version |
| Observed product versions | not version-negotiated; the pinned SDK package is the compatibility referent | exactly `16.0.3` latest stable and `15.0.7` LTS |
| SDK and native pin | Octokit `14.0.0` | LibGit2Sharp `0.32.0` with bundled libgit2 `1.8.6` |
| Accepted credential modes | `AppInstallationReference` and `UserDelegatedReference` | `UserDelegatedReference` and `ServiceAccountReference` |
| Capability profile schema | GitHub capability rows from `GitHubReadinessMapper` | `v1`, Forgejo capability rows from `ForgejoReadinessMapper` |
| Drift evidence lane | `tests/contracts/github/pinned-profile.json` pinned-profile manifest plus the failure-mode coverage matrix | `tests/contracts/forgejo/supported-versions.json` per-version Swagger snapshot manifest |

## Call-ceiling profile

Every ceiling below is published as observed in adapter code. A ceiling with no enforcing constant is recorded
as a numbered catalog gap instead of being presented as an enforced bound. No ceiling in this table is a target
or an aspiration.

| Ceiling | Provider scope | Published value | Enforcing constant | Recorded gap |
| --- | --- | --- | --- | --- |
| `CC1` | github | five-second mutation-send budget per owned mutation request | `OctokitGitHubApiClient.MaximumMutationRequestElapsed` | none |
| `CC2` | github | five-second tree-observation budget per staged-tree read-back | `OctokitGitHubApiClient.MaximumTreeElapsed` | none |
| `CC3` | github | per-call REST timeout on SDK-dispatched calls | none | `PG1` |
| `CC4` | forgejo | thirty-second REST response and body deadline per owned operation request | `ForgejoHttpApiClient.OperationResponseTimeout` | none |
| `CC5` | forgejo | thirty-second managed `HttpClient` deadline | `ForgejoHttpApiClientFactory` HttpClient timeout | none |
| `CC6` | forgejo | thirty-second native smart-HTTPS transport deadline | `ForgejoSmartHttpGitTransport.NativeOperationDeadline` | none |
| `CC7` | github, forgejo | fifteen-minute reconciliation window holding at most five read-only checks | `GitHubProvider.ReconciliationWindow` and `ForgejoProvider.OperationReconciliationWindow` | none |
| `CC8` | github, forgejo | twenty-four-hour `Retry-After` clamp on surfaced retry evidence | `OctokitGitHubApiClient.BoundedRetryAfter` and `ForgejoHttpApiClient` retry-after bounding | `PG3` |
| `CC9` | github, forgejo | per-call retry limit and backoff cap | none | `PG2` |
| `CC10` | github, forgejo | one hundred changes per atomic change set | `GitHubProvider.MaximumChangeCount` and `ForgejoProvider.MaximumOperationChangeCount` | none |
| `CC11` | github, forgejo | one MiB decoded content per file | `GitHubProvider.MaximumFileBytes` and `ForgejoProvider.MaximumOperationFileBytes` | none |
| `CC12` | github, forgejo | ten MiB aggregate decoded content per change set | `GitHubProvider.MaximumAggregateContentBytes` and `ForgejoProvider.MaximumOperationAggregateContentBytes` | none |

## Readiness-outcome profile

Both adapters publish the same nine capability rows through their readiness mappers. A row that is not granted
becomes `Unavailable` carrying `ProviderPermissionInsufficient`; no row is silently omitted and no row is
reported as ready on missing evidence. The canonical operation identifier `workspace_preparation` deliberately
carries no readiness row in either capability profile, and `cleanup_expiration` is published as `Unsupported`
rather than unavailable.

| Readiness row | github outcome | forgejo outcome |
| --- | --- | --- |
| `readiness_validation` | `Supported` | `Supported` |
| `provider_support_evidence` | `Supported` on granted metadata read, otherwise `Unavailable` | `Supported` on granted metadata read, otherwise `Unavailable` |
| `repository_creation` | `Supported` on granted administration write, otherwise `Unavailable` | `Supported` on granted organization repository creation, otherwise `Unavailable` |
| `repository_binding` | `Supported` on granted metadata read, otherwise `Unavailable` | `Supported` on granted repository observation, otherwise `Unavailable` |
| `branch_ref_inspection` | `Supported` on granted contents read, otherwise `Unavailable` | `Supported` on granted ref observation, otherwise `Unavailable` |
| `file_mutation_support` | `Partial`, because staging uses Git Data blobs and trees and never the Contents API | `Supported` through bounded smart-HTTPS fetch and local bare-tree staging |
| `commit_support` | `Supported` on granted contents write, otherwise `Unavailable` | `Supported` through non-force receive-pack with expected-old compare-and-swap |
| `status_query` | `Supported` on granted exact-ref observation, otherwise `Unavailable` | `Supported` through version recheck plus exact REST ref observation |
| `cleanup_expiration` | `Unsupported` | `Unsupported` |

Rate-limit posture is published with every readiness result as `bounded_retry` when retry-after evidence is
present and retryable, and as `no_retry` otherwise. Retryability never authorizes retrying an ambiguous
mutation. Live readiness execution remains owned by Story 3.3; this profile publishes the mapped outcome shape,
not a completed live readiness run.

## Recorded catalog gaps

| Gap | Statement | Owner |
| --- | --- | --- |
| `PG1` | The GitHub adapter pins no adapter-level per-call REST timeout. Only the five-second mutation-send and five-second tree-observation budgets bound owned calls; the SDK-dispatched create, repository read, and branch read run under the SDK transport defaults, so `CC3` has no enforcing constant. | Provider Readiness |
| `PG2` | No retry-limit or backoff-cap ceiling has a referent, because no provider adapter implements a retry loop. Ambiguous mutations are never retried, and the bounded read-only status checks are caller-driven inside `CC7`, so `CC9` is published as absent rather than as a number. | Provider Readiness |
| `PG3` | The twenty-four-hour `Retry-After` clamp bounds a value the provider passes through; it is not a backoff algorithm. It caps the retry evidence surfaced to callers and never authorizes replay of an ambiguous mutation. | Provider Readiness |

## GitHub hermetic drift lane

GitHub REST is one dated API behind a pinned SDK package, so the GitHub drift lane is a pinned-profile manifest
plus the C12 fixture-to-failure-mode coverage matrix rather than a schema-snapshot diff. The manifest is
`tests/contracts/github/pinned-profile.json`, and `GitHubDriftConformanceTests` proves that the manifest agrees
with this catalog and with the centrally pinned package, and that every provider-neutral failure category this
catalog claims maps to exactly one proving fixture. The lane performs no network call to any GitHub endpoint.

Provider-neutral failure categories claimed by the GitHub profile:

- `UnsupportedProviderCapability`
- `ProviderUnavailable`
- `ProviderAuthenticationRequired`
- `ProviderPermissionInsufficient`
- `ProviderRateLimited`
- `ProviderValidationFailed`
- `ProviderConflict`
- `ProviderFailureKnown`
- `ProviderTransientFailure`
- `UnknownProviderOutcome`

## GitHub profile

- Story authority: Story 3.10 comes from the approved 2026-07-14 structural correction as amended on 2026-07-15. Historical Story 3.3 completion does not complete this split story.
- SDK: Octokit `14.0.0`, centrally pinned in `references/Hexalith.Builds/Props/Directory.Packages.props`.
- REST profile: every owned request sends `X-GitHub-Api-Version: 2022-11-28`. Updating this value is compatibility work and requires focused transport and mapping verification.
- Product header: `Hexalith-Folders`.
- Accepted credential modes: `AppInstallationReference` and `UserDelegatedReference`. Credential values remain secret-store leases and are never persisted or emitted.
- Creation scope: organization repository creation with the authorized organization, repository name, and visibility. The request pins `auto_init=false` and supplies no license or gitignore template, so the adapter does not create an initial commit or implicitly choose an alternate default branch.
- Owner-kind constraint: creation targets an organization endpoint only. The resolved target carries no owner-kind discriminator, so a user-account owner is not a supported creation target and is observed as a concealed-or-missing organization rather than a distinct configuration failure. Supporting user-owned creation is a deliberate model change, not incidental work.
- Creation/target asymmetry: creation consumes owner, repository name, and visibility only. Because `auto_init=false` leaves the new repository with no branch, the resolved target's default branch, selected ref, protection requirement, and permission requirements cannot be verified at creation time and are enforced on the binding path instead. A newly created repository is therefore not re-read against the resolved target.
- Cancellation limitation: Octokit `14.0.0` exposes `IRepositoriesClient.Create`, `IRepositoriesClient.Get`, and `IRepositoryBranchesClient.Get` without any `CancellationToken` overload, so the caller's token cannot interrupt these calls once entered. The adapter therefore observes cancellation only before dispatch and returns `CancellationBeforeDispatch`; it never claims to have cancelled an in-flight GitHub request. Post-conflict reconciliation checks the token before its read-only lookup. Bounding an already-dispatched call requires a transport-level deadline, which this profile does not yet pin.
- Permission assumptions: repository creation requires administration write; metadata lookup requires metadata read; exact branch lookup requires contents read; branch-protection inspection requires administration read.
- Identity and alias rule: equivalence is based on the canonical repository ID plus separately authorized operation/binding-intent evidence. Input spelling, case variation, redirects, renames, and an `already exists` response are not sufficient proof by themselves.
- Branch/ref rule: the default branch and selected branch are compared with ordinal exact semantics. Prefix matches are not accepted. Required protection is inspected independently from branch existence. Tag and commit selectors are rejected as unsupported operations until an accepted profile defines their exact observation semantics.
- Rate-limit rule: a primary rate limit and a secondary rate limit are distinct internal conditions. Both may include bounded retry-after evidence, but retryability never authorizes retrying an ambiguous mutation. Rate-limit headers are read case-insensitively because HTTP/2 lowercases header names; `Retry-After` is accepted as delta-seconds or an HTTP-date, and a primary limit falls back to `X-RateLimit-Reset` epoch seconds. A rate limit observed while inspecting branch protection is a rate limit, not an administration-permission failure, even though Octokit models it as a `ForbiddenException` subclass.
- Reconciliation rule: cancellation before dispatch sends no mutation. Timeout, disconnect, cancellation, or malformed evidence after dispatch may return `unknown_provider_outcome`; there is no blind retry. A later read-only canonical-identity check may establish equivalent or conflicting state.
- Evidence binding: admitted target, path, content, staged tree, commit message, expected head, intended commit, full ref, and check-window evidence use versioned, domain-separated, length-prefixed SHA-256 bindings. Text is NFC-normalized UTF-8, collections remain caller-ordered, and comparison is fixed-time. Every binding also includes the high-entropy operation and authorization identities; a shape-valid digest alone is not authority.
- Reservation and replay: a persistence-neutral outcome seam reserves one opaque operation identity before dispatch and distinguishes acquired, pending, exact terminal replay, conflict, and unavailable outcomes. The generation is revalidated immediately before the first write. Pending and terminal replays never dispatch again. After dispatch, recording uses a five-second internal budget independent of caller cancellation; a failed record becomes metadata-only unknown evidence tied to the same operation identity.
- File-mutation scope: Story 3.11 uses Git Data requests (`git/blobs` followed by one `git/trees` request) in caller order. It never uses the Contents API, never auto-commits a file, and never updates a ref while staging. Add and change operations create base64 blobs; remove operations are ordered null-SHA tree entries. The authorized expected head is observed exactly before staging and a moved head is a provider conflict. The adapter permits at most 100 changes, 1 MiB decoded content per file, and 10 MiB aggregate content, checked before base64 allocation or dispatch.
- Tree-observation profile: only touched paths must be regular blobs with mode `100644`; unrelated executable files, symlinks, submodules, and nested trees are accepted. Every existing touched-path ancestor must remain a tree with mode `040000`. The created tree is read back by its returned SHA, additions and changes must reproduce their blob SHA, and deletions must be absent. A truncated recursive read falls back to non-recursive traversal of touched ancestors and fails closed at 64 requests, 256 entries per response, depth 32, 7 MiB per response, or five seconds. Recursive truncation is inspected before the non-recursive 256-entry cap; a truncated, oversized, slash-delimited, wrong-mode, or late fallback response never proves deletion.
- Explicit-commit scope: one Git commit is created from the authorized staged tree, exact message, and sole expected parent. Its returned SHA is read back and the private commit identity is recorded before at most one `force=false` ref update. The PATCH response alone is not success: one post-update GET must reproduce the exact full ref, a commit object, and the intended commit SHA. A moved head, protected branch, 409, or 422 never authorizes overwrite or a second update.
- Status scope: status is read-only and rejects an idempotency key before target, credential, content, or provider access. Each authorized call performs one exact ref observation and classifies it as confirmed, not applied, conflicting, or unavailable. Another full ref, a non-commit or divergent head is conflicting. An exact-ref 404 performs one bounded repository-visibility probe: a visible repository proves the ref was deleted and is conflicting, while missing, concealed, denied, or failed visibility evidence remains unavailable. The caller supplies the operation-bound check number and reconciliation-window evidence; the five read-only checks fit one 15-minute window, and only not-applied or unavailable checks 1 through 4 are retryable before the deadline. Check five, expiration, or conflict maps to `reconciliation_required`.
- Transport profile: dynamic GitHub REST segments are escaped and every raw request carries bearer authorization, the `Hexalith-Folders` user agent, GitHub media type, and API version. HTTP infrastructure is pooled. Delta-seconds and HTTP-date `Retry-After` plus case-insensitive, range-checked `X-RateLimit-Reset` epoch seconds are accepted and bounded. Every mutation send uses a private five-second token after a final caller-cancellation gate. Mutation-phase 429, 5xx, disconnect, cancellation, response-limit, and malformed responses are ambiguous and non-retryable; read-only status failures retain the bounded checks 1 through 4 retry posture.
- Failure scope: 400, 401, 403, 404, 409, 422, 429, primary/secondary rate limits, and 5xx responses retain stable provider-neutral mapping. Timeout, disconnect, cancellation, malformed success, or transport ambiguity after a mutation dispatch maps to `unknown_provider_outcome`, is non-retryable, and carries only an opaque reconciliation identity.
- Output rule: Octokit DTOs, provider response bodies, credentials, owner/repository/ref labels, URLs, and raw exceptions remain inside the GitHub adapter. Provider-neutral results carry only stable categories, opaque operation/binding references, safe fingerprints, bounded retry evidence, and reconciliation disposition.

## Forgejo profile

- External-evidence status: awaiting operator execution against an approved isolated installation. Repository code, hermetic transport proof, and the opt-in evidence runner are complete; this catalog is not evidence that a deployment was exercised.
- Transport: `ForgejoHttpApiClient` uses Forgejo REST v1 through a managed `HttpClient` for readiness, provisioning, binding, and status. File staging and commit use centrally pinned LibGit2Sharp `0.32.0` with bundled libgit2 `1.8.6` over smart HTTPS. Redirects are disabled, bearer authorization stays in memory, and no provider or native DTO crosses the adapter boundary.
- Supported versions: exactly `16.0.3` latest stable and `15.0.7` LTS. Each runtime profile is bound to a reviewed operation subset and the SHA-256 of its tagged upstream Swagger artifact. The retained `15.0.2`, `14.0.5`, and `11.0.14` fixtures are retired drift evidence and are never accepted as nearby substitutes.
- Accepted credential modes: `UserDelegatedReference` and `ServiceAccountReference`. Credential values remain short-lived secret-store leases and never appear in provider-neutral requests, results, reports, or documentation.
- Creation scope: one organization-scoped `POST /api/v1/orgs/{org}/repos` with `auto_init=false`, the exact resolved name, and public/private visibility. Internal visibility is rejected because the accepted create shape does not prove an internal-visibility request field. A 429, 5xx, disconnect, cancellation, oversized body, or malformed success after dispatch is an ambiguous non-retryable outcome; the adapter never issues a blind second POST.
- Existing-target rule: a documented 400 or an observed 409/422 permits one read-only `GET /api/v1/repos/{owner}/{repo}`. Equivalence requires prior authorization for equivalent-existing handling and an ordinal match to the expected canonical numeric repository ID. Otherwise the result remains `provider_conflict`.
- Binding/ref rule: the adapter observes the repository, exact selected branch, and required branch-protection rule in order. Canonical numeric ID, visibility, exact default branch, pull/admin permissions, exact selected branch, and exact effective protection name must all agree with the resolved policy. Tags and commit selectors are unsupported until an accepted profile defines their observation semantics.
- File-mutation rule: after exact-version and upload-pack capability checks, staging performs one bounded read-only fetch of the exact authorized head into a fresh private bare repository. It applies 1-100 caller-ordered regular-file changes locally, records the actual Git tree identity, creates no commit, sends no receive-pack, and removes the repository. Content is limited to 1 MiB per file and 10 MiB aggregate; paths are relative, normalized, unique, at most 500 characters, and free of ancestor conflicts. Changes/removals require the exact source blob identity and mode `100644`; touched executable and symbolic-link entries are rejected.
- Explicit-commit rule: commit revalidates the durable reservation after the native-operation gate, rechecks the exact version and smart-HTTP capabilities, then performs one fresh exact-head fetch. It reconstructs and verifies the staged tree, creates one local commit whose sole parent is the expected head, records that identity before mutation, and sends one non-force receive-pack update for the exact full ref and expected old SHA. A final read-only exact-ref observation must confirm the intended commit. The retained `repoChangeFiles` REST shape is incompatibility evidence and is never dispatched.
- Status/reconciliation rule: status is REST-only and read-only: it rechecks `/version` and observes the exact `git/refs/{ref}` endpoint. The intended commit, unchanged expected head, or any other identity becomes confirmed, not-applied, or conflicting. An exact-ref 404 becomes conflicting only when one bounded repository observation proves the repository remains visible; concealed, denied, missing, or failed visibility remains unavailable. Checks 1-4 may retry only unavailable or not-applied observations inside a 15-minute window; check 5, expiry, conflict, and version drift require reconciliation. An ambiguous receive-pack is never resent.
- Durable admission: current authorization and target freshness are validated before admission. Fresh requests resolve opaque repository/profile/policy references through `IProviderRepositoryTargetResolver`; conflicting, expired, malformed, and exact terminal replays stop before target resolution, credential lookup, client construction, or provider access. Prior unknown outcomes remain unknown and retain only their opaque reconciliation evidence.
- Redirect and response rule: every dynamic path segment is escaped; redirects are rejected without following credentials. REST JSON remains bounded, smart-HTTP advertisements are capped at 1 MiB, native transfer at 32 MiB, and the temporary repository at 64 MiB under one wall-clock operation deadline. Retry-after evidence is bounded to 24 hours and never authorizes replay of an ambiguous mutation.
- Failure scope: validation, authentication, permission, concealed/missing resources, expected-old conflicts, arbitrary remote rejection, remote policy rejection, unsupported refs/object formats/smart HTTP, rate limiting, native-load or transport unavailability, response/transfer/disk/deadline limits, schema drift, malformed evidence, cleanup failure, and post-dispatch mutation ambiguity map separately to stable provider-neutral categories. Only evidence of an expected-old mismatch is a provider conflict. Raw response bodies, URLs, repository/ref/path/message values, credentials, temporary paths, native output, and exception messages remain Forgejo-private.
- Live evidence: `pwsh ./tests/tools/run-forgejo-provider-evidence-gates.ps1` is deliberately absent from PR and scheduled CI. It combines the hermetic admission/transport matrix with controlled version, create, identity, conflict, bind/ref, denial, tenant-isolation, and boundary observations, and archives only scenario/status metadata.
- Alpine smart-HTTPS evidence: `pwsh ./tests/tools/run-forgejo-smart-http-alpine-smoke.ps1` starts disposable TLS Forgejo `16.0.3` and `15.0.7` containers and runs the musl build inside the production .NET 10 Alpine base. It verifies native loading, exact received commit/tree/content/removal evidence, and a post-advertisement stale-old race, then destroys its containers, network, certificate, and credential.

## Ownership and readiness limits

- Story 3.3 owns the live `GetReadinessAsync` implementation. It remains a prerequisite for a production end-to-end readiness claim and is not absorbed by Story 3.10.
- Story 3.10 owns GitHub repository creation, existing-repository binding, canonical identity, alias/duplicate handling, and branch/ref validation through the existing provider port.
- Story 3.11 owns GitHub file mutation, commit, and status behavior.
- Story 3.13 owns Forgejo atomic file mutation, explicit commit, and read-only status behavior. Hermetic production-registration and concrete HTTP evidence completes the adapter surface; it does not claim a credentialed live mutation run.
- Story 3.11 live mutation archive: waived 2026-09-06 (closure path C). Hermetic adapter/transport proof plus the catalog GitHub profile complete Story 3.11. A credential-gated live mutation/commit/status archive remains residual full provider-ready debt; this catalog does not claim a live GitHub mutation run occurred.
- Story 3.14 owns the runtime subscription, durable asynchronous orchestration, reconciliation scheduling, and final folder-binding transition.
- The configured-policy source needed to implement `IProviderRepositoryTargetResolver` is not present. Production dependency injection therefore registers a fail-closed resolver; an authorized in-memory target is exercised only through the internal provider seam until Story 3.8 configuration exposes an authoritative source.
- The durable target/content/staged-object source needed to implement the Story 3.11 operation resolver is intentionally absent until Stories 12.3/12.4 and 4.20/4.21 compose it. Production still resolves exactly one concrete GitHub/Octokit provider through `IGitProvider`; its file mutation, commit, and status operations fail closed with `provider_file_mutation_source_unconfigured`, `provider_commit_source_unconfigured`, or `provider_operation_status_source_unconfigured` before credential or provider access. This is provider-component composition evidence, not successful outer workspace-task execution evidence.
- OQ8 remains the authority for idempotency-record retention, expired-key tombstones, and `idempotency_key_expired` precedence. Existing repository flows prove unexpired equivalent/conflicting replay and restart no-mutation behavior, but Story 3.10 cannot invent the missing retention source or claim expired-key acceptance.
- Safe target fingerprints are not durable. The repository-creation fingerprint binds the repository profile reference under the `github-target-v1` label, and no worker, aggregate, projection, or emitted event reads a stored fingerprint today. Whoever first persists one owes a version-label bump, because a fingerprint whose inputs changed under an unchanged label silently invalidates stored evidence.
- The existing OpenAPI Contract Spine already carries opaque repository binding identities and canonical failure categories, including provider conflict, idempotency conflict/expiry, unknown outcome, and reconciliation required. Story 3.10 therefore introduces no public contract change.
- OQ4 catalog approval and the C12 closure it carries are governance records only. Stories 3.3, 3.14, 12.1, and 12.3 remain incomplete; the Forgejo credentialed live-evidence lane has not been run; and the Story 3.11 GitHub live-mutation archive stays waived rather than executed. This catalog grants none of them a completion claim, and the scheduled drift lane reports credentialed live provider evidence as explicitly not run.

Full GitHub provider-ready status requires OQ8 acceptance plus completion evidence from Stories 3.3, 3.10, 3.11, and 3.14. The GitHub OQ4 profile in this catalog is approved; this catalog must not be interpreted as complete provider-ready or release acceptance.

Forgejo provider-ready status additionally requires an operator to run the opt-in evidence lane against an approved isolated HTTPS deployment with least-privilege positive, denial, and tenant-isolation credentials. The configured-policy and durable content/outcome sources remain fail-closed until authoritative tenant and workspace sources are composed by their owning stories, and Story 3.14 still owns durable outer orchestration and final folder-binding transitions.
