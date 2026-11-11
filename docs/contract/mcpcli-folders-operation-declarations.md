# Folders McpCli operation declarations

Owner decision: 2026-11-10, “apply recommended”, under Story 5.12 and
DEC-EXEC-5.12. This is the Folders owner handoff for McpCli Story 5.8 against
migration inventory **1.0.0** and the v2 Contract Spine shipped in Contracts
**1.3.3**, oracle `contract_spine_sha256`
`d417afd8454dac16824af17c2c0a787fe579c342a009e13b6985b147724d89a7`. Execution is
explicitly authorized ahead of Story 1.17 closure. The next manifest
reconciliation owns `execution_authorization_status`; this story does not
change `planning-story-manifest.yaml`.

The assembly declares module `folders`, String identifiers, Explicit wire types,
public tenant selection and the default McpCli serializer. Its only package
dependency is `Hexalith.McpCli.Abstractions` 2.0.1, centrally pinned by Builds.
There are 13 Commands and 28 Queries. The seven seed-backed
`operations_console_projection` diagnostics remain undecorated and deferred;
each still requires a product-approved C13 not-applicable declaration.

Decorating these 41 types is owner consent to agent exposure without human
confirmation, including archive C01, provider-binding configuration C05,
file removal within C10 and ACL revoke C13. Gateway admission and authorization
remain mandatory. The selected live-vector chain is **C06 CreateFolder → C08
GrantFolderAccess → Q29 ListFolderAclEntries**. McpCli enrollment owns live
vectors and runtime catalog gates; these declarations alone do not authorize
retirement of the legacy binaries or establish live Gateway readiness.

## Candidate decisions and routing

`eligible` is the owner's decision for the next inventory revision; the immutable
upstream inventory retains its current `defer / exclude` disposition until
McpCli Story 5.8 revises it. The CLR names below are exact. Excluded rows retain
their candidate names and have no declaration or wire route in this assembly.
Every declared operation uses domain `folders`. Every query projection type is
the kebab operation part of its canonical inventory name.

Exemptions used by the drift gate:

- **E1:** `requestSchemaVersion` is implied by v2 admission, including nested
  request objects (Story 5.13).
- **E2:** C08 and C13 map to `UpdateFolderAclEntry`; `effect` is derived
  from operation identity, not a payload member (V08). McpCli validates raw JSON
  against its exported schema, whose `additionalProperties: false` rejects any
  caller-supplied `effect`. The local canonical STJ deserialization test is mirror
  evidence only; McpCli forwards the payload without deserializing it. Story 5.13
  also owns validation for callers reaching the Gateway directly and supplies
  `grant` or `revoke` from the wire type when translating to `UpdateFolderAclEntry`
  and the domain ACL payload; the domain already selects intent from that command type.
- **E3:** C10 maps to the `MutateFilesRequest` component and uses AddFile's path
  and task scope, preserving the canonical batch shape.
- **E4:** C06 adds caller-supplied `FolderId`; the domain requires
  `payload.FolderId == envelope.AggregateId`.
- **E5:** C05 routes on `ProviderBindingRef`. Story 5.13 must derive the reserved
  `ProviderBindingActorId` and reject an actor equal to the tenant before lookup.
- **E6:** The spine marks `X-Hexalith-Task-Id` optional on C01, C02, C04, C05,
  C06, C07, C08 and C13; McpCli carries it in `extensions["taskId"]`, not a
  payload member. The current domain nevertheless requires a caller-chosen
  `extensions["taskId"]` on every Gateway-dispatched Command, including C06
  `CreateFolder`, so task association is required for all 13 Commands; this is
  the platform-native task carrier. The value must match the domain's lowercase
  grammar `\A[a-z0-9._-]+\z` with at most 128 characters (G2/G3 below). McpCli
  profiles have an empty extension allowlist by default. For an empty allowlist,
  run `config set <profile> allowedExtensions taskId` before any of the 13 Commands
  can carry task context. This setter replaces the entire allowlist. For an
  existing allowlist, preserve every currently permitted key and include `taskId`
  in the comma-separated value, for example `existingKey,taskId` when `existingKey`
  is already permitted. McpCli enrollment (5.8/6.1 vectors) and the Folders
  5.10/5.11 parity setup own this profile prerequisite. Story 5.13 validates the
  task-value grammar and association, and reconciles the payload `TaskId` with
  the extension on C03, C09, C10, C11 and C12. Values ending in `--` are rejected
  by both McpCli and the Gateway for every caller, including REST; see the named
  platform gap below.

