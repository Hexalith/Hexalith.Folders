# Story 1.17 NSS inventory retry — 2026-10-31

**The inventory size blocker is corrected for the observed host. Strict readiness
remains blocked by real process/descriptor churn; the mutation suite stayed
unexecuted.** No error or loss was discarded to obtain READY.

Nine additional allocations total **346 seconds**, including collector setup,
for a conservative aggregate **899/900 seconds** with the previous 553 seconds.
One second remains unallocated; no further capture is planned. Teardown and
metadata-only after inventories do not extend the kernel collection deadlines.
Installed BCC 0.35.0 and the already prepared matching headers were used without
reprovisioning. The dependency check exits 0; it is not a completeness claim.

## Collector correction and offline verification

The retained failed standalone inventory contains 748,987 shared-mapping rows
occupying 251,410,790 bytes. Descriptor rows occupy another 13,001,380 bytes;
their exact payloads contain substantial duplication. These measurements came
from the previous archived metadata, without reading NSS contents.

Only `collector.py` and `test_collector.py` changed in this continuation:

- Kernel-confirmed KCMP_VM sharing references one successful mapping inventory.
  Each target task retains its own PID/TID/group/task starts, source identity,
  source observation interval, and before/after proof. Unknown/private resources
  enumerate fully; failed identity/sharing checks remain errors.
- Exact descriptor payload references follow fresh ordinary metadata/race checks.
  Target identity, FD and new observation time remain distinct. The per-process
  cache is bounded; saturation emits full rows. Shared descriptor-table references
  also retain the source task identity and interval.
- Process enumeration streams entries, retaining every observed disappearance.

The [reference format](INVENTORY-FORMAT.md) describes reconstruction and timing.
All 19 paired hooks, three raw hooks, BPF source, output cap/reserve, loss
accounting, deadlines and handshake requirements remain unchanged from the
start of this continuation. Existing earlier BPF corrections are preserved.

Normal and optimized offline runs pass **73 collector tests and 7 helper tests**.
Exact commands, exits and output are in [offline-final.json](offline-final.json).
The separate helper suite actually consumes simulated readiness and proves zero
mutation routes for wrong PID/start/owner/start/boot/probes/snapshot/deadlines;
it also checks exit/finalization refusal, cleanup and budget serialization.
Independent review findings, corrections and follow-up checks are recorded in
[FIX-SPEC.md](FIX-SPEC.md). AST/formatting checks and `git diff --check` pass.

The read-only, non-BCC inventory probe completed traversal at **28,346,410 bytes**
(27.03 MiB), retaining **five errors**. Its original result remains unchanged;
that observation never established a clean inventory. The helper now returns a
nonzero status for such incomplete observations. Host populations changed between
observations, so this is not an identical-population benchmark.

## Retained live attempts

All attempts used newly created synthetic directories beneath
`/var/tmp/story117-nss-retry.ubgn0d7i`, outside the real NSS store. New outputs
were private and exclusive. The ancestor shell, collector PID/start, boot ID,
mandatory hooks, snapshot flag and both deadlines were checked. The helper
also refuses observed collector exit/finalization before each mutation route.

| Attempt | Allocation | Actual outcome |
| --- | --- | --- |
| 08-fixture-01 | 60 s | Complete traversal; 33.10 MiB before inventory, 7 retained before errors; invalid handshake; zero mutation routes. |
| 08-fixture-02 | 60 s | 26.99 MiB before inventory, 3 errors; invalid handshake; zero mutation routes. |
| 08-fixture-03 | 60 s | 27.77 MiB before inventory, 3 errors; invalid handshake; zero mutation routes. |
| 08-fixture-04 | 60 s | 27.89 MiB before inventory, 3 errors; invalid handshake; zero mutation routes. |
| 08-fixture-05 | 60 s | 27.58 MiB before inventory, 3 errors; invalid handshake; zero mutation routes. |
| 11-signal | 25 s | Owned pidfd SIGHUP; receipt retains signal/setup interruption and incomplete snapshot. |
| 12-paused-deadline-loss | 15 s | Paused after tracing began; resumed after deadline; 72,402 submission failures retained; incomplete receipt. |
| 13-setup-deadline | 1 s | Mandatory attachment completion refused after deadline; no events, no READY, missing owned profile accounting retained. |
| 14-overflow | 5 s / 128 KiB | Setup deadline first, then after-inventory overflow; reserved incomplete receipt retained. This is not a new active-event-overflow validation. |

