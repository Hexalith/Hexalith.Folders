---
title: 'Story 1.17 prospective NSS writer diagnostic'
type: 'chore'
created: '2026-10-31'
status: 'done'
route: 'dispatch'
baseline_commit: 'fdcf5542e6ec0aabcbc2d7386907e276a4ec1b86'
tracker_policy: 'no-automatic-transition'
context:
    - '/home/administrator/projects/hexalith/folders/AGENTS.md'
    - '/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/spec-1-17-qualify-production-source-model-build.md'
    - '/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/story-1-17-production-source-model-results.md'
---

<frozen-after-approval reason="user explicitly authorized preparing and offline validating diagnostic files">

## Intent

Prepare an executable, reviewable, metadata-only prospective host-wide Linux NSS writer collector, exact provisioning/capture/cleanup instructions, and meaningful offline validation. Historical writers at 2026-10-30 16:39:27 UTC and 16:54:04 UTC remain unresolved. Historical logs are insufficient; tools are absent. New observation cannot prove either historical writer.

## Boundaries & Constraints

Add files only beneath `eng/story117-nss-writer-trace/`. Preserve all existing files, receipts and user changes. Do not install dependencies, attach tracing, enable auditing, build/qualify, start resources, publish/deploy or write Git state. Do not run privileged capture or a live mutation fixture now. Qualification stays incomplete, parent in-progress, tracker backlog, V1Only, T0 unset, activation authority null, staging 0/14. The user has already authorized the concrete preparation; no additional planning approval is required.

Future capture is explicit opt-in only, root in the host PID/mount namespaces, at most 900 seconds INCLUDING setup (kernel monotonic event deadline independent of teardown). Python CLI defaults to help; check/render modes must not import/instantiate BPF or attach. Output is a new mode-0700 directory outside the monitored parent; files mode 0600. Fail on reuse. No argv, environment, file contents, certificate/key bytes or buffers are read/recorded. Process comm/executable paths, file names and metadata are allowed. No workload is launched by the collector.

Capture all processes/UIDs, including preexisting descriptors. Use Python BCC + explicit Linux 6.6 VFS entry/return probes; no PID/UID filter. Host-wide regular-file mutation collection, with only the collector output directory excluded to prevent recursive events. Save pre-operation source AND destination paths/device/inode; emit successful mutations with return value, UTC calibrated from monotonic samples, PID/TID, kernel group-leader start_boottime, parent start identity, executable path/device/inode, boot ID. Track raw sched fork/exec/exit (exec old_pid too), and before/after all-process identities, NSS tree/parent metadata, existing descriptors and shared mapping metadata. Do not rely on post-event /proc lookup for short-lived executable identity.

Mandatory hooks cover scalar writes, vector writes, kernel writes, successful ATTR_SIZE changes (including O_TRUNC/ftruncate), rename/replacement, link/unlink/create/mkdir/rmdir, fallocate/copy/splice. Match exact 6.6 signatures; fail readiness if a mandatory hook cannot attach. Generic AND ext4 page-mkwrite probes are evidence-only, never successful content mutations. Already-writable mmap stores, async completion/io_uring bypasses, DAX/other filesystems, external VM/host writers, mount aliases/path truncation, snapshot races and other unsupported routes must be explicit. A successful ordinary mutation can identify a prospective writer; no-events never proves global immutability.

Account for perf lost callbacks, submission failures, context-map failures/overwrites, unmatched returns, unfinished pairs, kernel read/path truncation flags and owned kretprobe missed counts from kprobe_profile before detach. Loss, incomplete snapshots, unsupported filesystem, missing probes or uncertain accounting must invalidate completeness. Bound output size and stop on overflow with incomplete result. Failures/signals detach only this collector's probes using BCC cleanup; never clear global audit/tracing state.

## I/O & Edge-Case Matrix

| Scenario | Required outcome |
| --- | --- |
| Help/check/render without tools | Offline, no tracing; exact missing dependency report |
| Duration outside 1..900, reused/unsafe output | Refuse before BPF attachment |
| Successful write/truncate/rename | Keep success result and full entry identity; failed/queued return gets no content mutation credit |
| Page-mkwrite, open handles, timestamps/prevention only | No writer proof |
| Lost events, pairing errors, missing profile, partial path/snapshot | Incomplete, never clean/qualified |
| Historical or authority claims | Unresolved/incomplete and frozen states retained |

</frozen-after-approval>

## Code Map