| ID | Owner eligibility | CLR type | Explicit wire name | Spine operation/component | Aggregate source | Exemptions | Fixed freshness |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C01 | eligible | `Hexalith.Folders.Contracts.Commands.ArchiveFolder` | `Hexalith.Folders.Commands.ArchiveFolder` | `ArchiveFolder` | `FolderId` | E1, E6 | — |
| C02 | eligible | `Hexalith.Folders.Contracts.Commands.BindRepository` | `Hexalith.Folders.Commands.BindRepository` | `BindRepository` | `FolderId` | E1, E6 | — |
| C03 | eligible | `Hexalith.Folders.Contracts.Commands.CommitWorkspace` | `Hexalith.Folders.Commands.CommitWorkspace` | `CommitWorkspace` | `FolderId` | E1 | — |
| C04 | eligible | `Hexalith.Folders.Contracts.Commands.ConfigureBranchRefPolicy` | `Hexalith.Folders.Commands.ConfigureBranchRefPolicy` | `ConfigureBranchRefPolicy` | `FolderId` | E1, E6 | — |
| C05 | eligible | `Hexalith.Folders.Contracts.Commands.ConfigureProviderBinding` | `Hexalith.Folders.Commands.ConfigureProviderBinding` | `ConfigureProviderBinding` | `ProviderBindingRef` | E1, E5, E6 | — |
| C06 | eligible | `Hexalith.Folders.Contracts.Commands.CreateFolder` | `Hexalith.Folders.Commands.CreateFolder` | `CreateFolder` | `FolderId` | E1, E4, E6 | — |
| C07 | eligible | `Hexalith.Folders.Contracts.Commands.CreateRepositoryBackedFolder` | `Hexalith.Folders.Commands.CreateRepositoryBackedFolder` | `CreateRepositoryBackedFolder` | `FolderId` | E1, E6 | — |
| C08 | eligible | `Hexalith.Folders.Contracts.Commands.GrantFolderAccess` | `Hexalith.Folders.Commands.GrantFolderAccess` | `UpdateFolderAclEntry` | `FolderId` | E1, E2, E6 | — |
| C09 | eligible | `Hexalith.Folders.Contracts.Commands.LockWorkspace` | `Hexalith.Folders.Commands.LockWorkspace` | `LockWorkspace` | `FolderId` | E1 | — |
| C10 | eligible | `Hexalith.Folders.Contracts.Commands.MutateFiles` | `Hexalith.Folders.Commands.MutateFiles` | `MutateFilesRequest` | `FolderId` | E1, E3 | — |
| C11 | eligible | `Hexalith.Folders.Contracts.Commands.PrepareWorkspace` | `Hexalith.Folders.Commands.PrepareWorkspace` | `PrepareWorkspace` | `FolderId` | E1 | — |
| C12 | eligible | `Hexalith.Folders.Contracts.Commands.ReleaseWorkspaceLock` | `Hexalith.Folders.Commands.ReleaseWorkspaceLock` | `ReleaseWorkspaceLock` | `FolderId` | E1 | — |
| C13 | eligible | `Hexalith.Folders.Contracts.Commands.RevokeFolderAccess` | `Hexalith.Folders.Commands.RevokeFolderAccess` | `UpdateFolderAclEntry` | `FolderId` | E1, E2, E6 | — |
| Q01 | eligible | `Hexalith.Folders.Contracts.Queries.GetAuditRecord` | `Hexalith.Folders.Queries.GetAuditRecord` | `GetAuditRecord` | `FolderId` | — | eventually_consistent |
| Q02 | eligible | `Hexalith.Folders.Contracts.Queries.GetBranchRefPolicy` | `Hexalith.Folders.Queries.GetBranchRefPolicy` | `GetBranchRefPolicy` | `FolderId` | — | eventually_consistent |
| Q03 | eligible | `Hexalith.Folders.Contracts.Queries.GetCommitEvidence` | `Hexalith.Folders.Queries.GetCommitEvidence` | `GetCommitEvidence` | `FolderId` | — | eventually_consistent |
| Q04 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetDirtyStateDiagnostics` | — (undecorated) | `GetDirtyStateDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q05 | eligible | `Hexalith.Folders.Contracts.Queries.GetEffectivePermissions` | `Hexalith.Folders.Queries.GetEffectivePermissions` | `GetEffectivePermissions` | `FolderId` | — | read_your_writes |
| Q06 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetFailedOperationDiagnostics` | — (undecorated) | `GetFailedOperationDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q07 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderFileMetadata` | `Hexalith.Folders.Queries.GetFolderFileMetadata` | `GetFolderFileMetadata` | `FolderId` | E1 | snapshot_per_task |
| Q08 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderIndexingStatus` | `Hexalith.Folders.Queries.GetFolderIndexingStatus` | `GetFolderIndexingStatus` | `FolderId` | — | eventually_consistent |
| Q09 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderLifecycleStatus` | `Hexalith.Folders.Queries.GetFolderLifecycleStatus` | `GetFolderLifecycleStatus` | `FolderId` | — | eventually_consistent |
| Q10 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetLockDiagnostics` | — (undecorated) | `GetLockDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q11 | eligible | `Hexalith.Folders.Contracts.Queries.GetOperationTimelineEntry` | `Hexalith.Folders.Queries.GetOperationTimelineEntry` | `GetOperationTimelineEntry` | `FolderId` | — | eventually_consistent |
| Q12 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetProjectionFreshness` | — (undecorated) | `GetProjectionFreshness` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q13 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderBinding` | `Hexalith.Folders.Queries.GetProviderBinding` | `GetProviderBinding` | `ProviderBindingRef` | — | eventually_consistent |
| Q14 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderOutcome` | `Hexalith.Folders.Queries.GetProviderOutcome` | `GetProviderOutcome` | `FolderId` | — | eventually_consistent |
| Q15 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetProviderStatusDiagnostics` | — (undecorated) | `GetProviderStatusDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q16 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderSupportEvidence` | `Hexalith.Folders.Queries.GetProviderSupportEvidence` | `GetProviderSupportEvidence` | `none` | — | eventually_consistent |
| Q17 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetReadinessDiagnostics` | — (undecorated) | `GetReadinessDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q18 | eligible | `Hexalith.Folders.Contracts.Queries.GetReconciliationStatus` | `Hexalith.Folders.Queries.GetReconciliationStatus` | `GetReconciliationStatus` | `FolderId` | — | eventually_consistent |
| Q19 | eligible | `Hexalith.Folders.Contracts.Queries.GetRepositoryBinding` | `Hexalith.Folders.Queries.GetRepositoryBinding` | `GetRepositoryBinding` | `FolderId` | — | eventually_consistent |
| Q20 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetSyncStatusDiagnostics` | — (undecorated) | `GetSyncStatusDiagnostics` | `folderId` (spine scope; undecorated) | — | eventually_consistent |
| Q21 | eligible | `Hexalith.Folders.Contracts.Queries.GetTaskStatus` | `Hexalith.Folders.Queries.GetTaskStatus` | `GetTaskStatus` | `FolderId` | — | eventually_consistent |
| Q22 | eligible | `Hexalith.Folders.Contracts.Queries.GetWorkspaceCleanupStatus` | `Hexalith.Folders.Queries.GetWorkspaceCleanupStatus` | `GetWorkspaceCleanupStatus` | `FolderId` | — | read_your_writes |
| Q23 | eligible | `Hexalith.Folders.Contracts.Queries.GetWorkspaceLock` | `Hexalith.Folders.Queries.GetWorkspaceLock` | `GetWorkspaceLock` | `FolderId` | — | read_your_writes |
| Q24 | eligible | `Hexalith.Folders.Contracts.Queries.GetWorkspaceRetryEligibility` | `Hexalith.Folders.Queries.GetWorkspaceRetryEligibility` | `GetWorkspaceRetryEligibility` | `FolderId` | — | eventually_consistent |
| Q25 | eligible | `Hexalith.Folders.Contracts.Queries.GetWorkspaceStatus` | `Hexalith.Folders.Queries.GetWorkspaceStatus` | `GetWorkspaceStatus` | `FolderId` | — | read_your_writes |
| Q26 | eligible | `Hexalith.Folders.Contracts.Queries.GetWorkspaceTransitionEvidence` | `Hexalith.Folders.Queries.GetWorkspaceTransitionEvidence` | `GetWorkspaceTransitionEvidence` | `FolderId` | — | snapshot_per_task |
| Q27 | eligible | `Hexalith.Folders.Contracts.Queries.GlobFolderFiles` | `Hexalith.Folders.Queries.GlobFolderFiles` | `GlobFolderFiles` | `FolderId` | E1 | snapshot_per_task |
| Q28 | eligible | `Hexalith.Folders.Contracts.Queries.ListAuditTrail` | `Hexalith.Folders.Queries.ListAuditTrail` | `ListAuditTrail` | `FolderId` | — | eventually_consistent |
| Q29 | eligible | `Hexalith.Folders.Contracts.Queries.ListFolderAclEntries` | `Hexalith.Folders.Queries.ListFolderAclEntries` | `ListFolderAclEntries` | `FolderId` | — | eventually_consistent |
| Q30 | eligible | `Hexalith.Folders.Contracts.Queries.ListFolderFiles` | `Hexalith.Folders.Queries.ListFolderFiles` | `ListFolderFiles` | `FolderId` | — | snapshot_per_task |
| Q31 | eligible | `Hexalith.Folders.Contracts.Queries.ListOperationTimeline` | `Hexalith.Folders.Queries.ListOperationTimeline` | `ListOperationTimeline` | `FolderId` | — | eventually_consistent |
| Q32 | eligible | `Hexalith.Folders.Contracts.Queries.ReadFileRange` | `Hexalith.Folders.Queries.ReadFileRange` | `ReadFileRange` | `FolderId` | E1 | snapshot_per_task |
| Q33 | eligible | `Hexalith.Folders.Contracts.Queries.SearchFolderFiles` | `Hexalith.Folders.Queries.SearchFolderFiles` | `SearchFolderFiles` | `FolderId` | E1 | snapshot_per_task |
| Q34 | eligible | `Hexalith.Folders.Contracts.Queries.SearchFolderIndexedFiles` | `Hexalith.Folders.Queries.SearchFolderIndexedFiles` | `SearchFolderIndexedFiles` | `FolderId` | E1 | eventually_consistent |
| Q35 | eligible | `Hexalith.Folders.Contracts.Queries.ValidateProviderReadiness` | `Hexalith.Folders.Queries.ValidateProviderReadiness` | `ValidateProviderReadiness` | `ProviderBindingRef` | — | snapshot_per_task |

