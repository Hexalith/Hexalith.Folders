# Story 1.17 local recheck after EventStore 5.1.1, November 15, 2026

Operational qualification remains **HOLD**. Route mode is `V1Only`; T0,
first-install activation and activation authority are unset; all fourteen
staging checks are unexecuted. The parent spec stays in progress and its tracker
row stays backlog.

This directory records the first local recheck after EventStore 5.1.1 was
published and adopted by the uncommitted Builds and Projects catalog edits.
`receipt.json` states every claimed result; `handoff.json` is a byte copy of the
owning publication handoff; `manifest.json` binds every retained file.

## What ran

Every build, restore, test and generation ran inside a `run-with-temp` private
copy of the root and its thirteen root-declared repositories. Restores used an
offline feed of archives already in the user package cache, so no network
source was contacted. No owning Git index, revision, package pin or cache entry
was changed by these jobs.

| Directory | Purpose |
| --- | --- |
| `attempt-1/` | First Debug/source attempt on the unchanged tuple, plus `diagnosis/`. Builds pass, but native authority (16/64) and routing (0/20) fail to load EventStore Client/Contracts `5.1.1.0`. |
| `attempt-2-private-fix-trial/` | The same gates with the proposed `Directory.Build.targets` patch applied only to the private copy. Every gate passes. |
| `final/` | The same gates after the identical patch was applied to the owning checkout, plus deployment conformance and two pending-inventory generations. |
| `final-solution-with-pwsh-path/` | The broad Debug/source CI-solution build, rerun with `~/.dotnet/tools` prepended to PATH after `final/` stopped on `pwsh` exit 127. |

Each run directory keeps its `before.json`/`after.json` snapshots, command ledger, logs, xUnit XML
and per-selector counts. `copied-source-inputs.json.gz` (deterministic gzip) lists the hash of every
copied source/build input.

`profile-evaluation.json` evaluates (never builds) all 33 CI-solution projects in
both profiles. The fix is inert in Release/package and carries the root
EventStore selection on every Tenants source reference in Debug/source.
`reuse-identity.json` compares every owner with the same-day public-feed
Release/package qualification. The only Release-relevant difference is that
inert edit, so the earlier Release/package results are reused, not rerun.

## Root cause and fix

The Builds catalog now selects EventStore 5.1.1. Tenants keeps a local 5.1.0
package-cohort default and carries it to its EventStore source references. In
the Folders Debug/source graph, EventStore Client and Contracts were therefore
also built as `5.1.0.0` into the shared source output, while Folders and
EventStore Server/DomainService/Testing referenced `5.1.1.0`. Published packages
are not affected.

Folders `Directory.Build.targets` now applies EventStore's existing rule to its
own edges. In a local source graph without a verified workspace mapping, it
carries its EventStore selection on direct and promoted Tenants source
references. Tenants keeps its package pin, and package graphs are unchanged.

## Replay

```bash
python3 verify.py
python3 -O verify.py
run-with-temp python3 -I test_verify.py
python3 verify.py --workspace /absolute/path/to/folders
```

The workspace option also rehashes the owning handoff, pending inventory, fix,
tracker, protected inputs, the preserved spec/readiness prefixes and the exact
migration-package header correction. These checks establish local consistency
only: no release, hosted artifact, staging result or operational acceptance.
