---
title: Story 12.2 bounded owner-identity prerequisite
status: approved-routed-to-eventstore
approved: 2026-10-19
created: 2026-10-19
parent_spec: spec-12-2-durable-projections-and-task-completion-pipeline.md
---

# Observed blocker

The existing SDK can deliver position 10,001 and the domain can plan its complete rebuild with bounded logical checkpoint, receipt and view values. The real named/shared dispatcher cannot qualify that complete history. The regression in `OperationalStatusCapacityTests.TenThousandPositionsAndNinetyEightHistoricalTasksRebuildAndCatchUpWithBoundedValues` preserves the successful 10,000-position rebuild, ordinary growth to 10,001, and the subsequent unavailable production-dispatch result. It separately calls the public `SharedProjectionEpochCoordinator.SeedCapturedOwnerSequenceAsync` with position 10,001 and an exactly sized 320,032-byte identity vector. The SDK throws `ArgumentException`, parameter `capturedEventDigests`, stating that captured identities must cover the complete owner history.

At EventStore HEAD `84bb115a0ced2497ca4315644a677b273cb44edf`, both `SeedCapturedOwnerSequenceAsync` and `CanPromoteCapturedJournalOwnerAsync` explicitly reject captured histories over 10,000 positions, independently of configured dispatch bounds. `DomainSharedProjectionRebuildDispatcher` retains and hands off one captured identity vector per source owner. Existing partitioned/streaming interfaces bound output mutations; they do not provide a bounded captured-identity handoff for one growing source owner. Raising transport options does not supply that capability.

# Proposed generic prerequisite

Authorize the EventStore owner to design and implement SDK-owned bounded captured-identity retention and checkpoint handoff for an arbitrarily growing original source owner through the existing named/shared rebuild transport. The owner must select any required API, protocol and storage-schema changes explicitly. Folders must continue to supply its original source stream, positions and identities, and consume the resulting public SDK seam without a local owner-partition or persistence protocol.

Preserve complete duplicate/conflicting-position and message identity evidence, valid captured-prefix comparisons, source continuity, current-head completeness, CAS fencing, selected-generation reads, journal catch-up and recoverable promotion. Keep the recoverable 100-operation/one-MiB batch limits. Do not impose a domain history cap, omit captured identities, discard historical required views, rewrite retained events or relax contradiction checks.

Qualification must cover ordinary growth beyond 10,000, complete current-source rebuild at 10,001 and beyond, exact duplicate proof, conflicting earlier identities, subsequent growth, multiple independent owners, concurrent writers, inherited generations, interrupted staging/promotion and process replacement. Run the public production dispatcher, not only coordinator unit tests. Bind evidence to exact SDK source and consumer binaries; package/release qualification and any later pin change remain separate authorized work.

# Current disposition

This proposal records no new approval and authorizes no SDK protocol/schema/public-surface changes, publication, dependency update, deployment, commit or push. Independently authorized Story 12.2 corrections proceed. Story 12.2 acceptance remains open until this generic prerequisite is approved, implemented and qualified.

# Approval (2026-10-19)

Jerome approved this generic prerequisite as separate EventStore-owned work, decided in the Story 12.2 `bmad-build` session. It follows the ratified one-story-per-repository model. The SDK capability is designed, implemented and qualified in an EventStore repository story and session, not from a Folders session. The EventStore owner selects any API, protocol or storage-schema change explicitly, within the constraints above.

Story 12.2 Task 36 and full Story 12.2 acceptance stay open until that work lands, is released or otherwise qualified for consumption, and Folders requalifies against it. This approval authorizes no Folders-side workaround, no domain history cap, no publication, no dependency or pin update and no push.

Separately, the four minimal-mapper SDK changes consumed by Story 12.2 exist only as the unpushed local EventStore commit `d4d05f871991ec2af305267f59dcefeae3f127ed`. On 2026-10-19 that branch was 1 commit ahead of and 20 behind `origin/main`, and the root gitlink still recorded `84bb115a0ced2497ca4315644a677b273cb44edf`. Rebasing, releasing and pinning those changes remains separately authorized work.