The query request shape includes all path/query/body inputs and the task header
where the spine defines it, preserving its required or optional status. Freshness
is fixed per operation and is omitted from the payload (V01). Cursor, limit and
filter remain typed payload fields (V02). Command task scope is required on C03,
C09, C10, C11 and C12 as declared by `FoldersCommandCatalog`; every Command
requires a caller-supplied idempotency key independently of message identity.
The drift gate derives both command headers' schema and required flags from the
spine, checks duplicate body/header task inputs agree, and records the spine's
optional task header as an envelope extension (E6). The current domain requires
that extension on every Command, so every caller profile must allowlist `taskId`;
Story 5.13 owns the spine/domain divergence (G2/G3).

## V02 typed paging approval

The owner retains typed payload paging for V02; Story 5.14 defines the authoritative
payload/envelope precedence and rejects conflicting inputs. McpCli's
`DescriptionLinter` therefore has these eight expected `payload_paging_member`
findings. Each exact operation/code/property tuple has the following owner approval
reference for McpCli enrollment; the runtime lint gate remains McpCli-owned.

| Operation | Code | Property | approvalReference |
| --- | --- | --- | --- |
| `folders.get-provider-support-evidence` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.glob-folder-files` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.list-audit-trail` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.list-folder-acl-entries` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.list-folder-files` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.list-operation-timeline` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.search-folder-files` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |
| `folders.search-folder-indexed-files` | `payload_paging_member` | `/properties/cursor` | `docs/contract/mcpcli-folders-operation-declarations.md#v02-typed-paging-approval` |

