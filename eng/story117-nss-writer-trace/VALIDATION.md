# Offline validation — 2026-10-31

This records preparation evidence only. No dependency was installed; no BCC C
compilation, kernel probe, audit rule, privileged capture, live mutation fixture,
SDK/build/qualification, resource start, deployment or Git write was performed.

Initial implementation checks:

| Command | Result |
| --- | --- |
| `python3 -B -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'` | 35/35 pass; 0.018 seconds |
| `python3 -B -O -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'` | 35/35 pass; 0.018 seconds |
| `python3 -B eng/story117-nss-writer-trace/collector.py check` | Expected exit 2; `bcc` absent and matching prepared headers absent; `attached=false`, `tracing_imported=false`, BTF present |
| `python3 -B eng/story117-nss-writer-trace/collector.py --help` and `capture --help` | Exit 0; no capture |
| `python3 -B eng/story117-nss-writer-trace/collector.py render > /dev/null` | Exit 0; default zero deadlines |
| Python stdlib `ast.parse` for both Python files, imported `render_source(0)`, ctypes layout inspection | Pass; rendered source 17,567 bytes, event ABI 3,304 bytes, 19 mandatory paired hooks |
| Byte-level LF/final-newline/trailing-space inspection of all new implementation files | Pass; existing SPEC not rewritten |

The offline tests cover safe CLI modes, duration/output policy, modes and byte
reserve, device encodings, binary identity/result decoding, metadata-versus-size
and page-fault semantics, exec old PID/fork creator identities, missing/lost
accounting, and simulated mandatory-probe failure and cleanup ordering. Fake BCC
does not compile C or call kernel tracing facilities. A passing simulation is
not evidence that attachment or actual cleanup works.

Independent parent verification and preservation results are recorded below.
The initial implementation's live blocker is exactly:

```text
Python BCC module bcc (Ubuntu package python3-bpfcc; /usr/bin/python3)
/lib/modules/6.6.87.2-microsoft-standard-WSL2/build (matching prepared utsrelease.h/autoconf.h required; BTF is not a substitute)
```

Kernel verifier acceptance, all entry/return/raw tracepoint attachments,
event-time executable availability, all mutation routes, perf-loss behavior,
actual owned kretprobe profile aliases/missed counts, signal/overflow cleanup,
snapshot completeness, and suspend/deadline enforcement remain **unverified**.
The README supplies prepared provisioning and capture commands for separate
future authorization. None were executed here.

Every diagnostic receipt preserves `qualification=incomplete`, parent
`in-progress`, tracker `backlog`, `V1Only`, unset T0, null activation authority,
and staging `0/14`. Both historical NSS writers remain unresolved. Unsupported
routes and races prevent global immutability proof, regardless of event count.

## Review corrections — focused offline checks

The 35-case results above describe the initial implementation. Review found the
self-enumeration FD defect and missing executable/FD race, callback and traversal
coverage. The new regressions exercise actual snapshot functions against a
synthetic per-task proc tree, and perform one read-only real self-process
metadata snapshot. They never read NSS contents, attach probes or run a live
mutation fixture.

| Focused command/check | Actual result after corrections |
| --- | --- |
| `python3 -B -m unittest discover -s eng/story117-nss-writer-trace -p 'test_collector.py'` | 54/54 pass; final run 0.413 seconds |
| `python3 -B -O -m unittest discover -s eng/story117-nss-writer-trace -p 'test_collector.py'` | 54/54 pass; final run 0.180 seconds |
| In-memory source mutation removing callback uncertainty propagation, running `SimulatedCaptureTests.test_simulated_callback_uncertainty_persists_and_refuses_accounting` | Expected assertion failure observed |
| In-memory AST mutation removing descriptor enumeration, running `SnapshotTests.test_actual_proc_traversal_includes_thread_private_fds_and_shared_maps` | Expected assertion failure observed |
| In-memory AST mutation removing shared-map enumeration, running that same focused traversal regression | Expected assertion failure observed |
| Extract each README Bash fence and pass it to `bash -n`; parse Python heredocs with stdlib `ast.parse` | All 6 Bash blocks and 2 Python heredocs pass syntax checks; no embedded command executed |

Snapshot coverage now asserts thread-private descriptor tables, shared mappings,
FD path/stat/fdinfo races, executable changes, accumulated partial-phase errors,
and refusal to descend a replaced NSS root symlink. Output tests perform a
directory-to-symlink swap before descriptor chmod and confirm the unrelated
target's permissions remain unchanged. Fake BCC delivers binary events and lost
callbacks through the registered functions; simulations assert persisted event
and receipt semantics, ABI/decode errors, overflow, missing owner, signals and
collector-only cleanup requests. These simulations are not actual kernel
attachment, loss injection or BCC cleanup validation.

