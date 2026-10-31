# Story 1.17 prospective NSS writer diagnostic

These files prepare a future, explicit opt-in observation. They have only been
validated offline. They do not identify the writers at **2026-10-30 16:39:27 UTC**
or **16:54:04 UTC**. Historical attribution remains unresolved. A new observation
cannot establish either historical writer.

Qualification remains incomplete; the parent remains in-progress, tracker
backlog, production control V1Only, T0 unset, activation authority null, and
staging 0/14. No receipt from this diagnostic can promote those states.

`collector.py` defaults to help. `check` and `render` use Python's standard
library, do not import BCC, and never compile or attach probes. `capture` alone
imports BCC, after policy and dependency checks. The collector launches no
workload and reads no argv, environment, user buffers, NSS database contents,
certificate bytes, or key bytes. It reads kernel/proc metadata and its own C
source; BCC necessarily reads kernel compilation headers. Paths, comm, file
metadata and process identities are retained.

## Offline use — safe to run now

From `/home/administrator/projects/hexalith/folders`:

```bash
python3 -B eng/story117-nss-writer-trace/collector.py
python3 -B eng/story117-nss-writer-trace/collector.py check
python3 -B eng/story117-nss-writer-trace/collector.py render
python3 -B -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'
python3 -B -O -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'
```

`check` currently exits **2**: Python `bcc` is absent, and prepared matching
headers at `/lib/modules/6.6.87.2-microsoft-standard-WSL2/build` are absent.
`/sys/kernel/btf/vmlinux` exists. BTF does **not** supply the generated C headers,
kernel configuration, or preparation needed by Python BCC. Installing bpftrace
or audit tools would not supply this collector's Python BCC runtime; neither is
used here. Those tools are also absent on the current host.

The default render substitutes zero deadlines, which disable every event. It
prints C to stdout; it does not check verifier acceptance or symbol attachment.
Tests create temporary synthetic metadata under this diagnostic directory and
remove it. Fake BCC tests simulate controller ordering and cleanup requests;
they supply no evidence about compilation, actual probes, or kernel cleanup.

## Prepared provisioning — NOT executed, separate authorization required

Current host: Ubuntu 26.04, x86-64 WSL2, kernel
`6.6.87.2-microsoft-standard-WSL2`. The read-only package index reported
`python3-bpfcc`/`libbpfcc` candidate `0.35.0+ds-1ubuntu2`. Minimum capture
requirements are Python 3.10+ with Python BCC and libbcc, compatible clang/LLVM libraries,
matching configured and prepared Linux 6.6 headers, readable proc metadata,
tracefs kprobe profile, kernel BPF/perf/kprobe/raw tracepoint support, and root
in the actual host PID/mount namespaces. Their presence alone does not prove
the program can compile or attach.

These are proposed commands only. Do not run them under the current preparation
authorization. Do not install an ordinary Ubuntu `linux-headers-generic` package
as a substitute for the running Microsoft WSL kernel headers.

Minimal proposed capture dependency installation:

```bash
sudo apt-get update
sudo apt-get install --no-install-recommends python3-bpfcc
```

The current `apt-cache depends python3-bpfcc` lists `libbpfcc`, `libclang-cpp21`
and `libllvm21` among its dependencies; apt installs them. This minimum command
does not provide the missing prepared WSL kernel headers. Optional tools for
the source/header preparation recipe below are separate:

```bash
sudo apt-get install --no-install-recommends \
    build-essential flex bison libssl-dev libelf-dev bc dwarves curl ca-certificates
```

If matching prepared headers are not supplied separately, the following recipe
prepares headers from the exact Microsoft tag without Git operations, installing
a kernel, rebuilding the kernel image, or rebooting. It needs the packages above
and the current readable `/proc/config.gz`. It changes only a new private source/
build tree and the explicitly checked module header links. Review source archive
provenance and retained SHA-256 before using it. If any release check fails,
stop; do not edit version pins/configuration to disguise a mismatch.

