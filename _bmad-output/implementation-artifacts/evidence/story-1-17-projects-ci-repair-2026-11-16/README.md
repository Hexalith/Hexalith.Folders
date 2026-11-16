# Projects fixture repair and host owner preparation — 2026-11-16

The supplied closure handoff is updated; its exact prior bytes are retained in `closure-handoff.before.json`. Historical preparation evidence, publication and approval files are unchanged. The old exact-workspace comparison is historical because this authorized fixture/handoff change alters that tuple.

`receipt.json` binds the handoff, fixture patch and owning Projects action. `reconciliation.before.json` records initial Git and input state. `remote-ci-fixture-excerpt.txt` is a bounded read of job 133200208530 from run 43838261389, not a rerun. That run remains failed with product jobs skipped.

The fixture now uses manifest-declared EventStore.Contracts and checks external-version preservation through normalization. Before: exact Conversations KeyError reproduced. After: 20/20 fixtures pass. The six locally buildable package-mode test projects pass 1357/1357, zero skipped; warnings/audits remain enabled. Full solution restore and focused Server.Tests build fail NU1109 (Folders 2.0.0 requires McpCli.Abstractions >=3.0.0, Projects pins 2.0.1). Five diagnostic candidates pack, but strict graph validation fails EventStore.Contracts version drift (manifest 5.1.0 versus selected 5.1.1); API consumers are not run on rejected candidates. Server, Integration, runtime/G-6 and real E2E remain unqualified.

Product snapshots copy tracked build/source/test inputs and read the Projects-declared Builds tree 801f268cd1b37ecae41d4fb20d115c055b4c5793 via the permitted existing root Builds object's archive; no nested submodule is initialized. `product-results.json` records every copied file hash and exact individual command/outcome. The retained `verify-product.py` now requires a new `--output` directory and refuses an existing one, preserving original evidence on rerun. `package-results.json` records the original separate five-candidate Release/package-mode check with explicit diagnostic version 91.92.93-ci.1 and source provenance. Disposable archives and outputs were removed with their run-with-temp workspaces; no package or host was published or retained as a release. Wrapper exit zero only means orchestration completed; individual failing gate results remain failures.

`folders-payload-binding.json` rehashes the exact retained signed Client/Contracts 2.0.0 archives and DLLs and reads their dependency metadata. Signature verification and publication remain completed historical evidence; no feed download, audit refresh or new signature claim occurred. The owner action is `references/Hexalith.Projects/docs/runbooks/story-1-17-projects-host-artifact-next-action-2026-11-16.md` from the umbrella root. It identifies package owners, the missing host production lane/configuration inputs, exact image/provenance receipt and Projects/Platform enrollment action. No immutable Projects host currently exists in this packet.

`commands.json`, `product-results.json` and `package-results.json` distinguish successes from blockers. Preserve all controls: V1Only, T0 null, activation authority null, fourteen staging checks unexecuted, tracking unchanged. DEC-A8-HOLD is satisfied; Story 11.6 implementation/verification is complete. No commit, push, publication, deploy or activation is performed.

Run the read-only verifier from the Folders workspace:

```bash
run-with-temp python3 -O _bmad-output/implementation-artifacts/evidence/story-1-17-projects-ci-repair-2026-11-16/verify.py
```

This checks actual results, unchanged source inputs, retained payloads, 200 protected historical paths, all thirteen HEADs/indices and old sealed packet members. It does not run current audit validation or grant operational acceptance. SHA256SUMS covers all packet files except itself and the verifier output `verification.log`.

## Independent review corrections

`verify-packages.py` retains the full reproducible snapshot procedure. Its fresh `package-replay/` execution captures its own source manifest, SDK/restore settings, exact commands/results, actual normalized nuspecs and `dependency-report.json`. The actual core candidate records EventStore.Contracts 5.1.1, proving the observed mismatch against manifest 5.1.0. No version is changed to make the check pass. The original ad hoc package-run source was not retained; its logs/results remain historical, and the retained runner independently reproduces the same graph rejection. Original product SDK output was not separately captured; `environment-observation.json` is explicitly a later observation, while the package replay captures its own environment.

To reproduce package boundaries, supply a NEW output directory:

```bash
run-with-temp python3 _bmad-output/implementation-artifacts/evidence/story-1-17-projects-ci-repair-2026-11-16/verify-packages.py --output <NEW-absolute-evidence-directory>
```

`verify.py` requires a complete SHA256SUMS seal, cross-checks both result tuples and handoff totals, and requires exactly the protected Folders Client/Contracts pair. Default mode also requires the original current workspace/diff. After separately authorized landing or later source work, verify historical retained bytes independently:

```bash
run-with-temp python3 -O _bmad-output/implementation-artifacts/evidence/story-1-17-projects-ci-repair-2026-11-16/verify.py --packet-only
```

Packet-only verification checks the complete retained seal; it does not claim the later live workspace still matches the tested tuple. The source snapshot intentionally selects tracked inputs; unchanged HEAD/index checks already reject newly tracked additions, and untracked files are not inputs to the diagnostic snapshots.