All 18 review findings/verdicts and grouped corrections are recorded in the
SPEC's Review Triage Log. Its frozen intent is unchanged. The prepared runbook
uses a root-owned private parent, separate shell-owned log, the exact 900-second/
64-MiB next capture, a readiness handshake, pidfd stop and errexit-safe status
handling. Minimal `python3-bpfcc` installation is separate from optional header
build tools; none of those proposed commands ran.

Runtime metadata now preserves observed diagnostic Python/C/rendered-source
hashes and BCC version (or explicit unknown). No store bytes are hashed. Kernel
rename/return-width/exit-identity corrections are covered offline for policy,
decoding and source structure only. Live C compilation/attachment, all mutation
routes, actual dispatch/perf loss, profile accounting, BCC cleanup and suspend
behavior remain unverified. Entry PMU dispatch/recursion misses and paired
omissions cannot be ruled out by these counters. The independent parent verification and preservation audit follow below.


## Independent parent verification — 2026-10-31

The final code was checked independently after all three review layers and the
bounded corrections. These are preparation results, not a live tracing receipt.

| Exact command or read-only method | Parent result |
| --- | --- |
| `python3 -B -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'` | 54/54 pass, 0.169 seconds |
| `python3 -B -O -m unittest discover -s eng/story117-nss-writer-trace -p 'test_*.py'` | 54/54 pass, 0.148 seconds |
| `python3 -B eng/story117-nss-writer-trace/collector.py check` | Expected exit 2; exactly the missing BCC/prepared-header blockers shown above; no BCC import or attachment |
| `python3 -B eng/story117-nss-writer-trace/collector.py --help`, `capture --help`, and `render > /dev/null` | Exit 0; no capture |
| stdlib AST parse, inert module import/render, byte-level LF/newline/whitespace check; `bash -n` on each prepared block and AST parse of heredocs | Pass; 6 Bash blocks, 2 Python heredocs; no prepared command executed; rendered zero-deadline C is 18,637 bytes, event ABI 3,304 bytes; script mode 0755 |
| Read-only `snapshot_process(os.getpid(), rows.append)` | 9 metadata records, zero snapshot errors; BCC remains absent from imported modules |
| In-memory removal of callback uncertainty propagation | The exact callback regression fails once as expected, zero test errors |
| In-memory AST removal of descriptor enumeration | The exact per-task traversal regression fails once as expected, zero test errors |
| In-memory AST removal of shared-map enumeration | The same traversal regression fails once as expected, zero test errors |
| stdlib parse of `/sys/kernel/btf/vmlinux` and symbol-name lookup in `/proc/kallsyms` | 135,846 BTF types inspected; all 19 selected hook symbols/prototypes present and used argument prefixes match. `do_splice_from` is absent; selected `iter_file_splice_write` is present and matches. Presence is not attachment evidence. |
| SHA-256 comparison against the pre-change preservation baseline | All 36 existing production-model receipts, archives and protected/input documents unchanged |
| `GIT_OPTIONAL_LOCKS=0 git -c submodule.recurse=false diff --check` and status inspection | Exit 0; no tracked changes; only the seven new diagnostic files. New untracked file whitespace was also checked directly. |

Root HEAD remains `fdcf5542e6ec0aabcbc2d7386907e276a4ec1b86`. The original
production-model spec/results, historical receipts, parent in-progress state,
tracker backlog, V1Only, null T0/activation authority and all 14 unexecuted
staging checks are unchanged. The new diagnostic SPEC's frozen intent matches
the reviewed copy. Its done status means preparation/review only.

The original two historical timestamps remain unattributed. Successful future
ordinary mutation events may identify prospective writers with the documented
identity/correlation evidence. They cannot prove either historical writer.
Actual eBPF compilation/verifier acceptance, attachment, mutation capture,
shared-mapping store attribution, dispatch/perf loss accounting, kernel cleanup,
suspend deadlines and host-wide snapshot completeness remain unverified or
explicitly unsupported as specified in README. No live capture or SDK work ran.

Final diagnostic source/command bindings (observed file bytes, no store bytes):

| File | SHA-256 |
| --- | --- |
| `collector.py` | `4587ae4d695326587ad0e559389fd43a0ced5bf407178b2ecb10c6474298ae37` |
| `trace.bpf.c` | `75bed935415d5f942b9f046260d55012f82cf8751d2f3231246b1fd70125952d` |
| `test_collector.py` | `22673c508d148772fdc2e49cd845250b8f391b37672db20be4315c11f0e298cd` |
| `README.md` | `eeeac35ccf3c7fe57dc34062c228dcfba46fd36d16542eeea2b125fd7a8dc8c4` |