```bash
set -euo pipefail
umask 077
kernel_release=$(uname -r)
test "$kernel_release" = '6.6.87.2-microsoft-standard-WSL2'
headers_root=$(mktemp -d /var/tmp/story117-wsl-headers.XXXXXXXX)
source_dir="$headers_root/source"
build_dir="$headers_root/build"
mkdir "$source_dir" "$build_dir"
curl --fail --location --proto '=https' --tlsv1.2 \
    'https://github.com/microsoft/WSL2-Linux-Kernel/archive/refs/tags/linux-msft-wsl-6.6.87.2.tar.gz' \
    --output "$headers_root/linux-msft-wsl-6.6.87.2.tar.gz"
sha256sum "$headers_root/linux-msft-wsl-6.6.87.2.tar.gz"
tar -xzf "$headers_root/linux-msft-wsl-6.6.87.2.tar.gz" \
    --strip-components=1 -C "$source_dir"
gzip -dc /proc/config.gz > "$build_dir/.config"
make -C "$source_dir" O="$build_dir" olddefconfig prepare modules_prepare
prepared_release=$(make --no-print-directory -s -C "$source_dir" O="$build_dir" kernelrelease)
test "$prepared_release" = "$kernel_release"
test -f "$build_dir/include/generated/autoconf.h"
test -f "$build_dir/include/generated/utsrelease.h"
test ! -e "/lib/modules/$kernel_release/build"
test ! -L "/lib/modules/$kernel_release/build"
test ! -e "/lib/modules/$kernel_release/source"
test ! -L "/lib/modules/$kernel_release/source"
sudo mkdir -p "/lib/modules/$kernel_release"
sudo ln -sT -- "$build_dir" "/lib/modules/$kernel_release/build"
sudo ln -sT -- "$source_dir" "/lib/modules/$kernel_release/source"
/usr/bin/python3 -B eng/story117-nss-writer-trace/collector.py check
```

The check requires `UTS_RELEASE` to equal the running release and generated
`autoconf.h` to exist. Header preparation and compilation compatibility remain
unverified. If the exact headers or probe symbols are unavailable, retain the
failure and stop; no fallback silently weakens mandatory coverage.

## Prepared future capture — NOT executed, separate opt-in required

Run from the actual host PID and mount namespaces, with host `/proc` mounted.
The explicit `--host-namespaces-confirmed` is the operator's assertion that this
is the host, rather than a container's PID 1. The controller also verifies root
and that its PID/mount namespace links match `/proc/1`. That check alone cannot
prove that a container's PID 1 is the real host. In WSL, “host” here is this WSL
Linux kernel; Windows/VM/external host writers remain outside coverage.

Use the same existing Bash shell for launch, readiness, any separately
authorized work, and stop. The collector requires that shell's PID to be an
ancestor, binds its proc start ticks, and stops if it exits or is reused. Assign
`owner_pid` **before** launching the background command; Bash's `BASHPID` inside
an asynchronous command can identify a subshell. NSS and output paths must be
real paths without symlink ancestors. Output must be new and outside the NSS
directory's parent. Its existing parent must be private (0700) and owned by the
collector EUID, which is root for live capture. Every ancestor is opened relative
to a no-follow directory FD and must be owned by root/the collector without group/
other write permission, except root-owned sticky ancestors such as `/var/tmp`.
Reuse is refused. Do not create the output directory first. Directory/file chmod
uses opened FDs; named/opened identities are compared so a replaced path cannot
redirect chmod.

