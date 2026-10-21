# Current integrated-source continuation

Task 36 remains open. The strict authenticated real-provider case failed on current
binaries; component passes and partial live observations do not establish acceptance.
No product source or SDK source was changed in this continuation.

The full Debug/source restore and serialized solution build passed with zero warnings
and errors. The six individual consumer lanes passed 4,249 cases, with zero failures
and the same two optional Domain skips. Both complete original-owner component cases
at 10,001 and 32,769 positions passed. Exact commands and results are retained in
`builds-attempt-1.json`, `components.json` and their XML/log files.

Strict live attempt 1 failed after 240.562 seconds while waiting for
`WorkspacePreparationRequested`. The retained log observes internal HTTP 429 at
`IdempotencyAdmissionActor/MarkRecoveryAsync` in the anonymous/unknown partition,
admission-unavailable retries, a later HTTP 202, and no required event within the
unchanged 90-second wait. The provider-binding rebuild and actual repository-denial
subchecks passed. The log does not establish a causal fix or a retained terminal
command outcome. See `strict-attempt1-diagnosis.json` and the full XML/log/receipt.

The separate 10,001-position HTTP lane failed after 1,280.421 seconds, with zero
skips. Both original owners accumulated, large staging completed in 581.528 seconds,
four actual managed hosts changed PIDs, and the same Prepared session committed and
verified. A subsequent independent-owner generation also committed and verified;
the large owner's checkpoint remained readable through inheritance. The first
historical duplicate (index 0) then returned Failed instead of AlreadyCompleted
at `OperationalStatusBoundedHistoryAcceptanceTests.cs:83`. Growth/repeated rebuild
and later conflict assertions were not reached. No complete HTTP pass or
32,769-position Folders HTTP execution is claimed.

The [SDK handoff](sdk-inherited-owner-handoff.md) isolates the failure using tiny
reproductions in two distinct frozen binary groups. The second explicitly loads
the live Folders Server DomainService copy and matches all five production modules
to live runtime hashes; its sixth module is the frozen Domain test harness.
The first retains the distinct Domain test-group DomainService copy.
With evolution disabled and enabled,
identical canonical request bytes and an unchanged captured owner checkpoint
produce AlreadyCaptured before another owner promotes, then IdentityConflict
afterward. Named dispatch returns `Failed/delivery_identity_conflict`; the Folders
fold callback is entered zero times for the failed replay. The live helper did not
retain its response ReasonCode; the exact reason and layer are established by
the frozen-DLL reproduction, separately from the live assertion. No SDK production
edit, consumer workaround, weakened assertion or additional full live attempt was
made.

`source-freeze.json` binds 12,118 source/config inputs and 546 PE/PDB files.
All 271 inspected PDBs match 37,909 ordinary source-document copies, with zero
mismatches or missing ordinary sources. The verifier excludes generated obj/bin
documents; no generated-document qualification is inferred. `captured-graph.tar.gz`
preserves the exact inventoried source and binary bytes.

`sdk-prebuild-audit.json` preserves 62 source differences, 24 differing SDK
Server-test PE/PDB files and one changed historical Server XML against the approved
SDK packet. Historical SDK capability/process results remain evidence for their
recorded graph, including its failed broad gate. Changed batch/source-proof paths
receive no exact-candidate qualification from that evidence. During execution,
the SDK HEAD advanced from `1b5ec217a74cdfdd84605e093114071b56c9f700` to
`1748b16b11fb63b885dfa2fd4a9c7a420ea103a9` through an external planning-only commit;
the final audit still matched every frozen source and binary hash. The audit
command returns nonzero because it also checks submodule HEAD equality; its retained
result distinguishes that planning-only head difference from zero source/binary drift.

`cleanup.json` found no surviving exact fixture-owned processes or containers.
The unrelated Tenants AppHost retained its original PID 67730. Actual managed
capacity PIDs changed as follows: Folders 82332 to 22801, Workers 82336 to 24181,
replica 69066 to 25698, and EventStore 82207 to 27606. Runtime module hashes and
their comparison with the frozen binary inventory are retained in
`runtime-binding-audit.json`.

Historical packets, assertions, deadlines, retry limits, limiter settings, quota,
unrelated processes, dependency pins and Git index were preserved by this task.
SDK current-source capability qualification, its broad regression gate,
package/PD10, SM4 and independent scoped/whole-story review remain separate gates.
