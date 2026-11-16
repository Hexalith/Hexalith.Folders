# Story 1.17 Projects G-6 catalog acquisition — 2026-11-16

The CiCd-owned acquisition action is implemented and locally qualified. **Owner
delivery and fresh corrected hosted CI/G-6 are blocked:** Projects has no CiCd
registration or accepted delivery ref, and the supported delivery applies an
entire perimeter including the Builds pin. No narrow pre-adoption workflow
delivery mechanism exists. The correction has not been applied to Projects.

The owner implementation is in CiCd at
`.github/actions/prepare-g6-catalog/{action.yml,prepare.py}`. It validates the
committed direct Platform source, staged outer gitlinks, direct checkout HEADs,
root declarations and approved Builds origin. It derives the catalog commit
from Platform's committed Builds gitlink, fetches only a missing exact commit
with recursion/tags disabled, verifies the catalog object and regular blob, and
checks HEAD/index/gitlink preservation even if fetching fails. Failures emit
bounded rule/category diagnostics. Builds' mapper is unchanged.

## Exact source and disposable proof

Projects remains `981ceef13a1abc88219925e73c51a23eb9ac50c2`; its Builds gitlink is
`fdda17c0a4538b0f7c99f813c20ca24b49006530` and direct Platform gitlink is
`829d68b4cb6bf4e27c9baba318555e2e7b3ebbaf`. That Platform revision selects catalog
`e2bbbd0893f586e4d447ef8358ff747ea3d34435`. The live Platform branch has advanced;
it is not the catalog authority for this Projects source. CiCd's reviewed owner
base is `d3f3ef864c08cd89e390246d1a5d1dedffc4a92b`; its unrelated documentation
advance during investigation is preserved.

[probe-result.final.json](probe-result.final.json) records the actual production helper
executed in a disposable depth-one Builds checkout fetched from the approved
GitHub origin. The source Projects and direct Platform checkouts were sparse
copies using existing local objects; no nested submodule was initialized.

- Git batch-check reports the exact catalog commit missing before acquisition.
- Reading its catalog fails with exit 128 before acquisition and succeeds with
  exit 0 afterward.
- The catalog bytes equal the exact selected source bytes, SHA-256
  `d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637`.
- All three tested repository HEADs, complete indices, committed gitlinks and
  refs are unchanged. A second invocation reports no acquisition.
- Seventeen offline behavioral tests pass, covering failure paths and approved
  origin enforcement. [behavioral-tests.log](behavioral-tests.log) and the
  [Python 3.12 runner check](behavioral-tests-python312.log) are retained.

This proof qualifies Git-object acquisition only. It does not execute the
Conversations mapper, NuGet restore or runtime qualifier and supplies no new
hosted acceptance.

## Owner integration and remaining blocker

CiCd retains `docs/proposals/projects-g6-catalog.patch`, with exact source/file
bindings in [proposal.json](proposal.json). It adds the local owner action and
invokes it before `Restore effective G-6 package graphs`; the Platform revision
is read from the executing Projects source, rather than hardcoding a catalog.
[proposal-check.json](proposal-check.json) records `git apply --check` and an
exact-byte application in a disposable source copy, preserving HEAD/index.
No live module was edited.

[owner-blocker.json](owner-blocker.json) retains the observed registry and the
empty successful `git ls-remote --refs origin
refs/heads/delivered/Hexalith.Projects` response. The owner sources show:

- `tools/deliver.py:apply_module` requires an accepted delivered ref and applies
  its complete rendered perimeter.
- `templates/perimeter.json.j2` replaces workflows and sets a Builds pin from
  `builds-version` (`v6.0.6`). The generic CI template has no G-6 preparation hook.
- Projects is absent from `setup/`. Full adoption is separately scoped as
  CiCd Story 18, including policy changes and owner preconditions.

The remaining action is for the CiCd owner to provide a supported delivery path
for this pre-adoption preparation change that preserves current pins and gates.
Then apply the retained proposal and qualify its actual committed Projects
source in hosted CI/G-6. Direct workflow edits, a credential workaround, or
registering Projects as an incidental adoption would breach this task's bounds.
No owner workflow was dispatched to run an inapplicable delivery.

Fresh read-only [hosted observations](hosted-observations.json) still show
[Projects CI 43881420098](https://github.com/Hexalith/Hexalith.Projects/actions/runs/43881420098),
attempt 1, completed/failure at the source above. G-6 failed at the restore step;
build-and-test and generated-artifact jobs succeeded. The managed E2E job was
skipped. These are new observations of the existing run, not a corrected run.
The latest first page of repository runs contains that CI and passing CodeQL
and commitlint runs for the source; it is not an exhaustive history query.

## Reproduction and preservation

From Folders, using a NEW evidence output path for each probe:

```bash
run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-1-17-g6-catalog-acquisition-2026-11-16/probe.py --output <new-result.json>
run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-1-17-g6-catalog-acquisition-2026-11-16/check-proposal.py
run-with-temp python3 -B -O _bmad-output/implementation-artifacts/evidence/story-1-17-g6-catalog-acquisition-2026-11-16/verify.py
```

`verify.py --packet-only` checks retained hashes and proof consistency without
comparing the later workspace. Default verification additionally checks owner
source hashes, original repository HEADs/indices/gitlinks and protected tracking
bytes against [before.json](before.json). An unrelated concurrent edit to CiCd's
Story 12 spec and a later documentation-only Story 19 commit are explicitly
hash-bound in [concurrent-work.json](concurrent-work.json). Their source/index
advance is checked against that observation; task-owned implementation, owner
delivery policy and all gitlinks are checked independently. This task never
edited or committed those concurrent files.
The probe refuses an existing output.
All child work finishes before the wrapper removes its private TMPDIR.

The initial pre-review probe receipts are retained as historical observations;
`probe-result.final.json` binds the final hardened helper. Independent review identified
ten corrections to Git isolation, state preservation and regression coverage;
all were addressed. No finding was deferred.

No dependency pin, nested initialization, publication/audit/A8 work, deployment,
v2 activation or Story 1.17 tracking was changed. No commit, push, delivery or
rerun was performed. The parent story remains open.
