# Story 12.3 qualification

Current independent-review fixes and final parent verification are qualified in
[review-fixes/README.md](review-fixes/README.md). The original implementation snapshot below and
[audit-fixes/README.md](audit-fixes/README.md) remain historical. The changes remain uncommitted. Root baseline is
`28513763ec5c0405ab942b1e5266a483573f451d`; the EventStore source baseline is
`995117f9b899a2994e99257091b0ea35cfbe5090`. No dependency pin changed.

[qualification.json](qualification.json) records exact commands, results, full-log locations/digests,
runtime identities and limitations. [source-inventory.json](source-inventory.json) binds the changed source
and supporting artifacts. Retained logs contain metadata only; they include no uploaded body bytes or tokens.

The Aspire lifecycle baseline preceded code edits. EventStore's generic capability was implemented and
qualified before dependent Folders implementation. Its [prerequisite evidence](../../../references/Hexalith.EventStore/docs/evidence/story-12-3-content-authority/qualification.json)
records 11 protocol tests and one real Redis/Dapr test, zero failures/skips, replaced sidecar PIDs
60616→60946, replacement client/store objects and persisted-byte integrity checks. All 33 upstream qualified
source/helper hashes matched at that historical snapshot. Some earlier exploratory lanes overlapped;
the final gates ran sequentially.

| Final lane | Result |
| --- | --- |
| Core | 2,218 total; 0 failed; 2 existing Forgejo operator skips |
| Server | 1,066 passed; 0 failed/skipped |
| Client | 336 passed; 0 failed/skipped |
| Contracts/generators/parity | 360 passed; 0 failed/skipped |
| Folders EventStore host adapters | 66 passed; 0 failed/skipped |
| In-process content wiring | 1 passed; 0 failed/skipped |
| Erasure grant/rejection mapping | 9 passed; 0 failed/skipped |
| NFR gate regression | 17 passed; 0 failed/skipped |
| Opted-in live content class | 1 passed; 0 failed/skipped; 241.744 seconds |
| Full solution restore/build | Debug/source, 60-project graph; 0 warnings/errors; build 24.29 seconds |

The broad build used the established source, MinVer and audit environment pins. A subsequent live run
qualified its actual binaries: every one of the 30 distinct loaded module paths matched that historical output
hash. There was no solution-level test run or Release/package qualification.

| Approved matrix row | Concrete qualification |
| --- | --- |
| Stage/read | Client stream boundaries 0/262144/262145/1048576; real inline and multipart ingress; exact Redis body and range reads before/after host replacement |
| Replay/race | Equivalent replay leaves one mutation; stale version/lock conflicts; two distinct replacement Folders processes contend; one immutable persisted winner |
| Delete/retention | Real removal suppresses reads immediately; hold preserves bytes; terminal orphan, hold release and resumed work reset the P7D epoch; actor grant precedes actual Redis body deletion |
| Policy | Immutable policy/glob/classification unit tests; current authorization, hidden/missing/foreign denial, aliases/traversal and unchanged HTTP bounds in Core/Server suites |
| Bytes | Strict UTF-8/BOM/control and size tests; exact binary mutation bytes remain preserved and unreadable; client cancellation, hash mismatch, oversize and caller stream ownership tests |
| Fault | SDK bounded cancellation/non-cooperative stream tests; live missing/corrupt bodies and paused persistence return unavailable and recover exact bytes |
| Atomic change set | Distinct same-command source bindings; real two-receipt batch publication; invalid later item leaves both previous versions unchanged |

The in-process test uses the repository's shared rejection-propagating `/process` gateway harness, actual
processor/gates/content SDK, and a CAS persistence test double. Its source-authority seam is explicit; the
separate live class uses production authority, authenticated gateway/actor callbacks and physical Redis.
P7D qualification advances only the SDK disposition clock through an opted-in disposable startup hook;
source, JWT and lock clocks remain real.

The final race used managed serving PIDs 3742165 and 3746273, reached through distinct HTTP destinations
`http://localhost:44345/` and `http://localhost:43989/`. Their earlier serving PIDs were 3723043 and 3723091;
both processes were replaced. Launcher/managed mappings, DLL hashes, MVIDs and PDB hashes are retained in
[live-runtime-bindings.jsonl](live-runtime-bindings.jsonl).

The retained loser code is `AppendConflict`, mapped specifically to HTTP 409 `concurrency_conflict`.
Focused regressions also preserve unrelated validation/denial outcomes. The race returned 202/409 and
retained exactly one winning version, explicitly asserted as version 1, with 25 persisted bytes and SHA-256
`5658e777bdddcb895572e4603742e6a540ee0287dc6d81e5acc8901e9e1923de`.
[live-assertions.log](live-assertions.log) records its receipt and the physical-body verification marker.

The OQ2 1.1.0 artifact remains SHA-256
`b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`.
The immutable historical v1 baseline remains
`3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`.
Active v1 supports actual multipart bytes and additive observed fences/versions. Generated consumer models,
disabled v2 candidate artifacts and the pending source manifest were regenerated. Public v2 remains disabled;
the 49 operation identities/status contracts and historical approval evidence remain unchanged.

[attempt-history.json](attempt-history.json) distinguishes earlier failed/partial attempts from complete
qualification. It retains an Aspire EventStore stop failure that did not recur on the unchanged retry.
The initial Contracts Release-artifact stub mismatch was fixed by explicitly selecting the current built
test assembly only with `SkipRestoreBuild`; ordinary CI still builds/executes the fixed Release/package
artifact and enforces existence/execution-count guards.

Production needs an explicit finite non-confidential workspace policy. Confidential cleartext execution
continues to fail closed pending Story 12.7. Historical descriptor-only metadata cannot manufacture a body
binding or readable bytes; no legacy-body reconstruction/migration is claimed. Changes consume the new
uncommitted EventStore source API; packaged consumption requires a separately authorized upstream release.
Independent review is complete; the current results and limitations are in the final review snapshot linked above.
