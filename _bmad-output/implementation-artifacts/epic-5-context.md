# Epic 5 Context: Cross-Surface Workflow Parity

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Let REST, SDK, CLI, and MCP users run one canonical tenant-scoped task lifecycle with equivalent operation identity, authorization, idempotency, errors, audit evidence, terminal states, and mixed-surface handoff. Generated C13 oracle rows and shared conformance scenarios prove parity. Since the approved November 10, 2026 correction, only the Hexalith.McpCli CLI and MCP heads qualify for MVP CLI/MCP cells; release depends on enrolling the Folders Contracts.

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

Core lifecycle and tenant administration require all four surfaces. CLI/MCP cells require an owner-approved inventory `replace`/`include` row naming a decorated type. Unsupported operations need an inventory `withdraw`/`defer` decision plus a product-approved C13 declaration; deferring a core operation blocks MVP. Legacy binaries never supply fallback evidence. Preserve completed historical compatibility evidence.

Every mutation requires an opaque caller idempotency key; reads reject keys before source access. Authorization and canonical validation precede durable admission. Current authorization is revalidated before equivalent replay returns the original logical result. Changed live intent conflicts. Equivalent or changed reuse after expiry returns `idempotency_key_expired`, non-retryable, with `refresh_state_then_submit_with_new_key`; it never executes as new work. Generated message identity stays independent of the caller key. No duplicate events or external effects are permitted.

Authentication, authority availability, authority, then protected access is the required order. Fresh negative authority yields the canonical safe-denial 404; unavailable, stale, conflicting, or incomplete authority yields retryable 503. Envelope actor and tenant values are comparison inputs, never trusted authority. Enforce layered ACL, revocation, task association, and workspace/lease ownership before access or replay.

Keep workspace lifecycle, lock state, operation status, cleanup status, and operator disposition separate. Lock states are exactly `unlocked`, `locked`, `expired`, `stale`, `revoked`. One writer serializes on managed tenant plus canonical provider/repository identity plus normalized ref; aliases collide. C7 governs renewal, authorization revalidation, revocation effect, and expired-to-stale timing. Unconfirmed effects enter `unknown_provider_outcome` with bounded read-only confirmation; exhausted/conflicting evidence enters `reconciliation_required`, blocking blind retry, mutation, and takeover.

Results, errors, events, logs, and audit evidence obey metadata-only and disclosure rules: no content bytes, inline content, stream descriptors, secrets, or unauthorized paths. Credential configuration accepts references only. Content mutation supports the inclusive 262144-byte decoded inline boundary; larger content needs the approved generic staging contract or an explicit deferral.

Completion requires authenticated real deployed composition with persisted state-store end-state assertions for success and negative boundaries: wrong tenant/denial, replay/conflict/expiry, collision, provider failure, timeout/unknown, and invalid inputs. Fake, NoOp, mock-only, seed-only, safe-empty, unavailable, in-memory, or legacy-head results cannot prove completion. Evidence binds exact McpCli and Contracts versions, Catalog digest, environment, and execution vectors by hash and passes the secret scan.

## Technical Decisions

McpCli heads dispatch decorated Contracts through the EventStore Gateway to the Folders domain service; they do not wrap Folders SDK/REST. Shared domain enforcement must therefore supply REST-equivalent authorization, idempotency, lifecycle, and output behavior. Do not add a module-specific CLI, MCP host, or McpCli adapter. Frozen Folders adapters receive safety and continuity fixes only until authorized retirement.

Contracts owns the OpenAPI 3.1 v2 Spine, DTOs, and decorated public Command/Query types without behavior or an EventStore dependency. Its sole package dependency is versionless Hexalith.McpCli.Abstractions, versioned by the Builds catalog. Declare the module, domain, aggregate routing, identifier properties, and descriptions; prove identifier round-trip without conversion. Commands explicitly retain existing Hexalith.Folders.Commands wire names. A drift gate pins fields, required flags, bounds, and mappings against the Spine. Contracts packaging must satisfy MCPCLI001 and AD-15/AD-21 enrollment gates.

Queries use registered IDomainQueryHandler implementations and durable projections; `/project` is never a query dispatcher. Preserve each operation's freshness and paging/filter behavior with explicit payload/envelope precedence and reject conflicts. McpCli owns exit codes, error documents, profiles, and MCP result shapes. Folders maps canonical category/code/retryable/clientAction into those documents. C13 adds `mcpcli_operation` with canonical name, decorated CLR type, and inventory disposition; legacy adapter columns remain until retirement.

## Cross-Story Dependencies

Stories 5.12–5.14 supply the Folders enrollment foundation: decorated Contracts, command admission/authorization, and query handling. Their prerequisites include the v2 Spine (1.17), shared enforcement (11.10), and durable persistence/idempotency/projections/content (12.1–12.3, 12.6).

EXT-MCPCLI-FOLDERS-ENROLLMENT additionally requires McpCli-owned inventory re-disposition and generic capability decisions, an exact Contracts package enrolled in a McpCli release passing closure/Catalog gates, and approved execution vectors with loopback request/result parity. Folders cannot infer external acceptance from local types or tests.

Story 5.8 requires deployed durable prepare/lock (4.19) and enrollment. Story 5.9 requires 4.20, 4.21, 12.6 and enrollment; 5.10 requires 12.1 and enrollment. Story 5.11 follows 5.8–5.10. Story 5.15 follows 5.11 and EXT-MCPCLI-FOLDERS-RETIREMENT-GATE, requiring owner-approved replacement/withdrawal of every legacy operation/variant before lockstep removal of legacy source, packages, CI, and documentation.