## Accepted behavior losses

| Decision | Affected inventory and legacy rows | Rationale and accepted loss | Replacement |
| --- | --- | --- | --- |
| V04/V05 | C10; M01/M04/M13; L24–L26 (content input on add/change) | Generic typed JSON has no local-file/stdin upload helper or derived evidence. The owner accepts losing those input conveniences while retaining the evidence requirements. | Caller sends inline content, media type, decoded byte length, content-hash reference and path/policy evidence. The decoded inline limit is inclusive at 262144 bytes. Content from 262145 through 1048576 bytes remains deferred pending McpCli 5.2 staging; this declaration grants no staging readiness. Above 1048576 bytes remains invalid. |
| V07 | C01–C13; M01–M14; CLI mutation leaves L01/L05–L07/L10/L13–L14/L16–L18/L24–L27 | Generic McpCli keeps caller-owned idempotency keys and rejects legacy `sha256:` keys; the owner accepts loss of `--allow-auto-key` and hash-format sourcing. No implicit conversion or replacement key is permitted. | Caller supplies a canonical uppercase ULID. Story 5.13 preserves replay, conflict, expiry and task association during admission. |
| V09 | All eligible C01–C13 and Q01–Q35 except Q04/Q06/Q10/Q12/Q15/Q17/Q20; all CLI leaves L01–L42; M01–M49 excluding diagnostics M18/M20/M24/M26/M29/M31/M34 | The owner accepts losing legacy credential precedence, human output and distinct operational exits 64–77 because generic heads use their own profile and result contracts. | McpCli profiles, JSON output and operational exit 2; Story 5.8 records setup/result migration and secret-safe evidence. |
| R01 | Q28 ListAuditTrail; resource audit-trail | Generic McpCli v1 exposes operations without MCP resource transport. The owner accepts loss of the resource interface. | The authorized metadata query `folders.list-audit-trail`, with typed filter/cursor/limit fields and fixed eventually_consistent freshness. |
| R02 | Q30 ListFolderFiles; resource folder-tree | Generic McpCli v1 exposes operations without MCP resource transport. The owner accepts loss of the resource interface. | The task/workspace-scoped metadata query `folders.list-folder-files`, with typed cursor/limit fields and fixed snapshot_per_task freshness. |

