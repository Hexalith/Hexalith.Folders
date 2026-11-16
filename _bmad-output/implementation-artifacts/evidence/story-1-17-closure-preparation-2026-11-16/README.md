# Story 1.17 current closure preparation

This packet reconciles the November 16 audit handoff with the current workspace.
Preparation is complete; operational entry and closure remain HOLD. The parent
story remains in-progress and its tracker backlog. No deployment, activation,
recovery, publication, Git mutation or tracker transition ran.

Read `receipt.json`, the linked owner intake and the new handoff first.
`before.json` binds thirteen owner revisions/indices and 200 protected paths.
`document-appends.json` and `preservation-check.json` establish preservation of
historical prefixes and unchanged protected inputs. The three appended documents
provide the current status without rewriting their original evidence.

`consumer-inventory.json` describes inspected source behavior, not deployed callers
or verified hosted delegation. `current-mapping-inputs.json` is a proposal input,
not owner mapping acceptance. `remote-observations.json` is a filtered observation
of one page of GitHub results; it retains asset metadata and individual CI job
states, not downloaded package bytes. `deployment-records.json` and
`deployment-workflow.json` distinguish package release environment records from
actual workloads. No global absence is established. The CI failure excerpt is
selected diagnostic text from the linked job, not a complete log archive.

Check the sealed packet bytes from this directory:

```bash
sha256sum --check SHA256SUMS
```

From the Folders repository root, retained artifact replay can be repeated without
network access or current-workspace qualification:

```bash
run-with-temp python3 -O _bmad-output/implementation-artifacts/evidence/story-1-17-released-v2-consumer-2026-11-15/verify.py
run-with-temp python3 -O _bmad-output/implementation-artifacts/evidence/story-1-17-landed-v2-tuple-2026-11-15/verify.py
run-with-temp pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-package-version-audit.ps1
```

`commands.json` retains the four primary lane commands actually run and their logs. Additional source/preservation observations are independently recomputed by the read-only replay below. Do not use the
historical verifiers' `--workspace` option to claim the advanced tuple was tested:
their earlier exact revision bindings deliberately refuse changed workspaces.
The old source proposal's exit 1 is retained as the expected refusal, not a pass.
No previous package download, build or service test was repeated in this packet.

Use the owner intake's ordered deliverables to resume. The first-install and
existing-v1 fallback decisions are already accepted conditionally; target/data
branch selection still requires the real attempt lock. DEC-A8-HOLD is satisfied
and Story 11.6 implementation/verification are complete. Neither is reopened.

The new replay uses only standard-library reads and read-only Git commands. From
the repository root, recompute all recorded workspace comparisons and controls:

```bash
run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-1-17-closure-preparation-2026-11-16/replay.py --workspace "$PWD"
run-with-temp python3 -B -O _bmad-output/implementation-artifacts/evidence/story-1-17-closure-preparation-2026-11-16/replay.py --workspace "$PWD"
```

Without `--workspace`, replay checks only this sealed packet and recorded primary
lane outcomes. Workspace replay deliberately refuses future changed revisions or
indices; it does not re-run product builds, contact services or qualify an
advanced tuple. All seventeen Platform direct owner pins are inventoried; the
four declaration comparisons do not select an executable closure by themselves.