```bash
set -u
umask 077
cd /home/administrator/projects/hexalith/folders
owner_pid=$BASHPID
nss_dir=/home/administrator/.pki/nssdb
sudo -v
capture_parent=$(sudo mktemp -d /var/tmp/story117-nss-trace.XXXXXXXX)
capture_out="$capture_parent/capture"
# This shell cannot write in the root-private parent; its separate log is 0600.
capture_log=$(mktemp /var/tmp/story117-nss-controller.XXXXXXXX.log)
sudo /usr/bin/python3 -B eng/story117-nss-writer-trace/collector.py capture \
    --nss-dir "$nss_dir" --output "$capture_out" \
    --owner-pid "$owner_pid" --host-namespaces-confirmed \
    --duration 900 --max-bytes 67108864 > "$capture_log" 2>&1 &
launcher_pid=$!

# This read-only handshake waits at most 900s; the collector has independent
# kernel deadlines. It prints the bound collector PID only after valid READY.
if collector_pid=$(sudo /usr/bin/python3 -B - "$capture_out" "$owner_pid" <<'PY'
import json, pathlib, sys, time
root, owner_pid = pathlib.Path(sys.argv[1]), int(sys.argv[2])
until = time.monotonic() + 900
while time.monotonic() < until:
    if (root / 'receipt.json').exists():
        raise SystemExit('Collector finished before handshake; inspect receipt/log, do not start work')
    try:
        ready = json.loads((root / 'ready.json').read_text())
        pid = ready['pid']
        text = pathlib.Path('/proc', str(pid), 'stat').read_text()
        fields = text[text.rfind(')') + 2:].split()
        owner_text = pathlib.Path('/proc', str(owner_pid), 'stat').read_text()
        owner_start = int(owner_text[owner_text.rfind(')') + 2:].split()[19])
        now = time.monotonic_ns()
        boot_now = time.clock_gettime_ns(time.CLOCK_BOOTTIME)
        if (ready['caller_shell']['pid'] != owner_pid or
                ready['caller_shell']['start_ticks'] != owner_start or
                ready['boot_id'] != pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip() or
                int(fields[19]) != ready['start_ticks'] or
                not ready['mandatory_hooks_attached'] or
                not ready['before_snapshot_complete'] or
                now >= ready['deadline_monotonic_ns'] or
                boot_now >= ready['deadline_boottime_ns']):
            raise SystemExit('Readiness/ownership/deadline check refused; inspect evidence')
        print(pid)
        break
    except (FileNotFoundError, json.JSONDecodeError):
        time.sleep(0.1)
else:
    raise SystemExit('Readiness timeout; do not start work')
PY
); then
    handshake_rc=0
else
    handshake_rc=$?
fi
if [ "$handshake_rc" -ne 0 ]; then
    if wait "$launcher_pid"; then collector_rc=0; else collector_rc=$?; fi
    printf 'Handshake failed (%s), collector exit %s; inspect %s and any receipt.\n' \
        "$handshake_rc" "$collector_rc" "$capture_log"
else
    printf 'READY collector PID %s; evidence directory %s\n' "$collector_pid" "$capture_out"
fi
```

READY means mandatory probes attached, owned return-profile rows exist, and the
before snapshot has finished. The handshake additionally requires a complete
before snapshot. No workload command is included: only a separately authorized
execution may run after a successful handshake. A caller-shell relationship is
not enough to attribute an NSS operation to an owned SDK child.

The exact next capture above uses the hard **900-second** maximum including
setup and a **67,108,864-byte (64 MiB)** metadata limit. Conditional handshake/
wait commands retain expected failure status even if shell `errexit` is enabled.

Duration is an integer **1..900 seconds including setup**, not a fresh window
after READY. The controller records monotonic and suspend-aware boottime
deadlines before output creation, BCC compilation, attachment and snapshots.
Both clocks independently gate kernel entry/return/lifecycle emission. Suspend
cannot extend the boottime limit. Teardown and after-snapshot observations can
finish later; they do not extend kernel collection. Signals can stop earlier.

## Stop, receipt and cleanup

Normally let the deadline expire and collect the background command's exit
status. To stop early, open a pidfd, verify READY boot/PID/start/owner identity,
then send SIGINT through that pidfd. An exited/reused numeric PID cannot redirect
the signal to a new process. These commands assume the successful
handshake variables above and run from its original shell.

