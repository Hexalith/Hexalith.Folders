# Story 1.17 local qualification, November 15, 2026

This directory retains the pending-inventory reconciliation and its failed
release prerequisites. Operational qualification remains **HOLD**. Route mode
is `V1Only`; T0 and activation authority are unset; all fourteen staging checks
are unexecuted. The parent remains in progress and its tracker remains backlog.

`receipt.json` and `handoff.json` index the source tuples, exact commands,
cached package identities, local results and owner inputs. `manifest.json`
binds every retained file. Copied diagnostic DLLs/PDBs are local build evidence,
not published packages or independently executable deployment artifacts.

The source fallback passes authority 20/20, routing 19/19 and contract behavior
18/18 before generation. The 1,635-row candidate reproduces twice and changes
only six committed runner-script hashes plus its aggregate digest. Final
package-profile conformance passes 21/21, behavior 18/18 and governance
217/217 + 7/7 + 3/3. Five parity categories execute successfully; seven lack
their runtime/consumer assemblies after the recorded package build failure.

The default package graph fails on native scanner APIs absent from EventStore
5.1.0. Projects cannot restore Conversations Client/Contracts 2.0.0 from the
offline cache. These results neither establish absence from a remote feed nor
replace a fresh vulnerability audit. The old source proposal also refuses
current drift; its complete failed result is retained. Its 64 isolated safety
mutation methods pass, while both real-source binding methods fail, reporting
24 changed bindings. The narrower pre-oracle setup attempt is retained in
`before-oracle-prerequisite/` and is superseded only by the final command index.

To check retained bytes and result summaries without executing any binary:

```bash
python3 verify.py
python3 -O verify.py
run-with-temp python3 test_verify.py
```

For an independent rehash against the owning checkout:

```bash
python3 verify.py --workspace /absolute/path/to/folders
```

All build/restore jobs used `run-with-temp`, private repository copies and an
offline feed beneath its `TMPDIR`. Their child commands finished before cleanup.
No source/runtime, package pin, dependency, historical planning, tracker, Git
index/revision, publication or deployment was changed by this work. External
commits during preparation are distinguished in the dated source snapshots;
the 24,111 copied source/build inputs match between the two executed profiles.
No new topology or hosted observation is claimed.
