# Story 1.17 — loader attempt stopped; exact harness repair proposed

**The approved adapter ran once. Root rebinding passed and sandboxed SDK `--version` returned `10.0.402`. The next sandbox setup failed before SDK `--info` launched. Neither loader case nor the compiler ran; EventStore/Redis compatibility remains unresolved.**

The [execution receipt](evidence/story-1-17-cli-4-38-loader-execution-2026-10-26.json) binds the real invocation, both retained private directories, full explicit SDK environment, filter bytes, stdout/stderr and timestamped process/certificate observations. Its **315 archived file members** are verified. No retry or repair occurred under the consumed one-attempt authority.

## Actual result and preserved boundaries

Root remains `43423a9849c82a3a526590b0fa6e613768e7eb24`; root index SHA256 remains `62459e20b5115ebea71409210950651c7d3bfd32c20729dd69cde0d62d0a666d`. All twelve direct revisions and indices match, including Parties `986a369ec26f6c5194593eb7bce12955eda54149`. The original approved probe, driver, plans, specs, inputs and all approvals remain byte-identical. No repository or dependency patch was applied.

The single adapter invocation ran from `2026-10-26T14:40:09.332893+00:00` to `2026-10-26T14:40:15.169072+00:00`, exit1. The first sandbox launch (`sdk-before-compiler`) completed exit0 with exact stdout `10.0.402`. The second (`sdk-info`) exited1 with empty stdout and:

```text
bwrap: Unable to set up system call filtering as requested: prctl(PR_SET_SECCOMP) reported EINVAL. (Hint: this requires a kernel configured with CONFIG_SECCOMP and CONFIG_SECCOMP_FILTER.)
```

That is a sandbox-setup failure, not SDK output or a model/host compatibility diagnostic. The driver stopped at its SDK-info assertion, before MSBuild identity, the immediate compiler gate, compilation and either loader case. Two sandbox invocations were attempted; exactly one SDK identity command completed. There was no model rebuild, restore, CLI/AppHost entrypoint or ComposeRun invocation.

The 503 bound SDK files, 238 copied host files, 45 copied model files, package/archive inputs and prior owner-bound records match. Repository state and inspected certificate-store metadata compare equal. No certificate contents were read, certificate cleanup performed or certificate-installation message observed. Sampled process observations and final owned-process checks found no survivors; they do not prove absence of transient or escaped processes or whole-host preservation. The failed second setup does not establish its containment completed.

## Source-based diagnosis

The harness I prepared opens one `SECCOMP_FD` and passes that same descriptor to every `bwrap` invocation. It never rewinds the descriptor. The retained commands both use FD3 and the same unchanged 120-byte filter, SHA256 `92b4ad47fbfa2d39d14bcfc40dc3354a2adabcb632a0c0cbe1b1c017b59c3e48`.