```bash
if sudo /usr/bin/python3 -B - "$capture_out" "$owner_pid" <<'PY'
import json, os, pathlib, signal, sys
root, owner_pid = pathlib.Path(sys.argv[1]), int(sys.argv[2])
if (root / 'receipt.json').exists():
    print('Already finished; no signal sent')
    raise SystemExit(0)
ready = json.loads((root / 'ready.json').read_text())
pid = ready['pid']
if not isinstance(pid, int) or pid <= 1:
    raise SystemExit('Invalid collector PID; no signal sent')
try:
    pidfd = os.pidfd_open(pid, 0)
except ProcessLookupError:
    print('Collector already exited; no signal sent')
    raise SystemExit(0)
try:
    try:
        text = pathlib.Path('/proc', str(pid), 'stat').read_text()
        start_ticks = int(text[text.rfind(')') + 2:].split()[19])
        owner_text = pathlib.Path('/proc', str(owner_pid), 'stat').read_text()
        owner_start = int(owner_text[owner_text.rfind(')') + 2:].split()[19])
        boot_id = pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip()
        if (start_ticks != ready['start_ticks'] or ready['caller_shell']['pid'] != owner_pid or
                ready['caller_shell']['start_ticks'] != owner_start or ready['boot_id'] != boot_id):
            raise SystemExit('Boot/PID/start/owner mismatch; no signal sent')
        signal.pidfd_send_signal(pidfd, signal.SIGINT, None, 0)
    except (FileNotFoundError, ProcessLookupError):
        print('Collector or owner already exited; no signal sent')
finally:
    os.close(pidfd)
PY
then stop_rc=0; else stop_rc=$?; fi
if wait "$launcher_pid"; then collector_rc=0; else collector_rc=$?; fi
printf 'Stop request exit %s\n' "$stop_rc"
printf 'Collector exit %s; inspect receipt under %s\n' "$collector_rc" "$capture_out"
sudo /usr/bin/python3 -m json.tool "$capture_out/receipt.json"
```

Exit 2 means refused/incomplete accounting; exit 0 means the controller detected
no supported-probe accounting defect. **Neither exit grants global completeness,
writer attribution, historical attribution or qualification.** Every receipt
sets `complete: false`, `global_immutability_proven: false`, and frozen states.

SIGINT, SIGTERM, SIGHUP and SIGQUIT request gate closure and this BCC instance's
`cleanup()`. Failures take the same cleanup path. The program never clears global
audit rules, kprobe events, trace buffers or tracing state. SIGKILL, kernel crash
and abrupt VM termination cannot guarantee a final receipt or Python cleanup;
retain the incomplete directory and have the tracing owner inspect only this
instance's `r_<symbol>_bcc_<pid>` events. Do not run global cleanup commands or
delete evidence. The private source/build/header links from optional provisioning
are separately owned and are not removed by a capture. No unconditional removal
command is provided.

## Coverage and success contract

All hooks below are mandatory, with explicit Linux 6.6 argument positions. BCC
entry handlers omit unused trailing arguments; they never read buffer/iterator
contents. Entry and return probes share `(PID/TID, operation)` context. There is
no PID or UID filter and no dependence on observing an `open` syscall; existing
descriptors are covered. Only this collector's output directory ancestry is
excluded. Directory namespace metadata is included alongside regular file work.

| Route | Linux 6.6 hook and metadata arguments | Successful operation |
| --- | --- | --- |
| Scalar write/pwrite | `vfs_write(file=arg1)` | Return > 0 |
| Kernel write | `__kernel_write(file=arg1)` | Return > 0 |
| writev/pwritev and iter writes | `do_iter_write(file=arg1)` **and** `vfs_iter_write(file=arg1)` | Return > 0; nested duplicate records possible |
| Truncate/ftruncate/O_TRUNC and other attrs | `notify_change(idmap=arg1, dentry=arg2, iattr=arg3)` | Return 0; `ATTR_SIZE` bit 8 gives size-operation credit, other attrs metadata credit only |
| Rename/replacement/exchange | `vfs_rename(renamedata*=arg1)` | Return 0; flags and both entry identities saved; moved source dentry supplies target-after identity |
| Hard link | `vfs_link(old=arg1, idmap=arg2, dir=arg3, new=arg4)` | Return 0; destination identity also captured after success |
| Unlink/rmdir | `vfs_unlink` / `vfs_rmdir`, dentry arg3 | Return 0 |
| Create/mkdir/mknod | `vfs_create` / `vfs_mkdir` / `vfs_mknod`, dentry arg3 | Return 0; negative pre-operation dentry is retained, new inode comes from after metadata |
| Anonymous tmpfile | `vfs_tmpfile(idmap=arg1, parentpath=arg2, file=arg3)` | Return 0; parent before, file inode after; subsequent writes use file pointers |
| Allocate/punch/zero/collapse etc. | `vfs_fallocate(file=arg1, mode=arg2)` | Return 0; mode retained, conservatively no content-write credit |
| Copy range | `vfs_copy_file_range(src=arg1, destination=arg3)` | Return > 0 |
| Sendfile/direct splice | `do_splice_direct(src=arg1, destination=arg3)` | Return > 0 |
| Pipe to ext4 file | `iter_file_splice_write(pipe=arg1, destination=arg2)` | Return > 0; custom non-ext4 splice routes unsupported |
| Generic/ext4 mmap fault | `filemap_page_mkwrite(vmf=arg1)` **and** `ext4_page_mkwrite(vmf=arg1)` | Evidence only for every return; **no content mutation credit** |
| Lifecycle | Raw `sched_process_fork`, `sched_process_exec`, `sched_process_exit` | Child and actual creator identities for fork; exec `old_pid=args[1]`; no argv |

