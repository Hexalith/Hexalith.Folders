# Epic 5 Context: Cross-Surface Workflow Parity

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Let REST, SDK, CLI, and MCP users run one canonical, tenant-scoped, repository-backed task lifecycle with the same operation identity, authorization, idempotency, errors, audit evidence, and terminal states. Parity is proven by generated oracle rows and shared conformance scenarios, not by side-by-side comparison. Since 2026-11-10 the CLI and MCP surfaces are the generic `Hexalith.McpCli` heads, not Folders binaries. Folders declares its operations as decorated Contracts, enforces REST-equivalent admission and authorization for Gateway-dispatched operations, serves reads through Gateway query handlers, proves the enrolled lifecycle through both heads, and then retires `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp`. MVP release depends on McpCli enrolling the Folders Contracts.

## Stories

- Story 5.1: Ship SDK convenience helpers, samples, and quickstart
- Story 5.2: CLI tenant, folder, provider-readiness, and binding commands
- Story 5.3: MCP tenant, folder, provider-readiness, and binding tools/resources
- Story 5.4: Consume parity oracle in CLI and MCP tests
- Story 5.5: Validate golden lifecycle parity across REST and SDK
- Story 5.6: Validate behavioral parity across CLI and MCP
- Story 5.7: Validate mixed-surface handoff scenario
- Story 5.8: Enroll workspace preparation and lock lifecycle through McpCli
- Story 5.9: Enroll file, context, commit, status, error, and audit operations through McpCli
- Story 5.10: Enroll tenant, folder, ACL, provider-readiness, and binding operations through McpCli
- Story 5.11: Prove four-surface parity and mixed-surface handoff through McpCli
- Story 5.12: Declare Folders operations in decorated McpCli Contracts
- Story 5.13: Admit and authorize Gateway-dispatched Folders commands
- Story 5.14: Serve Folders reads through Gateway query handlers
- Story 5.15: Retire Hexalith.Folders.Cli and Hexalith.Folders.Mcp

## Requirements & Constraints

- **Surface coverage.** CLI and MCP cells count only when served by a McpCli head, for an operation whose owner-approved Folders inventory row is `replace`/`include` with a named decorated Contracts type. Core-lifecycle and tenant-administration operations are required on all four surfaces. An operation McpCli cannot carry needs an inventory `withdraw` or `defer` plus a product-approved C13 declaration. There is never a legacy-binary fallback, so deferring a core operation blocks MVP. Only a diagnostic or read-model operation may be marked not applicable for CLI/MCP, and only by explicit C13 declaration.
- **Frozen legacy surfaces.** `Hexalith.Folders.Cli`, `Hexalith.Folders.Mcp`, and the shared adapter core get safety and continuity fixes only, including the Story 1.17 v2 migration. They are never templates, and their evidence never satisfies an Epic 5 story after 2026-11-10. Do not add a Folders CLI, a Folders MCP host, or a Folders-specific McpCli adapter.
- **Idempotency.** Every mutation needs an idempotency key, and reads reject the key before source access. Live equivalent replay returns the same logical result after current authorization is revalidated. Live changed intent returns `idempotency_conflict`. An expired key returns `idempotency_key_expired` whatever the intent (HTTP 409, non-retryable, `refresh_state_then_submit_with_new_key`) and never runs as new work. The caller's key stays independent of McpCli's generated message ID.
- **Errors.** Every error carries category, code, safe message, correlation ID, optional task ID, retryability, client action, visibility, and closed metadata-only details.
- **v2 authorization outcomes.** Checks run in this order: authentication, authority availability, authority, and only then any protected lookup. Unauthenticated callers get 401. A fresh negative fact gets one byte-identical safe-denial 404. Stale, unavailable, conflicting, or incomplete authority gets a retryable 503. Protected responses never expose 403, `not_found`, `cross_tenant_access_denied`, or `audit_access_denied`.
- **Metadata only.** Results and errors never carry content bytes, inline content, stream descriptors, secrets, or unauthorized paths. Provider binding takes credential references only.
- **Content bounds.** Inline content is proven up to the inclusive 262144-byte decoded boundary. Larger content needs the staging contract approved by McpCli Story 5.2, or it stays an inventory `defer`.
- **Evidence.** Completion requires real deployed composition with asserted state-store end-state and the negative paths (denial, wrong tenant, replay, conflict, expiry, unknown outcome, boundaries). Fake, mock, NoOp, seed, in-memory, unavailable, or safe-empty paths cannot prove completion. Parity evidence binds the exact McpCli version, Folders Contracts package version, Catalog digest, environment, and vectors by hash, and must pass the secret scan.

## Technical Decisions

- **Topology (A-12).** McpCli heads dispatch decorated `Hexalith.Folders.Contracts` operations to the EventStore Gateway, which routes them to the Folders domain service. They do not wrap the SDK or REST, so Gateway dispatch bypasses Folders REST authorization. The domain service must enforce authority, layered folder/action ACL, revocation, task/workspace/lease ownership, deny-before-access, A-9 idempotency, and metadata-only output exactly as REST does. Envelope tenant and actor values are comparison inputs only, never authority.
- **Contracts package.** It holds the OpenAPI 3.1 Contract Spine, DTOs, and the decorated types, with no behavior and no `Hexalith.EventStore` reference. Its only package reference is `Hexalith.McpCli.Abstractions`: versionless, with the version from Builds `HexalithMcpCliVersion` (2.0.1 at the 2026-11-10 baseline). It declares `[assembly: HexalithModule("folders", …)]`.
- **Decorated type shape.**
  - Each type is public, one type per file, under `Hexalith.Folders.Contracts.Commands` or `.Queries`.
  - Each has a nonblank description, `Domain = "folders"`, aggregate routing, and `[HexalithIdentifier]` properties.
  - The chosen identifier kind must round-trip existing folder, workspace, task, and provider-binding IDs without silent conversion. Any format it cannot accept is a named gap.
