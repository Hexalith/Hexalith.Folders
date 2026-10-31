---
title: 'Story 1.17 bounded inventory and fixture retry'
type: 'bugfix'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '765544efc5c8aa395895f3a3d5911d4364658639'
tracker_policy: 'no-automatic-transition'
context:
  - '/home/administrator/projects/hexalith/folders/AGENTS.md'
  - '/home/administrator/projects/hexalith/folders/eng/story117-nss-writer-trace/README.md'
---

<frozen-after-approval reason="user explicitly authorized continuing narrow collector fixes and bounded synthetic validation">

## Intent

Reduce duplicated host inventory metadata while preserving required coverage and strict readiness. Then retry the synthetic fixture outside the real NSS store, only after a valid boot/PID/start/owner/probe/snapshot/deadline readiness handshake.

## Boundaries & Constraints

Preserve existing modifications and every prior failure. Implementation edits only collector.py and its tests; new evidence/helpers only in this directory. Do not modify BPF coverage, loss accounting, READY requirements, MAX_BYTES or frozen authority. Never discard a race/error to get READY. Do not read/mutate real NSS content, run project builds/qualification/resources, publish/deploy or write Git state. At most 347 additional allocated capture seconds including setup, for a total at most 900. All unsupported mmap/async/dispatch coverage stays explicit. Qualification incomplete; parent in-progress; tracker backlog; V1Only; T0 unset; activation authority null; staging 0/14. Historical writers at both 2026-10-30 16:39:27 UTC and 16:54:04 UTC remain unresolved.

## I/O & Edge-Case Matrix

| Scenario | Expected result |
| --- | --- |
| KCMP_VM confirms sharing before and after task observation | One shared mapping inventory plus explicit per-task references containing source/target task identity and observation timing note |
| Unknown/private VM or file table | Enumerate fully; never infer sharing from same process, exe or equal maps |
| Sharing changes, task/reference exits or start identity changes | Keep error; incomplete snapshot; no valid handshake |
| Duplicate descriptor metadata | Reference an exact previously emitted payload only after fresh ordinary metadata/race checks; no identity/timing loss |
| Process churn | Stream proc enumeration to reduce stale lists; retain every observed error |
| Failed readiness | Withhold fixture mutations, retain receipts/logs and cleanup observation |

</frozen-after-approval>

## Code Map

collector.py snapshot_process currently repeats maps for every task. shared_descriptor_table uses x86-64 kcmp=312 KCMP_FILES=2. KCMP_VM=1 is supported by installed kernel source and read-only host checks. snapshot_inventory eagerly sorts PIDs, adding avoidable stale-observation delay. BoundedOutput, capture hooks, policy, handshake fields and loss code remain unchanged. Existing snapshot tests use synthetic per-thread private fd/maps and patched sharing.

## Tasks & Acceptance

- [x] collector.py: add confirmed KCMP_VM map references with full task/reference start identities and end proof; preserve unknown/private fallback and all errors.
- [x] collector.py: compact exact duplicated descriptor payloads within each process using explicit references to an emitted observation, with bounded cache and fresh checks; retain full fallback.
- [x] collector.py: stream process enumeration using live scandir; do not ignore disappearing entries.
- [x] test_collector.py: cover sharing/unknown/private/unshare/reused or exited reference; round-trip exact descriptor references, changed payload and identity; meaningful existing traversal regressions.
- [x] Parent: normal/optimized offline suite and source checks before any capture. Record snapshot size/errors without tracing. Run bounded fixture retry and retain all failures, execution identities, losses, deadlines and cleanup.

Given shared resources, when inventories are compacted, then every task and all required metadata remain reconstructible with explicit original observation timing. Given churn or incomplete snapshots, when capture runs, then no errors are removed and no valid handshake is asserted. Given readiness failure, when fixture orchestration completes, then zero post-handshake mutation routes execute.

## Implementation Notes

Archive replay: failed standalone snapshot had 748987 shared_mapping rows (251410790 bytes), 25406 descriptors (13001380 bytes), 4745 shared-table references. Exact descriptor payloads total 9111722 bytes with 2413383 unique bytes. Maps dominate. Use additive files here; do not amend previous REPORT/SPEC/README/VALIDATION or authority files. Existing user authorization permits proceeding on this dirty tree and continuing to concrete validation.