`do_splice_from` is inlined/absent on this running kernel, so the mandatory ext4
pipe-write seam is `iter_file_splice_write`. The latter also occurs inside other
splice paths and can yield duplicate evidence. `vfs_iter_write` alone would miss
writev, which is why `do_iter_write` is mandatory. Missing, inlined, unprobeable,
or verifier-rejected mandatory hooks refuse READY. There is no weaker fallback.

Each paired event retains entry identity, result, pre-operation source and
destination metadata, and available target-after metadata. Identity includes
boot ID, PID/TID, exact kernel group-leader `start_boottime`, parent group-leader
start identity, comm and event-time executable path/device/inode. PF_KTHREAD is
retained explicitly: a null mm alone is not a kernel-thread identity. A normal
exiting user task can record `exiting-user-unavailable` for its executable
without inventing an executable or invalidating ordinary exits. User mutation
events still require their real event-time executable. The fork creator is separate from the
child's `real_parent`, which can differ for CLONE_PARENT/thread cases. No later
`/proc/PID/exe` lookup is used to invent identity for a short-lived event.

For replacement rename, the saved destination dentry can become negative or
displaced, so post metadata comes from the saved **source** after its move.
For `RENAME_EXCHANGE` (flag 2), the entry source object moves to the destination
name, while the entry destination object moves to the old source name. Both
entry identities and flags remain; `target_after` describes the first moved
object, not a second post-exchange observation. Int returns are sign-extended
from s32, byte-count hooks retain signed-long results, and page-mkwrite retains
its unsigned vm_fault_t result.

Dentry paths are leaf-first bounded components (12 components, 47 bytes plus NUL
each). They are mount-relative, not proven host absolute paths. Device/inode
remain primary joins; Linux internal `dev_t` is decoded as major `raw>>20` and
minor `raw&((1<<20)-1)`, rather than compared directly with Python's `st_dev`.
Truncation, kernel read errors and uncertain exclusion ancestry invalidate
supported accounting. Namespace/mount aliases remain explicit limitations even
when no truncation flag appears.

Before/after JSONL inventories retain every observed process identity and parent,
each task's PID/TID/group-leader/task start ticks and executable identity,
per-task descriptor path/stat/whitelisted fdinfo metadata, including thread-private tables,
shared mapping ranges/permissions/offset/device/inode/path, and NSS tree/parent
stat metadata. Snapshot proc start ticks are rounded observations; they do not
replace exact kernel starts. Permission failures, disappearing/reused processes,
descriptor races, symlink aliases and partial traversal are incomplete snapshots.
Descriptor path/device/inode and fdinfo are rechecked, as is the executable
across task observations. Races become errors while other tasks/descriptors/maps
continue. Indistinguishable close/reopen between checks can escape detection.
Symlink/non-directory NSS roots/parents are flagged and not descended; child
metadata walks use directory FD anchors. Receipt phases retain started/completed/
partial state and accumulated errors even after output/deadline interruption.
No atomic snapshot is claimed. UTC estimates use retained monotonic-to-UTC
samples at startup, about once per second while polling, and finish. Steps/drift
over 1ms between samples invalidate accounting; a step that reverses between
samples remains unsupported.

## Loss, output and evidence acceptance