## Named gaps and downstream ownership

- **G2/G3 — Story 5.13:** verified authority, layered authorization, revocation,
  deny-before-access, admission, task ownership and envelope comparison.
  Legacy lowercase envelope requirements must accept McpCli's uppercase ULIDs
  without lowercasing opaque payload IDs. The spine marks `X-Hexalith-Task-Id`
  optional on C01, C02, C04–C08 and C13, while the current domain and the v1
  REST path require a caller-chosen task ID on every Command (E6). The domain's
  task extension grammar is lowercase `\A[a-z0-9._-]+\z` with at most 128
  characters, whereas the payload `TaskId` opaque pattern admits uppercase: a
  mixed-case task ID satisfies the payload schema but is rejected at `/process`,
  and on C03, C09, C10, C11 and C12 the equality with the extension forces a
  lowercase payload value. Story 5.13 reconciles both divergences without
  silently converting identifiers. Request-schema version is admission
  state. Conditional file transport/add/change/remove combinations, duplicate
  detection and cross-field byte limits remain admission rules. The spine's
  `CommitWorkspaceRequest.auditMetadataKeys` has `uniqueItems: true`; rejecting
  duplicate audit keys belongs to 5.13 and is not expressible in these declarations.
- **command-payload-translation — Story 5.13:** the decorated v2 request shapes
  cannot be forwarded unchanged to the existing domain payload records under the
  same wire names. Admission must translate every Command listed below, supply
  each domain payload's required `requestSchemaVersion` at the value the domain
  accepts (today `v1`, mirroring `Pd10V2CandidateCompatibilitySeam`'s root and nested
  `branchRefPolicy` v2-to-v1 rewrite), keep caller idempotency and task context
  distinct from generated message identity, and validate or resolve the domain
  evidence before execution. No handler change is made by 5.12.
