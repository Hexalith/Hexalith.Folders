# Released Folders 2.0.0 consumer verification

`receipt.json` records the current local adoption and remaining operational gates.
All thirteen owning HEADs and indices remain unchanged. Builds has a two-file
working delta selecting the complete Folders 2.0.0 family and guarding it with the
existing catalog tests. Projects imports that catalog without a local Folders pin.

The final private run passes both Release/package CI-solution builds, the full
Folders Debug/source build, 124 Projects adapter/delegation/mapping cases, twelve
parity categories, governance 217 + 7 + 3 cases, and the catalog/compatibility gates.
The five signed public archives and four archive-to-runtime DLL bindings are
retained. `final/projects-server-local-build.zip` is an unpublished compiled runtime
bundle; it supplies no immutable hosted image, release or deployment identity.

`provisional-audit/` contains a review proposal, its validated temporary source
commit, bundle, patch, prior audit and exact successful generator/validator commands.
It refreshes five Folders rows, preserves 312 unrelated package rows and 156 family
decisions, and retains all 9,451 historical observations. The live owner audit is
unchanged and refuses the uncommitted catalog. Regenerate it after the real owning
catalog commit; never replace it with this temporary-revision proposal.

The initial cache-copy interruption, the primary/fallback cache setup failure and
the first refused temporary bundle command are retained separately. Final runs
supersede those setup attempts. No owning commit/push, publication, deployment,
topology startup, activation, migration clock or closure occurred.

Replay from the repository root:

```bash
packet=_bmad-output/implementation-artifacts/evidence/story-1-17-released-v2-consumer-2026-11-15
python3 "$packet/verify.py" --workspace "$PWD"
python3 -O "$packet/verify.py" --workspace "$PWD"
```

`verify.py` also works without `--workspace` for portable retained-artifact replay.
It uses explicit checks under optimized Python. `replay-probes.json` records
rejection of archive corruption and a rebound unsafe activation-authority claim.
`artifact-manifest.json` binds retained files; it intentionally omits itself and
the subsequently produced replay logs.

To reconstruct the provisional source and rerun the deterministic audit validator
without changing an owning repository:

```bash
packet="$PWD/_bmad-output/implementation-artifacts/evidence/story-1-17-released-v2-consumer-2026-11-15"
run-with-temp bash -s -- "$PWD" "$packet" <<'SH'
set -eu
workspace=$1
packet=$2
git clone --quiet --shared --no-checkout "$workspace/references/Hexalith.Builds" "$TMPDIR/builds"
git -C "$TMPDIR/builds" fetch --quiet "$packet/provisional-audit/source.bundle" HEAD
git -C "$TMPDIR/builds" checkout --quiet --detach FETCH_HEAD
cd "$TMPDIR/builds"
pwsh -NoProfile -File Tools/validate-package-version-audit.ps1 \
  -AuditPath "$packet/provisional-audit/package-version-audit.proposed.json"
SH
```

The source bundle requires the recorded Builds base
`04bdcaf929198e69321c226dda5ab40f7b3839ce`, present in the owning repository.
`run.py` accepts a new simple attempt name and reserves its output directory before
running fresh public-package qualification in `run-with-temp`. The audit preparation
and finalization scripts document this execution; preserve their existing evidence
directories and select new destinations for another proposal.