The new output directory is 0700, files 0600, created exclusively, never reused.
The configurable total metadata budget is 128 KiB..256 MiB (default 64 MiB),
with a 64 KiB final-receipt reserve. Overflow stops collection, skips an
unfinishable after snapshot, and records incomplete evidence. Files are
`metadata.json`, `provenance.json`, `trace-start.json`, `ready.json`, `before.jsonl`, `events.jsonl`,
optional `clock.jsonl`, `after.jsonl`, and `receipt.json`; failure may leave only
a subset. No-events is never global immutability evidence.

Provenance includes observed Python source, C template and exact rendered C
SHA-256, Python version/executable and BCC's observed version. It hashes only
diagnostic sources, never NSS contents. The template hash and rendered source
use the same observed bytes. Python's source hash describes on-disk setup bytes,
not proof that identical bytes were loaded earlier. Missing hashes or unexposed
BCC versions remain null with an explicit reason.

Kernel counters cover perf submission errors, context-map failures/overwrites,
unmatched returns, unfinished/deadline pairs, kernel-read/path/identity errors.
User space records perf lost callbacks, validates event ABI/deadlines, and
reconciles submissions against delivered records and reported losses. Submission
errors and perf lost notifications can overlap: any such loss is already
incomplete, and arithmetic disagreement is additional uncertainty, never a way
to cancel a loss. Undrained records or mismatched pair counts are incomplete.

`maxactive=1024` forces tracefs-backed return probes in BCC; the controller
derives this instance's return aliases from its BCC `kprobe_fds`, reads matching
`kprobe_profile` rows before detach, and preserves hits/missed counts. A missing,
unreadable, duplicate or malformed owned row, or nonzero missed count,
invalidates accounting. PMU-backed entry probes need not have profile rows.
Their dispatch/recursion misses are not directly exposed by owned profile rows:
unmatched returns detect some entry misses, but counters cannot rule out paired
omissions. Detected-loss accounting therefore does not prove zero dispatch loss.
No assumption that a blank profile means zero loss is permitted. Profile/map
reads are observations around gate closure, not a globally atomic kernel
snapshot; evidence outside the closed interval is not credited.

A prospective writer claim needs all of the following together:

1. A successful ordinary target mutation, with valid pre-operation device/inode/
   path and any needed created/replaced target identity. Failed/queued returns,
   zero-byte writes, page faults, handles and timestamps receive no such credit.
2. The event-time boot/PID/TID/group-leader start/executable identity and raw
   fork/exec chain, including creator identity and exec old PID.
3. Exact correlation to the separately authorized workload's **owned process
   PID/start identity from its execution receipt**. Being beneath the same shell,
   having a matching comm, or sharing a time interval does not establish owned
   SDK attribution; unrelated background work can share that shell.
4. Loss accounting and before/after metadata sufficient for that claim, with
   all limitations and any incomplete fields retained. A lossy positive event
   may guide investigation but cannot support a complete-capture assertion.

Successful operation fields describe kernel return semantics; they do not prove
that particular bytes changed, which this collector never reads. Already-writable
shared mmap stores, async completion/io_uring, filesystem-specific and DAX routes,
non-ext4 semantics, external Windows/VM/host writes, mount aliases, path limits,
and snapshot races prevent global completeness. A successful ordinary operation
can identify a prospective writer when its identity and correlation are sound.
Absence of records cannot prove absence of all writers. Manual evidence review
is required, and no prospective result resolves either historical writer.

Primary signature/implementation sources: Linux 6.6
[read/write](https://github.com/torvalds/linux/blob/v6.6/fs/read_write.c),
[attributes](https://github.com/torvalds/linux/blob/v6.6/fs/attr.c),
[namespace operations](https://github.com/torvalds/linux/blob/v6.6/fs/namei.c),
[splice](https://github.com/torvalds/linux/blob/v6.6/fs/splice.c),
[filemap faults](https://github.com/torvalds/linux/blob/v6.6/mm/filemap.c),
[ext4 faults](https://github.com/torvalds/linux/blob/v6.6/fs/ext4/inode.c),
[sched tracepoints](https://github.com/torvalds/linux/blob/v6.6/include/trace/events/sched.h),
and BCC [Python attachment/cleanup](https://github.com/iovisor/bcc/blob/v0.35.0/src/python/bcc/__init__.py)
and [libbpf tracefs aliases/maxactive](https://github.com/iovisor/bcc/blob/v0.35.0/src/cc/libbpf.c).