- `collector.py`, `trace.bpf.c`: new CLI/controller and BCC program. Python stdlib for offline checks. Existing source-distribution scripts are context only; never execute them.
- `test_collector.py`: pure offline CLI/policy/decoding/loss tests; fake BCC allowed but distinguish simulation from kernel validation.
- `README.md`: exact current-host provisioning, capture with caller shell ownership/ready handshake, stop and cleanup; coverage matrix and evidence acceptance contract.
- `VALIDATION.md`: exact offline outcomes and unverified kernel behavior; parent fills after review.
- Existing `.editorconfig`, `.gitattributes`, build files, tests guidance and architecture were inspected by parent. Scripts LF; use nested new `.editorconfig` to set LF for C/Python if needed, without editing root configuration.

## Tasks & Acceptance

- [x] Implement the collector and bounded failure/cleanup behavior.
- [x] Add executable offline tests for policy, success semantics, identity and losses; run normally and optimized without BCC.
- [x] Document exact commands, minimum dependencies and unsupported coverage. On this host `bcc`, bpftrace/audit tools and `/lib/modules/6.6.87.2-microsoft-standard-WSL2/build` are absent; BTF exists. Never imply BTF supplies BCC headers. Provisioning commands are prepared only.
- [x] Review the actual new files, record validation honestly, verify only additive diagnostics and no authority transitions.

Given available offline tooling, validation must run without tracing. Given missing dependencies, live capture remains unverified with an exact blocker. Given capture evidence, attribution requires successful mutation + target identity + event-time executable/start identity + fork/exec correlation + loss accounting and before/after metadata. Receipt preserves unsupported coverage and cannot promote qualification.

## Implementation Notes

Planning used a read-only design subagent and Linux 6.6 primary sources. Use `do_iter_write` or `vfs_writev` in addition to `vfs_iter_write`; the latter alone misses writev. `notify_change` dentry is argument 2, iattr argument 3. Rename argument 1 is `renamedata*`. Path components may be mount-relative; preserve device/inode and explicit uncertainty rather than invent full paths. Kernel checks are unverified until separately authorized tooling/capture work.

## Review Triage Log

All three independent layers reviewed `/tmp/story117-nss-review-t73oumtl.diff`.
The following verdicts precede grouping. Fixes remain inside this new diagnostic;
no existing receipt, authority file, or tracker is amended.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| B01 | high | Parent reproduced FileNotFoundError on the collector's own enumeration FD; before snapshot/handshake cannot succeed. Patch lazy live enumeration and regression. |
| B02 | medium | Null mm can also describe an exiting user task. Event JSON currently says has_user_mm=false correctly, but C/documentation imply kernel-thread identity and do not distinguish expected exit unavailability. Patch explicit PF_KTHREAD state and exit semantics. |
| B03 | medium | Signed int returns are read as full-width RAX; upper bits are not the int result contract. They get no false zero-success credit, but failure errno metadata can be wrong. Patch per-hook return normalization. |
| B04 | medium | Rename flags are not retained; replacement/exchange cannot be interpreted reliably. Patch flags and correct moved-object post identity. |
| B05 | medium | mkdir/chmod/open use separate path operations; a replaced directory or ancestor can redirect privileged chmod. Patch descriptor-anchored creation/validation and trusted-parent policy, with demonstrated swap regression. |
| B06 | medium | FD path/stat/fdinfo and executable observations can change without PID/start reuse. Patch stable identity comparisons and regression. |
| B07 | medium | Only leaders' FD tables are enumerated; unshared thread tables can be omitted. Patch per-task descriptor inventory and explicit thread identity. |
| B08 | medium | A raising snapshot loses its local error list before assignment. Patch incremental errors and explicit partial/completed phase state. |
| B09 | medium | Capture metadata lacks diagnostic source/rendered hashes and BCC version. Patch observed-source/runtime provenance without reading store contents. |
| B10 | medium | Simulation bypasses callback and snapshot paths, so the deterministic self-snapshot bug and uncertainty propagation escape. Patch meaningful offline callback/snapshot/error/stop tests. |
| E01 | high | Same independently reproduced self-enumeration defect as B01; same patch. |
| E02 | medium | vfs_rename performs d_move/d_exchange before return; saved destination dentry may be displaced/negative. Patch saved target to source, retain entry destination and flags. |
| E03 | medium | Same independently identified descriptor identity race as B06; same patch. |
| E04 | high | Stop command checks start then calls os.kill; PID reuse between them can signal unrelated process as root. Patch pidfd_open + identity verification + pidfd_send_signal, with boot identity check. |
| V01 | medium | Pre-verified removal of descriptor/shared-map loops leaves 35 tests green. Add actual synthetic proc traversal assertions, including per-task tables. |
| V02 | medium | Pre-verified removal of callback uncertainty propagation leaves 35 tests green. Deliver binary events through the registered callback and assert persisted event/receipt refusal. |
| P01 | medium | Provisioning example enables errexit; later expected failed wait/handshake assignments can exit before status capture. Patch shell status handling to work with errexit. |
| P02 | low | apt-cache depends confirms python3-bpfcc pulls libbpfcc, libclang-cpp21 and libllvm21. Split minimum capture dependency command from optional header-preparation tools; provide a full 900-second next capture example. |

