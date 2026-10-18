---
title: 'Story 12.1: enable current-main authenticated durability acceptance'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
baseline_commit: '539662120f5be2fefefae2576890f1047e1fcd01'
context:
  - 'spec-12-1-current-pin-durable-repository-acceptance.md'
  - 'evidence/story-12-1-durability-2026-10-17-continuation.json'
---

## Intent

Restore the prerequisites for running Story 12.1's complete authenticated durability matrix on the current Folders checkout. The accepted Story 12.1 scope freezes dependency pins, so this spec owns the necessary EventStore integration and task-scoped runner setup. Historical acceptance evidence remains immutable.

## Code Map

- `references/Hexalith.EventStore`: the root-declared EventStore gitlink currently points to `77d9af18a54d92506e310fe5a45021a395e66f60`. Folders owner-read code references later `VerifiedQueryCaller`, `QueryEnvelope.CallerAuthority`, and `QueryCallerAuthorityBinding` APIs.
- `src/Hexalith.Folders.Server/Authentication/VerifiedReceiptEventStoreAuthorizationValidator.cs`, `src/Hexalith.Folders.Server/Queries/FolderOwnerReadQueryHandler.cs`, and their Server tests: verify exact query binding and current authority. Never grant on an unverified envelope.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` and `tests/Hexalith.Folders.Server.Tests/CreateFolderEndpointTests.cs`: interpret a completed replay's original EventStore execution message ID when its cached result still carries the first response's replay flag.
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceTests.cs` and its fixture: existing opt-in authenticated acceptance lane with task-owned Redis and Aspire topology.
- `_bmad-output/implementation-artifacts/evidence/`: retain source-bound commands, results, and cleanup without credentials or protected payloads.

## Tasks & Acceptance

- [x] Inspect the EventStore source revision that supplies the owner-read APIs, its trust-boundary tests, and its compatibility with the current Folders source. Update only the root-declared EventStore gitlink if the Debug/source graph and focused authorization regressions pass. Preserve fail-closed authorization behavior.
- [x] Diagnose inotify use per UID and run the acceptance lane under a dedicated real UID with a private home and mount namespace, using only task-owned runtime resources. Keep unrelated processes and host-wide limits unchanged. If isolation cannot work, present the exact host-wide change and obtain approval before making it.
- [x] Restore and build the current Folders `.slnx` in Debug with source references. Run focused owner-read, authentication, permission projection, replay, and EventStore trust-boundary regressions.
- [x] Assert public create's initial, equivalent, and post-restart replay flags; repair the cached-result response mapping without treating an uncompleted pending submission as a completed replay.
- [x] Execute the complete `FolderDurabilityAcceptanceTests` class with integration enabled. Require zero failures and zero skips; inspect persisted contents, changed-PID restart, zero-checkpoint replay, authorization denials, and dependency failure.
- [x] Record exact Folders and dependency revisions, expanded commands, outcomes, retained hashes, and task-owned cleanup in a new continuation record. Keep the Story 12.1 spec and sprint status in progress until the entire class passes.

**Acceptance criteria**

- Given current Folders owner-read source, when the selected EventStore SDK is built and tested, then all referenced APIs resolve and unverified caller authority is denied.
- Given a task-scoped acceptance runner, when all six sidecars start, then watcher creation succeeds without changing the shared watch limit or interrupting unrelated processes.
- Given a fresh managed tenant and authenticated mutations, when the complete class executes, then persisted state, restart, replay, denials, and dependency failures pass with no failures or skips.

## Verification

The [completion record](evidence/story-12-1-durability-2026-10-18-complete.json) binds Folders `539662120f5be2fefefae2576890f1047e1fcd01` and EventStore `269b20d58236f07228bc2afd54807b94bd96f37b`. The final Debug/source solution build passed with zero warnings/errors. Folders Server (1,008), Workers (147), and focused EventStore query-authority (94) regressions passed with zero failures/skips. The complete opted-in class passed 1/1 with zero failures/skips under a dedicated real UID. Earlier failed candidates remain separate historical evidence.