## Review Triage Log

## Verification

/usr/bin/python3 -B -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py', repeated with -O. Read-only snapshots and dependency checks. Parent writes independent continuation-only diff; no staging. Use installed BCC/headers only.


### Independent review triage

All three review layers returned before triage. Findings concern the new diagnostic helpers, not a relaxation of collector coverage. Every row below is assessed before grouping.

| ID | Verdict | Evidence and disposition |
| --- | --- | --- |
| B1 | medium | Setup failure after worker start occurs before cleanup scope; patch cleanup registration before starting it. |
| B2 | medium | Worker identity has leader ticks only; retain separate task start ticks. |
| B3 | medium | Cached READY can outlive collector; check owned process and final-receipt state before every operation. |
| B4 | medium | Destination creation bypasses route guard/recorder; gate and record it. |
| B5 | medium | Concurrent helper reservations can lose budget allocations; lock reservations and replace ledger atomically. |
| B6 | medium | Control helper exit alone does not establish intended outcome; explicitly classify each receipt/action. |
| B7 | medium | Snapshot helper ownership of intermediate directory is implicit; validate runtime and use exclusive owned creation. |
| B8 | medium | Snapshot helper exits zero despite retained errors; retain result and use nonzero for incomplete observation. The original five-error result remains a failed clean-inventory observation. |
| B9 | medium | Worker exception is not propagated by join; record worker failure/skipped route explicitly. |
| E1 | medium | Same demonstrated setup/worker cleanup boundary as B1. |
| E2 | medium | pidfd acquisition can fail after launch before cleanup registration; register owned Popen cleanup immediately after launch. |
| E3 | medium | Same cached readiness issue as B3. |
| E4 | medium | Same budget concurrency issue as B5. |
| V1 | medium | Collector simulations never execute retry readiness consumption; add driver tests asserting zero mutations for every mismatched identity/probe/snapshot/deadline condition. |

Group B1/E1/E2 as helper lifecycle cleanup; B3/E3/V1 as readiness consumption and verification; B5/E4 as budget reservation. All route to bounded helper patches and focused offline tests. Preserve collector code and previous evidence. User authorization and frozen state rules override workflow steps that would revert earlier user work, alter trackers or create commits.


Follow-up review found two bounded defects in the helper corrections: B10 (medium), snapshot output was closed twice after introducing ExitStack; remove the redundant close and exercise a successful offline snapshot. B11 (medium), a live collector can have closed its gate while producing after.jsonl before its receipt; reject this observable stopping phase and test it. Neither changes collector coverage or readiness requirements. V1's reviewer confirms the driver refusal gap is addressed; normal and optimized helper discovery are separate from collector discovery.


### Live outcome and remaining blocker

Collector implementation and offline/helper reviews are complete. Normal and optimized suites pass 73 collector tests plus 7 helper tests. Read-only inventory completes at 28,346,410 bytes while retaining five errors. All five 60-second fixture retries retain 3..7 before errors; valid handshakes and executed mutation routes remain zero. Four control allocations (25+15+1+5 seconds) exercise SIGHUP, paused deadline/loss, setup deadline and after-snapshot overflow. New allocation is 346 seconds; aggregate 899/900. No further capture is planned. The full fixture validation remains incomplete, so this continuation stays in-progress; no parent/tracker/qualification transition occurs.

Both existing main and unshared-thread descriptors are observed in all five fixture before inventories with their recorded identities. Reference replay finds no unresolved references. None of this grants mutation-route or historical attribution. Every error, loss and earlier attempt remains retained. All 133 recorded owned aliases and known collector processes are absent after cleanup; synthetic directories are removed and prepared headers retained.

An external CI-only merge advanced HEAD/index to 89c610e7791455a71740cd0ee394a263adc9ec5f during the final audit. Preserve that checkpoint; this work issued no Git mutations. Preliminary drift evidence and the classified final audit remain additive.
