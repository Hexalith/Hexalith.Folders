# Story 12.3 final review qualification

Story 12.3 is implemented and independently reviewed. This snapshot supersedes changed-source claims in
[the original evidence](../README.md) and [the parent-audit snapshot](../audit-fixes/README.md), which remain historical.
Three independent review lenses produced 18 findings; parent follow-through added two. The spec records
all 20 individual verdicts: 19 patched, one cursor claim refuted. No deferred or unresolved finding remains.

The Debug/source solution restore and current build passed with zero warnings/errors. Parent gates ran
sequentially; source changes never overlapped the live class. A first Contracts run found a cache-lint
violation in one new test fixture. That test now uses the strong byte-state/ETag path, asserts the exact
tenant-scoped receipt key, and inspects persisted metadata and bytes. The current solution build, full
Server and full Contracts lanes were rerun. Production sources remained unchanged throughout; the final
post-correction source interval is unchanged across all retained paths.

| Current test lane | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| sdk-protocol | 19 | 0 | 0 |
| sdk-persistence | 1 | 0 | 0 |
| sdk-helper | 49 | 0 | 0 |
| core | 2,218 | 0 | 2 |
| client | 342 | 0 | 0 |
| server | 1,100 | 0 | 0 |
| contracts | 360 | 0 | 0 |
| eventstore-host | 66 | 0 | 0 |
| cli | 31 | 0 | 0 |
| mcp | 15 | 0 | 0 |
| process-wiring | 1 | 0 | 0 |
| live | 1 | 0 | 0 |

Across these lanes, 4,203 passed and 2 pre-existing optional Forgejo operator cases were skipped.
All Story 12.3 acceptance checks executed without skips. No solution-level test command was used.

The real live class passed in 248.730 seconds. Both actual Folders serving processes and the
worker/EventStore owners were replaced. 141 retained runtime records cover
30 distinct DLL paths and 30 PDB paths; every hash matches
its final build output. Distinct destinations were `http://localhost:45811/` and
`http://localhost:38083/`. The contention returned 202/409 with exactly one persisted version-1
winner of 25 bytes. The retained domain rejection code is
`AppendConflict`; historical AppendConflict evidence is separate.

Live checks verify exact inline/multipart bytes after restart, missing/corrupt/paused storage, removal
suppression, hold/release and resumed-work P7D resets, actor-authorized physical byte erasure, and atomic
multi-file publication. Erasure retains a byte-free exact-key metadata tombstone that prevents delayed
create-only uploads from recreating content. SDK protocol and real Redis/Dapr tests additionally qualify
empty files and recovery after the tombstone is stored but before the completion marker.

New focused regressions cover committed-body retention after staged replacement/removal, expiry/closed-task
replay without resurrection, actual multipart replay hashing, current authority before 416, final single/batch
lease checks, sensitivity denial/unavailability before writes, malformed JSON, production multi-file search/glob,
canonical typed wire responses, and legacy optional-version roundtrips with deterministic generation.

[qualification.json](qualification.json) records exact commands, full-log digests, results, matrix coverage,
review resolutions, source stability and limitations. [source-inventory.json](source-inventory.json) binds
138 current source/configuration/support paths; the upstream
[final qualification](../../../../references/Hexalith.EventStore/docs/evidence/story-12-3-content-authority/review-fixes/qualification.json)
binds 41 paths. [live-runtime-bindings.jsonl](live-runtime-bindings.jsonl),
[live-assertions.log](live-assertions.log), and [attempt-history.json](attempt-history.json) retain metadata only.
Historical command text contained a dotted Integration.Tests path typo; the actual project and assembly
are `Hexalith.Folders.IntegrationTests`, as used by this snapshot.

Frozen approved intent, OQ2 1.1.0 and the immutable historical v1 baseline are unchanged. All 229 disabled-v2
pending inventory entries match their source hashes; public v2 remains disabled. Production requires an
explicit finite non-confidential policy. Unsupported confidential cleartext execution remains denied pending
Story 12.7. Legacy descriptor-only events do not manufacture readable body authority. Qualification uses
uncommitted EventStore source; Release/package consumption and publication require a separate upstream release.
No deployment, dependency upgrade, staging, commit or push occurred.
