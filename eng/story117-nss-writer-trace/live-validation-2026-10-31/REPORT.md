# Story 1.17 live NSS collector validation — 2026-10-31

**Provisioning and live controller checks ran; full fixture validation remains
incomplete.** The required READY handshake never succeeded: a standalone
all-process snapshot exceeds the collector's maximum 256 MiB budget and observes
process churn. The synthetic mutation suite was therefore withheld. No weaker
readiness or coverage contract was substituted.

The seven requested capture windows total **553 seconds**, conservatively
counting setup and failed attempts, below the authorized aggregate 900 seconds.
The collector never launched a workload. Disposable fixture preparation created
three synthetic files and warmed one existing descriptor/shared mapping outside
the real NSS store. No real NSS store content was read or mutated by this work.
No project builds, qualification, application resource starts, publication,
deployment, dependency updates in the repository, or Git writes were performed.

## Provisioning

Installed the README prerequisites using `sudo apt-get update` and
`sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends
python3-bpfcc build-essential flex bison libssl-dev libelf-dev bc dwarves curl
ca-certificates`. Both commands exited 0. BCC is 0.35.0; exact package versions
and logs are in [provisioning](provisioning/receipt.json).

The archive from Microsoft's exact `linux-msft-wsl-6.6.87.2` tag has SHA-256
`a74e856bf47f13f5e0da7df4887c14770bce2b42b489cd1a97f31a46cf660b75`.
The source/build tree is `/var/tmp/story117-wsl-headers.pyOhVksU` and its
`/lib/modules/6.6.87.2-microsoft-standard-WSL2/{source,build}` links remain
installed. No kernel image was built or installed; no reboot occurred.

The documented `olddefconfig prepare modules_prepare` initially failed in the
bundled host libbpf against Ubuntu's C23 string interfaces. A host-only compiler
wrapper uses GNU11 and the older string interface for that source. A failed
broader compatibility-header attempt is retained too. No warning suppression,
manual kernel-source/configuration edit or release-pin change was used. The
final preparation completed and `UTS_RELEASE` equals the running release.

**Configuration precision:** the documented `olddefconfig` recalculated compiler
and tool-capability symbols; prepared configuration bytes are not identical to
`/proc/config.gz`. The full [diff](provisioning/config.diff) and both configs are
retained. Earlier progress wording about unchanged configuration means no manual
configuration changes, not byte identity. Release equality alone does not prove
every running-kernel ABI offset. The final dependency check exits 0 and still
correctly makes no full-capture verification claim.

## Retained live outcomes

All attempts used the disposable path
`/var/tmp/story117-nss-live.dDxfX6SS/synthetic/nssdb`, a new root-private output
directory, host PID/mount namespace acknowledgment, a bound ancestor shell, and
both kernel deadlines. Individual metadata, provenance and receipts are in
[receipts](receipts/); absent receipts below remain absent.

| Attempt | Window / budget | Actual result |
| --- | --- | --- |
| 01 original attachment | 120 s / 64 MiB | Exit 1: LLVM rejects a large builtin memset before attachment; no final receipt. |
| 02 reset correction | 120 s / 64 MiB | Exit 2: all 19 paired and three raw hooks attach; ABI-padding rejection refuses READY; cleanup reported and aliases absent. |
| 03 fixture handshake | 180 s / 256 MiB | 76,722 ambient events decoded; output exhausted before READY; mutation suite withheld; controller exits -11 during unsafe reflected pair-value accounting; no final receipt. |
| 04 overflow after fixes | 60 s / 128 KiB | Exit 2: bounded overflow, reserved receipt retained, 7 events, cleanup reported. |
| 05 pidfd SIGTERM | 60 s / 32 MiB | Exit 2: signal 15 retained, 210 events, 509 lost notifications, 511 submission failures, incomplete receipt and cleanup. |
| 06 paused deadline/loss | 12 s / 32 MiB | Exit 2: owned collector paused with pidfd SIGSTOP and resumed after its deadline; 1,233 events, 77,505 submission failures; timeout retained, cleanup reported. |
| 07 setup deadline | 1 s / 32 MiB | Exit 2: setup deadline refuses mandatory attachment completion/READY; no events; cleanup reported. Missing owned rows remain incomplete. |

The three corrected full-attachment controller checks (04–06) retain all 19
owned return-profile rows with zero reported misses. Those rows do not measure
PMU entry dispatch misses. Failed submissions and perf-loss notifications can
overlap; the signal run's arithmetic disagreement remains explicit uncertainty.
Overflow also leaves delivery/accounting incomplete. No loss is cancelled by
another counter.

Across the streams, [summary.json](summary.json) records 78,172 ambient events,
four UIDs (0, 100, 1000, 1654), 71,589 nonleader-thread records and 8,539 raw
fork records carrying creator identity. There are 72,481 parent identity joins
among 78,172 records using PID plus exact kernel start identity. These are
observations of the busy host, not owned fixture/SDK attribution. All delivered
events pass the recorded monotonic and boottime interval checks. Pausing Python
past the deadline exercises independent kernel gating, but cannot prove the
absence of undelivered events or actual suspend behavior.