Every collector exits 2. Control `case_passed` means its intended incomplete
condition was observed; it does not grant readiness or complete accounting.
The first five receipts contain `ready: true` because the collector emitted its
readiness document, but every document says `before_snapshot_complete: false`.
**There were zero valid readiness handshakes.** That distinction is preserved.

The five before inventories contain actual disappearing processes, task exits,
and descriptor/fdinfo disappearance. All errors remain in the raw JSONL and
[summary](summary.json). Both the main preexisting descriptor and a genuinely
unshared thread's descriptor were observed with their recorded identities in
all five inventories. Mapping and descriptor references replay without missing
sources or invalid proof intervals. These are metadata observations within
incomplete host inventories, not mutation-route credit.

There are **182,557 new ambient events**, six observed UIDs (0, 100, 999, 1000,
1654, 65532), 148,252 nonleader-thread records, 26,692 fork records carrying
creator identity, and 152,907 ambient parent identity joins. Delivered records
have **zero observed monotonic/boottime interval violations**. The seven full
attachments retain 133 owned return-profile rows, all reporting zero misses.
This does not measure entry PMU dispatch/recursion loss or paired omissions.
Substantial submission failures and perf-loss notifications remain explicit;
they can overlap, and arithmetic disagreement is additional uncertainty.

## Remaining validation and limits

The supported mutation-route matrix, actual mutations through existing/private
handles, UID child fork/exec attribution, and rename replacement/exchange identity
assertions remain **unexecuted** after the failed handshakes. No synthetic target
event or owned SDK writer attribution is credited. Preparation created sparse
synthetic files and existing handles/mapping; no mapping stores occurred before
handshake. The gated mutation suite never ran.

Already-writable mmap stores remain unsupported. Page-mkwrite gives evidence
only, never content-mutation credit. A fixture-scoped `__kernel_write` trigger,
generic filemap fault, async/io_uring completion, DAX/filesystem bypasses,
external Windows/VM/host writers, aliases/path bounds, snapshot races, actual VM
suspend and zero PMU dispatch loss remain unverified or unsupported. No-events
and zero owned missed counts cannot prove global immutability.

## Cleanup, preservation and authority

[Cleanup verification](cleanup-verification.json) finds no live bound collector,
no remaining exact aliases among the 133 recorded owned aliases, and no synthetic
directories. BCC cleanup is reported by all nine receipts. No global tracing or
audit state was cleared. Prepared headers remain installed. Raw metadata,
receipts and source versions are retained in the separate additive archive
identified by [archive.json](archive.json), with streamed member verification.
The previous archive and every previous failure remain unchanged.

The [final preservation audit](preservation-final.json) checks all 12,367
original tracked regular files and every prior diagnostic evidence file. Only
the two intended collector files changed from this turn's baseline. The earlier
BPF changes, original README/VALIDATION/SPEC, historical evidence and protected
authority files remain byte-identical. A concurrent **CI-only merge** advanced
HEAD/index to `89c610e7791455a71740cd0ee394a263adc9ec5f`; its one CI pin change is
recorded in [external-checkpoint.json](external-checkpoint.json). The preliminary
drift audit remains additive. This work issued no Git mutation commands and did
not revert that external change.

No real NSS content was read or mutated by these helpers, and no project build,
qualification, application resource start, publication or deployment ran.
Historical attribution at **2026-10-30 16:39:27 UTC** and **16:54:04 UTC** remains
**unresolved**. Qualification remains **incomplete**, parent **in-progress**,
tracker **backlog**, **V1Only**, **T0 unset**, **activation authority null**, and
staging **0/14**. The remaining readiness blocker is the required error-free
host inventory on this churning host; substantial ambient loss independently
prevents complete accounting. No constraint was weakened.
