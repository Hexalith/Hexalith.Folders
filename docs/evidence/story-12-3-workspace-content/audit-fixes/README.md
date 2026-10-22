# Story 12.3 parent-audit corrections

This snapshot supersedes the original evidence for changed source. The original
[qualification](../qualification.json), 106-entry source inventory and logs remain historical and unchanged.
All audit correction builds, generators and test lanes ran sequentially. Some earlier exploratory lanes
in the original implementation overlapped; its final gates were sequential.

The fixes add a unique revocable CAS claim followed by irreversible `DeletionAuthorized` metadata before
physical deletion. Stale successful/failed callers cannot reopen a committed phase. Current source proof
and a fresh P7D epoch remain necessary before commitment after a resume/source change. A durable source
grant permanently disposes this receipt against future references, including from resumed work;
unrelated new tasks/locks remain allowed. Physical completion can recover after process replacement.

Active schemas, examples, deterministic generated DTOs and converters require observed file version and
positive lock fence. Builders/helpers reject missing/invalid observations before stream reads or sends.
CLI/MCP adapters expose and safely validate those inputs. Private erasure processing rejects null or
noncanonical workspace/receipt/task/generation values. Production inline/multipart endpoint regressions
prove configured `confidential_cleartext_required` returns safe 422 before SDK persistence or admission.

| Current lane | Passed | Failed/skipped |
| --- | ---: | --- |
| Upstream protocol/concurrency/crash | 16 | 0/0 |
| Upstream actual Redis/Dapr persistence | 1 | 0/0 |
| Existing affected SDK helper consumers | 49 | 0/0 |
| Focused Server transport/erasure/mapping | 58 | 0/0 |
| Client | 342 | 0/0 |
| Focused CLI | 31 | 0/0 |
| Focused MCP | 15 | 0/0 |
| Contracts/generators/parity | 360 | 0/0 |
| In-process REST→gateway→/process→gate→SDK | 1 | 0/0 |
| Production two-serving-replica live class | 1 | 0/0 |

The live class passed in 258.288 seconds. Both actual serving Folders processes were replaced:
primary managed PID 374836→430972 (launchers 373949→430637), replica managed PID
374945→439553 (launchers 373953→439185). Requests targeted distinct destinations
`http://localhost:33901/` and `http://localhost:39367/`. Workers and EventStore also changed identities.
All 30 distinct loaded DLL paths and all 30 PDB paths match current outputs across 174 retained bindings.
The SDK smoke independently replaced sidecar/client/store identities (PIDs 4133490→4133607), inspected
8192 actual persisted bytes and verified physical erasure.

Current live assertions include inline/replay, exact post-restart bytes, multipart, missing/corrupt bodies,
202/409 replica contention, one persisted version-1 winner, binary/hidden/foreign/unavailable failures,
immediate removal suppression, hold/release/resume/P7D physical erasure and atomic two-file publication.
This current race returned an early canonical conflict without a retained domain rejection marker. The
historical `AppendConflict` observation remains in the original snapshot; current focused mapping tests
still pin its canonical 409 and preserve unrelated validation/denial outcomes.

[qualification.json](qualification.json) records exact commands, full-log SHA-256 values, results and
matrix mapping. [source-inventory.json](source-inventory.json) records 118 current source/support paths,
including the 29 changed/new paths since the historical inventory. [live-runtime-bindings.jsonl](live-runtime-bindings.jsonl)
and [live-assertions.log](live-assertions.log) retain metadata only. [attempt-history.json](attempt-history.json)
keeps the failed correction attempts separate from final passing results. The upstream
[correction evidence](../../../../references/Hexalith.EventStore/docs/evidence/story-12-3-content-authority/audit-fixes/qualification.json)
binds all 34 upstream source/guide/helper paths.

Frozen intent remains `85b7f36c3ef508e9b18486ab9c14c13f674e65fd6a67cfb49fa0811ce41f7cfc`.
OQ2 and immutable historical v1 are unchanged. Candidate v2 is regenerated and remains disabled.
The original broad Debug/source build/Core/full-Server/host qualification remains historical; the audit
used the affected gates above without another blanket solution/all-suite cycle. No package publication,
dependency change, remote or VCS recording occurred. Independent parent workflow review remains pending.