- **Wire names.** Each Command maps explicitly to its existing `Hexalith.Folders.Commands.*` wire name, never a derived `Contracts.Commands.*` name, and a test pins each mapping.
- **Query dispatch.** Each Query is served by a registered `IDomainQueryHandler` from durable projections. `/project` is never a query dispatcher. Each operation applies a single freshness value. Payload-versus-envelope paging precedence must be defined, and conflicting or unsupported combinations are rejected, not silently dropped.
- **Gates.**
  - A Release build with TreatWarningsAsErrors must report no McpCli `MCPCLI001` diagnostic.
  - The packed Contracts closure must pass McpCli's AD-15 package/closure gate. McpCli enrollment also applies its AD-21 Catalog set/type gates.
  - A drift gate fails when a decorated type's fields, required flags, or bounds diverge from the Contract Spine operation it maps to.
- **Ownership split.** McpCli owns exit codes, MCP result shapes, error documents, and profiles. Folders owns operation semantics and the C13 mapping from each canonical category, code, retryable flag, and client action to the McpCli error document.
- **C13 columns.** Each C13 row adds `mcpcli_operation`: a canonical name such as `folders.create-folder`, the decorated CLR type, and the inventory disposition. The legacy `cli_exit_code` and `mcp_failure_kind` columns stay until Story 5.15.
- **v2 Contract Spine.** The v2 Contract Spine (`/api/v2`) is the only target; v1 is historical. Cell values come from the generated v2 authorization matrix and C13 oracle, not from prose. `GetReadinessDiagnostics`, `GetProjectionFreshness`, and `GetTaskStatus` take an explicit `folderId` scope. Provider, repository, ref, and task scope dimensions are derived from already-authorized resources, never supplied by the caller.
- **McpCli Folders inventory 1.0.0.** It lives at `references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md`. The planning artifacts cite these IDs:
  - Candidates C01–C13 and Q01–Q35, each needing an owner agent-eligibility decision.
  - Folders-owned gaps: G1 decorated Contracts (5.12), G2 `/process` admission and G3 Gateway authorization (5.13), G4 query handlers and G5 metadata-only output (5.14), and G6 provider egress (5.10).
  - Behavior losses Story 5.12 must decide and record for McpCli Story 5.8: V04 local-file input, V07 `--allow-auto-key`, V09 credential precedence, and the R01/R02 resources.
  - Other variants: V01 freshness and V02 paging/filter inputs; V04/V05 `PutFileStream`, staged content above 262144 bytes; V08, where grant and revoke stay distinct operations; and C05 with provider binding. V03 and V06 are cited by Stories 5.13 and 5.14 without further detail.

## Cross-Story Dependencies

- **Done stories.** Stories 5.1–5.7 are done and kept as historical compatibility evidence. Do not rewrite them.
- **Execution ranks.** Every new or re-scoped row is `backlog` and execution-held behind DEC-A8-HOLD:

  | Story or node | Rank | Prerequisites |
  | --- | --- | --- |
  | 5.12 | 11 | Story 1.17 (the PD10 v2 Contract Spine, still backlog) |
  | 5.13 | 22 | 5.12, 11.10, 12.1, 12.6 |
  | 5.14 | 23 | 5.12, 5.13, 12.2, 12.3 |
  | EXT-MCPCLI-FOLDERS-ENROLLMENT | 24 | 5.12–5.14 |
  | 5.8 | 42 | 4.19 and the enrollment node |
  | 5.10 | 42 | 12.1 and the enrollment node |
  | 5.9 | 43 | 4.20, 4.21, 12.6 and the enrollment node |
  | 5.11 | 44 | 5.8–5.10 |
  | EXT-MCPCLI-FOLDERS-RETIREMENT-GATE | 45 | 5.11 |
  | 5.15 | 46 | 5.11 and the retirement gate |

- **Enrollment node (external, McpCli-owned).** It is satisfied by McpCli Stories 5.2 and 5.8 plus a requested Folders enrollment story. It requires three things:
  - an owner-approved inventory version that names a decorated type for every `replace` row;
  - a McpCli release that enrolls the exact Folders Contracts package version;
  - one approved execution vector per included operation, with loopback request/result parity.
- **5.12 feeds McpCli.** McpCli Story 5.8 re-dispositions the Folders inventory from 5.12's eligibility decisions and named types. McpCli counts a candidate as a replacement only once its type is named.
- **Redirected action item.** HXF-MCP-001 now goes to McpCli for annotations and structured output, to 5.12 for typed request bodies, and to 5.14 for authorized content read-back within C4 bounds, or an inventory `defer`.
- **Story 5.15.** It removes the legacy projects, the shared adapter core, `.slnx`/NuGet/CI entries, and the pinned inventory gates in one lockstep change. Story 11.6 stays safety work on the frozen adapters.
