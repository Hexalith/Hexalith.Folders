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
- **E2:** C08 and C13 map to `UpdateFolderAclEntry`, with an immutable serialized
  `effect` of `grant` and `revoke`, respectively (V08). Each publicly read-only
  property has a private JSON initializer that preserves its fixed value under
  McpCli's canonical resolver. McpCli forwards caller JSON without constructing
  these records: Story 5.13 must derive/enforce the fixed effect from the operation
  identity before domain execution. A typed initializer does not validate that
  forwarded JSON, including omitted or conflicting effect values.
- **E3:** C10 maps to the `MutateFilesRequest` component and uses AddFile's path
  and task scope, preserving the canonical batch shape.
- **E4:** C06 adds caller-supplied `FolderId`; the domain requires
  `payload.FolderId == envelope.AggregateId`.
- **E5:** C05 routes on `ProviderBindingRef`. Story 5.13 must derive the reserved
  `ProviderBindingActorId` and reject an actor equal to the tenant before lookup.

| ID | Owner eligibility | CLR type | Explicit wire name | Spine operation/component | Aggregate source | Exemptions | Fixed freshness |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C01 | eligible | `Hexalith.Folders.Contracts.Commands.ArchiveFolder` | `Hexalith.Folders.Commands.ArchiveFolder` | `ArchiveFolder` | `FolderId` | E1 | — |
| C02 | eligible | `Hexalith.Folders.Contracts.Commands.BindRepository` | `Hexalith.Folders.Commands.BindRepository` | `BindRepository` | `FolderId` | E1 | — |
| C03 | eligible | `Hexalith.Folders.Contracts.Commands.CommitWorkspace` | `Hexalith.Folders.Commands.CommitWorkspace` | `CommitWorkspace` | `FolderId` | E1 | — |
| C04 | eligible | `Hexalith.Folders.Contracts.Commands.ConfigureBranchRefPolicy` | `Hexalith.Folders.Commands.ConfigureBranchRefPolicy` | `ConfigureBranchRefPolicy` | `FolderId` | E1 | — |
| C05 | eligible | `Hexalith.Folders.Contracts.Commands.ConfigureProviderBinding` | `Hexalith.Folders.Commands.ConfigureProviderBinding` | `ConfigureProviderBinding` | `ProviderBindingRef` | E1, E5 | — |
| C06 | eligible | `Hexalith.Folders.Contracts.Commands.CreateFolder` | `Hexalith.Folders.Commands.CreateFolder` | `CreateFolder` | `FolderId` | E1, E4 | — |
| C07 | eligible | `Hexalith.Folders.Contracts.Commands.CreateRepositoryBackedFolder` | `Hexalith.Folders.Commands.CreateRepositoryBackedFolder` | `CreateRepositoryBackedFolder` | `FolderId` | E1 | — |
| C08 | eligible | `Hexalith.Folders.Contracts.Commands.GrantFolderAccess` | `Hexalith.Folders.Commands.GrantFolderAccess` | `UpdateFolderAclEntry` | `FolderId` | E1, E2 | — |
| C09 | eligible | `Hexalith.Folders.Contracts.Commands.LockWorkspace` | `Hexalith.Folders.Commands.LockWorkspace` | `LockWorkspace` | `FolderId` | E1 | — |
| C10 | eligible | `Hexalith.Folders.Contracts.Commands.MutateFiles` | `Hexalith.Folders.Commands.MutateFiles` | `MutateFilesRequest` | `FolderId` | E1, E3 | — |
| C11 | eligible | `Hexalith.Folders.Contracts.Commands.PrepareWorkspace` | `Hexalith.Folders.Commands.PrepareWorkspace` | `PrepareWorkspace` | `FolderId` | E1 | — |
| C12 | eligible | `Hexalith.Folders.Contracts.Commands.ReleaseWorkspaceLock` | `Hexalith.Folders.Commands.ReleaseWorkspaceLock` | `ReleaseWorkspaceLock` | `FolderId` | E1 | — |
| C13 | eligible | `Hexalith.Folders.Contracts.Commands.RevokeFolderAccess` | `Hexalith.Folders.Commands.RevokeFolderAccess` | `UpdateFolderAclEntry` | `FolderId` | E1, E2 | — |
| Q01 | eligible | `Hexalith.Folders.Contracts.Queries.GetAuditRecord` | `Hexalith.Folders.Queries.GetAuditRecord` | `GetAuditRecord` | `FolderId` | — | eventually_consistent |
| Q02 | eligible | `Hexalith.Folders.Contracts.Queries.GetBranchRefPolicy` | `Hexalith.Folders.Queries.GetBranchRefPolicy` | `GetBranchRefPolicy` | `FolderId` | — | eventually_consistent |
| Q03 | eligible | `Hexalith.Folders.Contracts.Queries.GetCommitEvidence` | `Hexalith.Folders.Queries.GetCommitEvidence` | `GetCommitEvidence` | `FolderId` | — | eventually_consistent |
| Q04 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetDirtyStateDiagnostics` | — (undecorated) | `GetDirtyStateDiagnostics` | `none` | — | eventually_consistent |
| Q05 | eligible | `Hexalith.Folders.Contracts.Queries.GetEffectivePermissions` | `Hexalith.Folders.Queries.GetEffectivePermissions` | `GetEffectivePermissions` | `FolderId` | — | read_your_writes |
| Q06 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetFailedOperationDiagnostics` | — (undecorated) | `GetFailedOperationDiagnostics` | `none` | — | eventually_consistent |
| Q07 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderFileMetadata` | `Hexalith.Folders.Queries.GetFolderFileMetadata` | `GetFolderFileMetadata` | `FolderId` | E1 | snapshot_per_task |
| Q08 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderIndexingStatus` | `Hexalith.Folders.Queries.GetFolderIndexingStatus` | `GetFolderIndexingStatus` | `FolderId` | — | eventually_consistent |
| Q09 | eligible | `Hexalith.Folders.Contracts.Queries.GetFolderLifecycleStatus` | `Hexalith.Folders.Queries.GetFolderLifecycleStatus` | `GetFolderLifecycleStatus` | `FolderId` | — | eventually_consistent |
| Q10 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetLockDiagnostics` | — (undecorated) | `GetLockDiagnostics` | `none` | — | eventually_consistent |
| Q11 | eligible | `Hexalith.Folders.Contracts.Queries.GetOperationTimelineEntry` | `Hexalith.Folders.Queries.GetOperationTimelineEntry` | `GetOperationTimelineEntry` | `FolderId` | — | eventually_consistent |
| Q12 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetProjectionFreshness` | — (undecorated) | `GetProjectionFreshness` | `none` | — | eventually_consistent |
| Q13 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderBinding` | `Hexalith.Folders.Queries.GetProviderBinding` | `GetProviderBinding` | `ProviderBindingRef` | — | eventually_consistent |
| Q14 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderOutcome` | `Hexalith.Folders.Queries.GetProviderOutcome` | `GetProviderOutcome` | `FolderId` | — | eventually_consistent |
| Q15 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetProviderStatusDiagnostics` | — (undecorated) | `GetProviderStatusDiagnostics` | `none` | — | eventually_consistent |
| Q16 | eligible | `Hexalith.Folders.Contracts.Queries.GetProviderSupportEvidence` | `Hexalith.Folders.Queries.GetProviderSupportEvidence` | `GetProviderSupportEvidence` | `none` | — | eventually_consistent |
| Q17 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetReadinessDiagnostics` | — (undecorated) | `GetReadinessDiagnostics` | `none` | — | eventually_consistent |
| Q18 | eligible | `Hexalith.Folders.Contracts.Queries.GetReconciliationStatus` | `Hexalith.Folders.Queries.GetReconciliationStatus` | `GetReconciliationStatus` | `FolderId` | — | eventually_consistent |
| Q19 | eligible | `Hexalith.Folders.Contracts.Queries.GetRepositoryBinding` | `Hexalith.Folders.Queries.GetRepositoryBinding` | `GetRepositoryBinding` | `FolderId` | — | eventually_consistent |
| Q20 | excluded / defer: seed-backed operations_console_projection diagnostic | `Hexalith.Folders.Contracts.Queries.GetSyncStatusDiagnostics` | — (undecorated) | `GetSyncStatusDiagnostics` | `none` | — | eventually_consistent |
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
  without lowercasing opaque payload IDs. Request-schema version is admission
  state. Conditional file transport/add/change/remove combinations, duplicate
  detection and cross-field byte limits remain admission rules.
- **provider-binding-reserved-actor — Story 5.13:** C05 must derive the reserved
  binding actor from its public reference and reject tenant/namespace aliasing
  before any state access (E5).
- **G4/G5 — Story 5.14:** durable Gateway query handlers, fixed freshness,
  explicit payload/envelope paging precedence with rejection of conflicts,
  task context, metadata-only results and safe errors. `/project` is not a
  query dispatcher. These types do not prove handler registration or execution.
- **G6 — Story 5.10:** provider credentials by reference, authorized egress,
  readiness, failure and reconciliation evidence.
- **staged-content-above-inline — McpCli 5.2:** larger content remains deferred;
  retaining the typed spine stream descriptor does not approve a transport or
  demonstrate a configured live content store.
- **identifier-aggregate-grammar:** String payload values round-trip unchanged,
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

Only the PD10 story-owned path list is extended. The conformance manifest is
intentionally unregenerated under D4. Existing stale rows and new paths leave
PD10 gates red until Stories 12.2/1.17 reseal and qualify the changed set.