All findings route to bounded corrections of the demonstrated implementation or
verification defects. Preserve successful return semantics, no buffer/argv/env
reads, both time deadlines, mandatory probes, event-time identities, loss
refusals, and the unchanged historical/authority boundaries. No live kernel
verification is inferred from these corrections.

### Implementation corrections — 2026-10-31

All findings above have bounded implementation corrections; parent independent
verification remains pending. Frozen intent and historical/authority states are
unchanged.

| Findings | Implemented correction and focused evidence |
| --- | --- |
| B01, E01 | Lazy live scandir inspection keeps self-enumeration FDs open; actual read-only self-snapshot regression passes. |
| B02 | PF_KTHREAD is explicit; expected exiting-user mm/executable unavailability is distinct and valid for exit only. Mutation identities still require their executable when applicable. |
| B03 | Int hooks normalize through s32, byte-count hooks remain signed long, and vm_fault_t remains unsigned; decoding/normalization regressions pass. |
| B04, E02 | Rename flags and both entry identities are retained; source dentry supplies moved target-after metadata, with documented exchange interpretation. |
| B05 | Trusted/private parent policy and no-follow descriptor anchors replace path chmod; opened/named identity checks and a symlink swap regression preserve unrelated target permissions. |
| B06, E03 | Descriptor path/stat/fdinfo bracketing and executable before/after identity checks detect races, record errors and continue other inventory. Synthetic races exercise actual traversal. |
| B07, V01 | Per-task descriptors carry PID/TID/group-leader/task start identities; actual synthetic traversal asserts private FD tables and shared mappings. Removing either inventory loop causes its regression to fail. |
| B08 | Snapshot phase state and errors are retained incrementally through deadline/output interruption; receipt records started/completed/partial, counts and error samples. NSS root/parent aliases refuse traversal. |
| B09 | Runtime provenance records observed Python/C/exact rendered C SHA-256 and observed BCC version, retaining missing/unknown values honestly; no NSS contents are hashed. |
| B10, V02 | Fake BCC now delivers binary events/loss callbacks and uses actual synthetic snapshots; persisted uncertainty, ABI/decode error, overflow, owner exit and signal regressions pass. Removing callback uncertainty propagation causes the regression to fail. |
| E04 | Prepared stop code uses pidfd_open, boot/PID/start/owner verification and pidfd_send_signal, handling already exited state; syntax checked only. |
| P01, P02 | Prepared commands guard expected handshake/wait failures under errexit, split minimal python3-bpfcc from optional header tools, and give the exact 900-second/64-MiB invocation with root-private output parent and separate shell log. |

Only focused offline diagnostic tests and syntax checks ran. Live compilation,
attachment, mutation-route behavior, actual dispatch loss/cleanup and suspend
enforcement remain unverified. Entry PMU dispatch misses/paired omissions remain
an explicit coverage limit; no detected-loss accounting proves zero dispatch loss.

### Parent completion — 2026-10-31

Independent parent checks pass 54/54 normally and 54/54 optimized. Three separate
in-memory deletions (callback uncertainty propagation, descriptor enumeration,
shared-map enumeration) each produce exactly one expected assertion failure and
zero test errors. Read-only real self-inventory completes with nine records and
zero errors. All 19 selected hooks are present in running-kernel BTF/kallsyms,
and their argument prefixes match; this does not prove probeability.

Two additional parent follow-ups are resolved: NSS root/parent replacement or
symlink aliases refuse descent with incomplete snapshot metadata (covered by
synthetic traversal), and entry-PMU dispatch/recursion misses plus paired
omissions are explicit unsupported coverage. No zero-dispatch-loss claim is
made. The frozen diagnostic intent matches the pre-review copy exactly.

Only this new preparation spec is done. Qualification remains incomplete, the
existing parent remains in-progress, tracker backlog, V1Only, T0 unset,
activation authority null and staging 0/14. All 36 existing production-model
receipts/artifacts/control-file hashes match the pre-change baseline. No tracker
transition, staging, commit, branch, push or other Git mutation was performed.
Live kernel verification remains expressly unexecuted and unverified.

## Verification

Run Python tests normally/optimized, help/check/render and AST parsing. Do not compile/attach eBPF through BCC. Parent checks new-file whitespace and existing protected file hashes. No SDK/build tests are relevant or authorized.
