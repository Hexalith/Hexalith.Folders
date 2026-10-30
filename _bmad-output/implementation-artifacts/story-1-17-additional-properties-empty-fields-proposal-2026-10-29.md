# Proposed repair: retain empty-field refusal and trailing-semicolon compatibility

**Unapplied production proposal.** The synchronized qualification stopped at an
existing assertion: of 133 discovered `WorkspaceMsBuildTests` cases, 132 passed
and `UnsafeForwardedAssignmentsStopBeforeChildEvaluationAsync` failed for
`HexalithDepFromSource=true;;Version=1.0.0`. The build returned `0` rather than
refusing with `HXW006`. The [failed receipt](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-fixtures.json)
preserves the exact original-assembly command, XML and full log. No expectation
was changed, and no production patch has been applied.

The owner is `Hexalith.Builds`, in
`SourceMappingReconciliationTask.TryCanonicalizeRetainedProperties`. The current
source is at revision `e38700b3a8de8ef57342688e9dfbbe3f573201ff`.
Upstream commit `992850be21cc8b7b10e06cde85e9cc0d0f1f9e1f` explicitly fixed
trailing-semicolon compatibility and added the passing
`RetainedProjectVersionMetadataMatchesCatalogAsync("Version=1.0.0;", true)`
case. Its `RemoveEmptyEntries` change also discards leading and interior empty
assignments. A one-line rollback would regress that intended positive case.

The [concrete patch](evidence/story-1-17-current-debug-distribution-2026-10-29-proposed-reconciliation.patch)
retains empty fields when splitting, then removes at most one final empty field.
The existing assignment validation rejects every remaining empty field. All
catalog/version, duplicate-name, permitted-property and physical-root checks
remain in place. The [provenance record](evidence/story-1-17-current-debug-distribution-2026-10-29-proposed-reconciliation.json)
binds the current source and proposed patch bytes. `git apply --check` passed
against the owning checkout and verified the source remained byte-identical.
The patch's behavior has not been executed.

The exact proposed policy is:

| Input | Expected result | Reason |
| --- | --- | --- |
| Empty or whitespace-only metadata | Accept as absent metadata | Existing behavior |
| `Version=1.0.0` | Accept when catalog matches | Existing behavior |
| `Version=1.0.0;` or `Version=1.0.0; ` | Accept when catalog matches | One optional list terminator; preserve upstream positive case |
| `HexalithDepFromSource=true;Version=1.0.0` | Accept when mapping/catalog match | Existing valid forwarding list |
| `HexalithDepFromSource=true;Version=1.0.0;` | Accept when mapping/catalog match | The same single-terminator grammar |
| `;Version=1.0.0` or `;HexalithDepFromSource=true` | Refuse with `HXW006` before child evaluation | Leading empty assignment |
| `HexalithDepFromSource=true;;Version=1.0.0` | Refuse with `HXW006` before child evaluation | Existing failing interior-empty case |
| `Version=1.0.0;;` or `Version=1.0.0; ;` | Refuse with `HXW006` before child evaluation | Removing one terminator still leaves an empty field |
| `;` or `;;;` | Refuse with `HXW006` before child evaluation | Separator-only metadata |
| Duplicate names, unknown properties, mismatched versions or escaping roots | Refuse | Existing checks remain unchanged |

The scope amendment should authorize this exact production parser patch and
table-driven coverage for the listed edge cases in the existing workspace test
class. It should retain the current positive trailing-version case and the
current failing forwarding case. It should authorize no unrelated historical
restoration, catalog/pin change, Git write, application startup or expectation
weakening. The original Debug qualification remains incomplete until rebuilt
from the approved repaired bytes and all its gates pass.

Existing regression methods to rerun after rebuilding are
`RetainedProjectVersionMetadataMatchesCatalogAsync`,
`ForwardedMappedSelectionsRestoreWithCanonicalChildValuesAsync`, and
`UnsafeForwardedAssignmentsStopBeforeChildEvaluationAsync`. Their original
assembly uses fully qualified `-method` filters and `-parallel none`.
Then rerun the full current workspace class with its discovery-derived
denominator, the three required original classes, the other permitted affected
workspace checks, and the compilation-only model/output/PE gates:

```bash
python3 -B eng/story117-source-distribution/qualify.py --execute --suffix approved-empty-fields
python3 -B -m unittest discover -s eng/story117-source-distribution -p 'test_*.py' -v
python3 -B -O -m unittest discover -s eng/story117-source-distribution -p 'test_*.py' -v
```

Use a fresh unused suffix for every attempt. The harness must continue to
exclude `WorkspaceAppHostTests.PlatformPlanDelegatesToThePinnedModelAsync`,
which reaches the forbidden `ComposeRun` boundary; that exclusion supplies no
model/runtime compatibility credit. No recorded command is replay authority.

The compatibility risk is a consumer currently relying on leading, repeated or
whitespace-only separator fields that the broad upstream split silently drops.
The proposal deliberately rejects those malformed lists while preserving one
ordinary terminator for every otherwise valid assignment list. Repeated trailing
terminators are therefore rejected. The existing blank-metadata fast path is
unchanged. The repair affects build-time input validation, not domain behavior
or deployed data; publication and ordinary execution remain outside scope.

Rollback is to restore the exact pre-patch bytes bound in the provenance record
and rebuild fresh private outputs. Any failed gate stops dependent qualification,
preserves the first failure and leaves the candidate unqualified. No generated
artifact, runtime state or Git history should be substituted or repaired to make
the validation pass.


## Approved disposition - 2026-10-29

Jerome answered "yes" to applying this concrete repair and continuing the Debug
qualification. The original proposal and provenance remain dated before-images;
implementation and verification will be recorded additively in the active spec
and qualification results. All unrelated production/operational boundaries hold.