## Coverage left incomplete

Ambient events include scalar/vector writes, attributes, rename, link/unlink,
mkdir/rmdir/mknod, copy, direct/pipe splice, ext4 page-mkwrite and fork/exec/exit.
They do **not** complete the supported-route fixture matrix. No synthetic-target
event is credited.

| Requested check | Evidence/limit |
| --- | --- |
| Supported mutation routes and exact rename replacement/exchange identities | Fixture suite prepared but unexecuted after failed handshake. |
| Existing handle and thread-private handle mutations | A descriptor and mapping existed before attempt 03; no traced synthetic mutation/complete inventory is credited. Private-table traversal remains covered by offline regressions. |
| UID/thread coverage | Ambient UIDs and thread records observed; owned UID child route unexecuted. |
| Event identity and ancestry | Kernel fields and ambient lifecycle joins retained; no owned workload attribution or historical attribution. |
| Kernel-write and generic filemap-mkwrite routes | Mandatory attachment succeeded; no fixture-scoped kernel-write trigger or generic filemap event observed. |
| Loss, overflow and incomplete snapshots | Actual failures retained; all receipts remain incomplete. |
| Deadlines and cleanup | Actual setup timeout, paused ordinary deadline, SIGTERM, final alias/process checks; no VM suspend or separate SIGINT/SIGHUP/SIGQUIT exercise. |

Already-writable mmap stores remain unsupported; page-mkwrite never receives
content-write credit. Async/io_uring, filesystem/DAX bypasses, external host/VM
writers, mount/path aliases, snapshot races, PMU dispatch/recursion misses and
paired omissions remain explicit limitations. Zero owned missed counts do not
prove zero dispatch loss or global immutability.

## Narrow fixes and focused verification

- Explicit volatile metadata resets replace unsupported large memset calls;
  a C size assertion binds the 3304-byte Python event ABI.
- The decoder accepts Linux perf's exact raw padding and retains observed sizes;
  the BCC loss callback uses its actual one-argument interface.
- Kernel-confirmed shared descriptor tables use references; private/unknown
  tables still enumerate, and an unsharing race makes the snapshot incomplete.
- Pair accounting counts keys without reading BCC's undersized reflected leaf.
  After-snapshot overflow also sets the overflow flag.

`/usr/bin/python3 -B -m unittest discover -s eng/story117-nss-writer-trace
-p 'test_*.py'` passes **58/58**, and the same command with `-O` passes **58/58**.
All diagnostic Python files parse and `git diff --check` passes. The independent
review and triage are in [FIX-SPEC.md](FIX-SPEC.md). Final helper safety checks
refuse reused artifacts and a synthetic descendant symlink without modifying
the outside sentinel or attaching anything. The helpers were hardened after
execution; their unexecuted mutation routes are not live validation evidence.

## Cleanup, evidence and preserved authority

The [cleanup observation](receipts/cleanup-observation.json) finds no remaining
rows for 76 recorded owned return aliases and no live known collector PID.
Read-only tracefs inspection also found zero BCC aliases after the crashed
attempt. Its missing final receipt is not repaired or relabelled as successful
cleanup. No global tracing state was cleared. The three synthetic files and
their directories were removed; evidence and provisioned headers remain.

Raw metadata streams, failed attempts and source versions are retained in the
ignored archive identified by [archive.json](archive.json) and
[capsule.members.json](capsule.members.json). Its 35,667,456 bytes have SHA-256
`a7e163e6896883ea95eb0ac116483fccd8b44fa0e500df9a016a310a36a759d2`.
All 96 members passed a streamed size/hash replay without extraction or command
execution; see [capsule-verification.json](capsule-verification.json). It
contains metadata and diagnostic sources only, with no NSS file contents.
Small receipts remain directly inspectable beside this report. Original and
intermediate diagnostic source bytes are bound by exact SHA-256, including
attempt 03's observed Python source recovered by an exact digest match.

[preservation-after.json](preservation-after.json) audits all 12,367 original
tracked regular files and the index/HEAD baseline. It finds 12,364 unchanged.
Only the three collector implementation/test files are intentionally changed;
the original README, VALIDATION, preparation SPEC, historical receipts and all
protected authority files remain byte-identical.

Both **2026-10-30 16:39:27 UTC** and **16:54:04 UTC** remain unresolved.
Qualification remains **incomplete**, parent **in-progress**, tracker **backlog**,
production **V1Only**, **T0 unset**, **activation authority null**, and staging
**0/14**. This diagnostic grants no status, startup, publication or deployment
authority. A complete fixture run still needs a host whose required inventories
fit the bounded output contract and pass the unchanged readiness handshake.
