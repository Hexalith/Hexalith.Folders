# Provider Compatibility Catalog

This catalog records the compatibility assumptions used by the provider adapters. It is implementation evidence, not an approval record. Human acceptance of the product and provider profile remains a release decision.

## GitHub profile

- OQ4 status: pending-human-acceptance
- Story authority: Story 3.10 comes from the approved 2026-07-14 structural correction as amended on 2026-07-15. Historical Story 3.3 completion does not complete this split story.
- SDK: Octokit `14.0.0`, centrally pinned in `references/Hexalith.Builds/Props/Directory.Packages.props`.
- REST profile: every owned request sends `X-GitHub-Api-Version: 2022-11-28`. Updating this value is compatibility work and requires focused transport and mapping verification.
- Product header: `Hexalith-Folders`.
- Accepted credential modes: `AppInstallationReference` and `UserDelegatedReference`. Credential values remain secret-store leases and are never persisted or emitted.
- Creation scope: organization repository creation with the authorized organization, repository name, and visibility. The request pins `auto_init=false` and supplies no license or gitignore template, so the adapter does not create an initial commit or implicitly choose an alternate default branch.
- Permission assumptions: repository creation requires administration write; metadata lookup requires metadata read; exact branch lookup requires contents read; branch-protection inspection requires administration read.
- Identity and alias rule: equivalence is based on the canonical repository ID plus separately authorized operation/binding-intent evidence. Input spelling, case variation, redirects, renames, and an `already exists` response are not sufficient proof by themselves.
- Branch/ref rule: the default branch and selected branch are compared with ordinal exact semantics. Prefix matches are not accepted. Required protection is inspected independently from branch existence. Tag and commit selectors are rejected as unsupported operations until an accepted profile defines their exact observation semantics.
- Rate-limit rule: a primary rate limit and a secondary rate limit are distinct internal conditions. Both may include bounded retry-after evidence, but retryability never authorizes retrying an ambiguous mutation.
- Reconciliation rule: cancellation before dispatch sends no mutation. Timeout, disconnect, cancellation, or malformed evidence after dispatch may return `unknown_provider_outcome`; there is no blind retry. A later read-only canonical-identity check may establish equivalent or conflicting state.
- File-mutation scope: Story 3.11 uses Git Data requests (`git/blobs` followed by one `git/trees` request) in caller order. It never uses the Contents API, never auto-commits a file, and never updates a ref while staging. Add and change operations create base64 blobs; remove operations are ordered null-SHA tree entries. The authorized expected head is observed exactly before staging and a moved head is a provider conflict.
- Explicit-commit scope: one Git commit is created from the authorized staged tree and expected parent, followed by at most one `force=false` ref update. Success is reported only when the ref-update response confirms the intended commit identity. A moved head, protected branch, 409, or 422 never authorizes overwrite or a second update.
- Status scope: status is read-only and rejects an idempotency key before target, credential, content, or provider access. Each authorized call performs one exact ref observation and classifies it as confirmed, not applied, conflicting, or unavailable. The caller supplies the durable check number and reconciliation-window evidence; durable scheduling owns the limit of five read-only checks in the 15-minute window, while conflicting or exhausted evidence maps to `reconciliation_required`.
- Failure scope: 400, 401, 403, 404, 409, 422, 429, primary/secondary rate limits, and 5xx responses retain stable provider-neutral mapping. Timeout, disconnect, cancellation, malformed success, or transport ambiguity after a mutation dispatch maps to `unknown_provider_outcome`, is non-retryable, and carries only an opaque reconciliation identity.
- Output rule: Octokit DTOs, provider response bodies, credentials, owner/repository/ref labels, URLs, and raw exceptions remain inside the GitHub adapter. Provider-neutral results carry only stable categories, opaque operation/binding references, safe fingerprints, bounded retry evidence, and reconciliation disposition.

## Ownership and readiness limits

- Story 3.3 owns the live `GetReadinessAsync` implementation. It remains a prerequisite for a production end-to-end readiness claim and is not absorbed by Story 3.10.
- Story 3.10 owns GitHub repository creation, existing-repository binding, canonical identity, alias/duplicate handling, and branch/ref validation through the existing provider port.
- Story 3.11 owns GitHub file mutation, commit, and status behavior.
- Story 3.14 owns the runtime subscription, durable asynchronous orchestration, reconciliation scheduling, and final folder-binding transition.
- The configured-policy source needed to implement `IProviderRepositoryTargetResolver` is not present. Production dependency injection therefore registers a fail-closed resolver; an authorized in-memory target is exercised only through the internal provider seam until Story 3.8 configuration exposes an authoritative source.
- The durable target/content/staged-object source needed to implement the Story 3.11 operation resolver is intentionally absent until Stories 12.3/12.4 and 4.20/4.21 compose it. Production still resolves exactly one concrete GitHub/Octokit provider through `IGitProvider`; its file mutation, commit, and status operations fail closed with `provider_file_mutation_source_unconfigured`, `provider_commit_source_unconfigured`, or `provider_operation_status_source_unconfigured` before credential or provider access. This is provider-component composition evidence, not successful outer workspace-task execution evidence.
- OQ8 remains the authority for idempotency-record retention, expired-key tombstones, and `idempotency_key_expired` precedence. Existing repository flows prove unexpired equivalent/conflicting replay and restart no-mutation behavior, but Story 3.10 cannot invent the missing retention source or claim expired-key acceptance.
- The existing OpenAPI Contract Spine already carries opaque repository binding identities and canonical failure categories, including provider conflict, idempotency conflict/expiry, unknown outcome, and reconciliation required. Story 3.10 therefore introduces no public contract change.

Full GitHub provider-ready status requires human OQ4 and OQ8 acceptance plus completion evidence from Stories 3.3, 3.10, 3.11, and 3.14. This catalog must not be interpreted as that acceptance.