Upstream Bubblewrap `seccomp_program_new` calls `load_file_data(fd)` and sets the program length from the bytes read. [Bubblewrap source](https://github.com/containers/bubblewrap/blob/v0.11.1/bubblewrap.c#L246-L280) and [its read loop](https://github.com/containers/bubblewrap/blob/v0.11.1/utils.c#L569-L610) show a read through EOF with no seek. Linux documents that inherited descriptors share the underlying open-file description and offset. [fork(2)](https://man7.org/linux/man-pages/man2/fork.2.html)

Consequently, the first child can advance the parent's shared offset to EOF; a later launch receives no filter instructions unless the offset is reset. This is a concrete harness defect and a strong explanation of the observed sequence. **The actual descriptor offset was not logged**, and installed Ubuntu Bubblewrap `0.11.1-1ubuntu0.3` has not been proven byte/source-equivalent to the retained upstream tag. This remains a source-derived diagnosis, not a measured historical offset or an executed repair result. The generic kernel hint alone does not establish missing kernel support; the first sandbox succeeded.

## Exact unapplied repair

The [four-line proposed patch](story-1-17-cli-4-38-filter-reset-proposal-2026-10-26.patch) records the current offset, resets it to zero, asserts the launch position, and persists the record before each existing sandbox call. Python documents this positioning behavior in [os.lseek](https://docs.python.org/3.14/library/os.html#os.lseek).

```python
record["seccompOffsetBeforeReset"] = os.lseek(SECCOMP_FD, 0, os.SEEK_CUR)
record["seccompOffsetAtLaunch"] = os.lseek(SECCOMP_FD, 0, os.SEEK_SET)
assert record["seccompOffsetAtLaunch"] == 0
write(WORK / (label + ".prelaunch.json"), record)
```

The sole proposed driver delta is four additions, zero deletions, in `run()` immediately before launching the existing process. Before SHA256: `515acde8c304d5ab71eb44a7d84fde1a247ada176c9121eb07e4ecb6c8927463`; proposed after SHA256: `ae559715da8b3e220703d4e229776289cf4bfdb3d098fa0fc15a4c0244b4bdba`. The after-bytes were materialized only for review and parsed as Python. The approved repository driver and failed-run copy remain unchanged. The patched driver has not executed.

No filter instruction, syscall denial, namespace, mount, environment variable, SDK/global.json, catalog, dependency pin, compiler command, model input or loader demand changes. No kernel or package update is justified. Rewinding a shared descriptor relies on the existing sequential execution; any future parallelization would need independent descriptors. A seek or assertion failure stops before that launch. Rollback is abandonment of the new private copy, retention of its evidence and continued use of the unchanged accepted-tool identity.

## One decision for Jerome

Authorize **only this private-copy repair and one fresh execution of the same bounded two-case experiment**, using the [exact fresh-attempt plan](story-1-17-cli-4-38-filter-reset-plan-2026-10-26.json) and [guarded proposed adapter](evidence/story-1-17-cli-4-38-filter-reset-proposal-2026-10-26/execute_after_filter_reset_authorization.py). The original one-attempt authority is consumed; it does not authorize changing driver bytes or retrying. Nothing is applied or executed now.

The fresh plan preserves the root-recording predicate's ancestry, complete-delta and exact-byte rules, extending its explicit path list only to these actual execution records and this repair packet. This permits a commit recording the reviewed artifacts while rejecting any operational/source/pin/gitlink changes. There is no wildcard exemption. It derives the same two private root fields and verifies every other input value against the original manifest.

After new authorization, the adapter will copy the original probe unchanged and materialize the repaired driver only in a fresh `/tmp/story117-cli438-filter-reset-20261026-*` directory. It verifies the before hash, single exact patch anchor, after hash and the actual patch-file binding. Both original approvals, the failed receipt and the new approval are retained separately; the new approval is the authority for the fresh attempt. The original approved packet is immutable.

The exact outer command is in the plan: `timeout --signal=TERM --kill-after=5s 600s <bound Python> -I -B <bound proposed adapter> --authorization <separately recorded fresh approval>`. All absolute paths, exact adapter/patch/driver hashes and input revisions are bound there. The existing driver still performs the asserted immediate SDK identity/hash gates, explicit environment and certificate prevention/observations, filter/mount/network/PID isolation, process observations and per-command deadlines. It compiles only LoaderProbe, then runs cold and host-first once each in fresh processes. No application entrypoints, methods, runtime prerequisites, run/debug or mapping are introduced.

Expected new evidence includes offset0 before the first sandbox read and a reset to0 at every later launch; a later pre-reset offset120 would support the diagnosis. Unexpected offsets/results are recorded rather than rewritten. Passing sandbox setup alone does not pass the SDK gate. Only actual request/path/hash/version/context/failure observations can address the loader questions; no API or ordinary-run acceptance follows automatically. Any unexpected gate, timeout, drift or survivor stops the lane without retry, changed filter, alternate version or unsandboxed fallback.

The new adapter and original driver's output directories must both be archived with an actual manifest/receipt, using new filenames so the failed attempt remains immutable. No future execution result is created in this packet. Cleanup remains limited to those newly allocated exact directories after archival verification, inventory and owned-process checks; no automatic deletion, prior-directory removal, shared-cache or certificate-store cleanup is permitted.

The historical immediate SDK gate remains incomplete. This attempt's initial version check does not repair that record or substitute for the unexecuted immediate compiler/load gates. All earlier review limitations survive. Ordinary public CLI prerequisites and genuine command-produced mapping/ContentHash remain separate from hosted graph fixtures. EventStore/Tenants/Memories dependency policy is unchanged. CLI4.35.2 remains historical accepted and4.38.0 a candidate; parent **in-progress**, tracker **backlog**, **V1Only**, unset **T0**, null **activation authority**, and **0/14 staging checks** remain unchanged. No tuple, package or hosted acceptance is recorded.

## Independent review and guard verification

Three independent review layers examined the actual receipt and unexecuted repair. The proposal now requires a hash-bound Python interpreter with `-I -B`, rejects optimized/nonisolated execution before assertion gates, and launches the inner driver through the same interpreter. It rejects assume-unchanged/skip-worktree flags, rehashes recording bytes immediately before launch and after execution, and preserves prelaunch/error/signal audit records. SIGKILL can still truncate finalization and must be reported as partial evidence.

A private atomic claim keyed to the exact new approval hash prevents duplicate and concurrent use under the normal retained-ledger workflow. Claims remain consumed after failures and are excluded from cleanup; deliberate deletion or loss of the ledger is outside that guarantee and requires a fresh owner decision. This creates only the explicitly proposed private claim metadata in addition to the two fresh output directories.

Thirteen pure guard tests cover permitted recording states, unrelated files, non-addition/mode changes, hidden index flags, optimized Python, same-path byte mutation, duplicate/concurrent claims, unsafe claim paths and the guard-to-launch connection. They use synthetic data and temporary directories; no adapter, Git repository mutation, sandbox, SDK or target assembly runs. Exact results and individual review dispositions are retained in the analysis capsule. These tests do not qualify the patched driver or the candidate loader. The plan includes an explicit partial-failure archival procedure and preserves the prior root-rebind approval as a separate member.