- **task-extension-profile — McpCli enrollment and Folders parity setup:**
  McpCli 5.8/6.1 vectors and Folders 5.10/5.11 must configure the selected caller
  profile to allow `taskId` for all 13 Commands. For an empty allowlist, use
  `config set <profile> allowedExtensions taskId`. The setter replaces the entire
  allowlist, so preserve all currently permitted keys in its comma-separated value
  when adding `taskId` to an existing allowlist. The allowlist is empty by default
  and enforced by McpCli before dispatch; it is
  not a Story 5.13 server setting. Task association through extensions is absent
  from the operation payload schema. Story 5.13 validates extension values
  against the domain's lowercase task grammar (G2/G3) and reconciles them with
  payload `TaskId` on the five task-scoped Commands.
- **task-extension-trailing-double-hyphen — platform-wide Gateway limit:**
  v2 task identifiers such as `opaque_task_abc012345678--` satisfy the opaque
  grammar and the domain's lowercase task grammar but are rejected as extension
  values by the injection filter `--\s*$`
  in both McpCli and the EventStore Gateway. This affects every caller, REST
  included, and is a platform limitation rather than an accepted McpCli behavior
  loss. Stories 5.13 and the EventStore/Gateway maintainers own grammar
  reconciliation without silently converting identifiers.
- **provider-binding-reserved-actor — Stories 5.13 and 5.14:** C05 admission
  (5.13), Q13 `GetProviderBinding` and Q35 `ValidateProviderReadiness` handlers
  (5.14) must derive the reserved `ProviderBindingActorId` from the public
  `ProviderBindingRef`, admit only its reserved namespace and reject an actor
  equal to the tenant or any tenant/namespace alias before state access (E5).
- **G4/G5 — Story 5.14:** durable Gateway query handlers, fixed freshness,
  explicit payload/envelope paging precedence with rejection of conflicts,
  task context, metadata-only results and safe errors. `/project` is not a
  query dispatcher. These types do not prove handler registration or execution.
  **read-file-range-window:** Q32 requires `endOffset >= startOffset` and
  `endOffset - startOffset <= 262144`; Story 5.14 enforces these derived rules
  with the spine's 400/422 outcomes before observation. Static member ranges
  cannot reject a reversed or oversized window.
- **tenant-wide-query-aggregate — Story 5.14:** Q16 `GetProviderSupportEvidence`
  declares neither an aggregate accessor nor a constant. McpCli still requires
  an envelope `aggregateId`, so callers must supply this routing input separately
  from the query payload. Story 5.14 selects and validates the authorized
  tenant-wide scope represented by that value. This declaration chooses no
  routing constant and does not treat the caller's aggregate as tenant authority.
- **G6 — Story 5.10:** provider credentials by reference, authorized egress,
  readiness, failure and reconciliation evidence.
- **staged-content-above-inline — McpCli 5.2:** larger content remains deferred;
  retaining the typed spine stream descriptor does not approve a transport or
  demonstrate a configured live content store.

### Command payload translation handoff

The `command-payload-translation` gap covers all thirteen Commands; the payload
names are from `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` (read-only evidence).

| Candidate and declared command | Existing domain payload and translation requirement (Story 5.13) |
| --- | --- |
| C01 ArchiveFolder | `ArchiveFolderPayload`: supply its required schema discriminator and project archive intent, separating routing/key members. |
| C02 BindRepository | `BindRepositoryPayload`: translate the root and nested `BranchRefPolicyPayload`, including both schema discriminators. |
| C03 CommitWorkspace | `CommitWorkspacePayload`: supply the discriminator, reconcile body/header task identity and validate unique audit metadata keys. |
| C04 ConfigureBranchRefPolicy | `BranchRefPolicyPayload`: supply its discriminator and project the policy fields independently of routing/key members. |
| C05 ConfigureProviderBinding | `ConfigureProviderBindingPayload`: supply the discriminator and derive the reserved binding actor before projecting credential-reference metadata. |
| C06 CreateFolder | `CreateFolderPayload`: supply the discriminator and preserve caller FolderId equality with the envelope aggregate. |
| C07 CreateRepositoryBackedFolder | `CreateRepositoryBackedFolderPayload`: resolve required root `RepositoryBindingId` and `CredentialScopeClass`, and supply root/nested discriminators from authorized state and request evidence. |
| C08 GrantFolderAccess | `FolderAccessMutationPayload`: derive grant from the wire type (E2) and translate ACL entry/subject/permission inputs to authorized `operations[{principalKind,principalId,action}]`. |
| C09 LockWorkspace | `LockWorkspacePayload`: supply the discriminator and reconcile required task/lease intent with envelope context. |
| C10 MutateFiles | `FileMutationPayload` has a single-file root, while the declaration is a batch: translate the batch intent and per-file path/content/transport evidence without forwarding the batch unchanged. |
| C11 PrepareWorkspace | `PrepareWorkspacePayload`: supply the discriminator and reconcile task, workspace, repository and policy scope. |
| C12 ReleaseWorkspaceLock | `ReleaseWorkspaceLockPayload`: supply the discriminator and reconcile task/lock ownership proof and release intent. |
| C13 RevokeFolderAccess | `FolderAccessMutationPayload`: derive revoke from the wire type (E2) and translate to authorized `operations[{principalKind,principalId,action}]`. |

