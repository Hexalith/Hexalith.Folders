---
title: 'Repair Story 1.17 live collector compilation and accounting'
type: 'bugfix'
created: '2026-10-31'
status: 'done'
route: 'oneshot'
context: []
---

<frozen-after-approval reason="user authorized narrow collector-only live-validation fixes">

## Intent

The first authorized live attempt fails before attachment because LLVM lowers
large metadata resets to unsupported BPF memset calls. Replace those resets with
explicit bounded stores without changing event layout, identity, coverage,
deadlines, loss accounting, output policy or frozen qualification states.
Retain the failed attempt and validate the corrected program on the actual
kernel within the session's aggregate 900-second tracing bound. No project
builds, resources, real NSS mutations, qualification or Git writes are allowed.

</frozen-after-approval>

## Implementation Notes

- `trace.bpf.c`: replace the two large builtin metadata resets with volatile,
  unrolled 64-bit zero stores. Both structures are aligned and sized to 64-bit
  words; retain compile-time size checks. The volatile stores prevent LLVM from
  recreating an unsupported memset call.
- First attempt: requested 120 seconds, compiler terminated with exit 1 before
  any BPF instance/READY/trace-start or final receipt. Preserve its partial files.
- Existing 54 offline tests pass normally and optimized before this correction.
- This is a collector repair only; the existing preparation SPEC remains intact.

- Actual compilation exposed Linux perf raw-sample padding: retain 3308-byte
  delivery sizes while decoding the compile-time-checked 3304-byte metadata ABI.
  Correct the BCC 0.35 loss callback to its single argument and test both paths.
- All-process inventory on this host has 583,160 per-task descriptor records.
  Kernel-confirmed KCMP_FILES sharing now emits a table reference, bracketed by
  comparisons; unknown/private tables still enumerate. Task mappings remain
  inventoried. Tests retain private tables and detect concurrent unsharing.
- Live BCC reflection drops the second array dimension in the pair-map leaf.
  Count keys without deserializing leaf values; the fake map rejects unsafe
  value iteration. Preserve the crashed attempt without inventing a receipt.
- After-snapshot output exhaustion now sets the overflow flag as well as
  retaining its incomplete snapshot/error. Actual overflow, SIGTERM, paused
  deadline, loss callbacks, owned profile observations and cleanup produced
  retained receipts after the accounting correction.
- Final tests: 58/58 normal and optimized. A standalone snapshot still exhausts
  256 MiB and encounters process churn. No READY or synthetic mutation-suite
  execution is credited. Overall live validation remains incomplete.
- Final helpers reject artifact reuse. An actual synthetic symlink refusal
  preserves its outside sentinel metadata and creates no capture output.
- The skill's commit/status-transition steps are overridden by the user's
  explicit no-Git-write and frozen tracker/qualification instructions.

## Review Triage Log

Independent one-shot Blind Hunter review reported three initial concrete issues
and one follow-up; each was checked against the actual helper.

| Finding | Verdict and disposition |
| --- | --- |
| Fixture descendant symlinks could redirect pre-capture truncation | Medium; patched no-follow directory anchors, protected ancestors and single-link regular-file checks. Actual refusal check preserves the synthetic outside sentinel. |
| UID child nonzero exit could be recorded as success | Medium; patched explicit nonzero failure classification. The fixture route remains unexecuted. |
| UID child record held only the parent's identity | Medium; patched child-generated PID/TID/start/UID/executable receipt tied to Popen ownership. The fixture route remains unexecuted and receives no identity credit. |
| Repeating a helper could overwrite earlier logs/receipts | Medium; patched exclusive creation and explicit refusal before capture/mutation. Actual reuse checks retain all existing evidence hashes. |

The reviewer confirmed the first three fixes, keys-only accounting and the
after-snapshot overflow flag. The final preservation fix was independently
exercised without tracing. No deferred change or authority transition follows.