### Identifier aggregate grammar gap

**identifier-aggregate-grammar:** String payload values round-trip unchanged,
including legacy values. McpCli aggregate routing accepts 1–256 ASCII
letters/digits/dot/underscore/hyphen and requires alphanumeric first/last
characters. The following accepted payload spellings cannot currently route
as aggregates and are asserted as explicit gaps, without conversion:

| Named gap | Unmodified sample | Source grammar |
| --- | --- | --- |
| aggregate-trailing-underscore | `opaque_FolderAbC012345678_` | v2 opaque identifier |
| aggregate-trailing-hyphen | `opaque_FolderAbC012345678-` | v2 opaque identifier |
| legacy-leading-dot | `.legacy-folder-01` | legacy lowercase identifier |
| legacy-leading-underscore | `_legacy-workspace-01` | legacy lowercase identifier |
| legacy-leading-hyphen | `-legacy-provider-01` | legacy lowercase identifier |
| legacy-trailing-dot | `legacy-folder-01.` | legacy lowercase identifier |
| legacy-trailing-underscore | `legacy-workspace-01_` | legacy lowercase identifier |
| legacy-trailing-hyphen | `legacy-provider-01-` | legacy lowercase identifier |

## Local conformance evidence and limits

The drift gate checks every operation and nested request's serialized field set,
required/nullability flags, string/numeric/array bounds and enum values against
the live v2 spine, using one explicit exemption table. The branch-pattern string
arrays share `DefaultRef`'s identical lexical DataAnnotations; the gate checks
both item schemas against those annotations while preserving JSON string arrays.
Conditional file branches are pinned as a complete schema tree until admission
implements them. Runtime McpCli ignores DataAnnotations, so these declarations
are evidence for structural conformance, not a substitute for validation.

Optional nullable CLR members represent omission when typed serialization omits
null values. Their CLR nullability does not grant permission to send JSON null:
only `FileMutationRequest.byteLength` has an explicit nullable wire declaration.
The drift gate pins that permission independently of required flags and CLR
omission representation. McpCli forwards raw JSON, so Stories 5.13/5.14 must reject
explicit nulls wherever the spine forbids them, preserving the current payload
shapes. Effective canonical JSON bindings, supported `allOf` intersections and
inline-content encoding/decoded byte bounds are checked independently as well.

The package-closure gate packs Release Contracts and checks its actual nuspec,
restored closure, assembly references and framework references. Per-operation
wire/routing tests and opaque/legacy identifier round trips are Folders-owned;
McpCli enrollment owns catalog execution and live vectors.

The PD10 story-owned path list includes the new declarations and tests. The
conformance manifest is intentionally unregenerated under D4. Resealing by
Stories 12.2/1.17 must include the changed SHA-bound rows
`.github/workflows/contract-spine.yml`, `tests/tools/run-contract-spine-gates.ps1`
and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineCiGateTests.cs`,
as well as changed Contracts/test rows and the new paths. PD10 remains red:
the five failing test names match the pre-existing baseline, but their byte-bound
mismatch evidence changes as these rows change. The McpCli selector runs as an
independent CI step so those PD10 failures cannot mask its pass/fail result.
