# Story 1.17 pinned catalog object restoration proposal

Status: pending explicit authorization. No command below has been executed.

The approved Debug distribution qualification stopped during production catalog
materialization. Platform HEAD `3659a44d86c08645215689eaeff8b3624d357dd5`
records Builds gitlink `4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42`, but the root
Builds repository at `aa46f0c534206b4645b8d9eef08c9a90e1ab540e` cannot read that
commit. Independent `cat-file`, `show` and `ls-tree` checks exit 128. Builds is
not shallow. The existing origin is
`https://github.com/Hexalith/Hexalith.Builds.git`.

The proposed scope amendment permits one network fetch into that repository's
object database, followed by read-only identity/content verification and a new
attempt of the already-approved build-only qualification. It grants no checkout,
pin, index, branch, tag, tracker, publication, deployment or application-startup
change. Existing receipts remain immutable. No nested reference is fetched.

From `/home/administrator/projects/hexalith/folders/references/Hexalith.Builds`:

```bash
git -c gc.auto=0 -c maintenance.auto=false -c fetch.writeCommitGraph=false fetch --no-tags --no-write-fetch-head --no-recurse-submodules origin 4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42
```

Before fetching, recheck origin, all thirteen repository HEAD/index identities,
root gitlinks and protected source bytes. Record the exact fetch command/result.
Afterward, require the requested object to be a commit, verify its complete
identifier, read `Props/Directory.Packages.props` and its import closure, and
confirm checkouts, indices and pins are unchanged. Re-run production mapping
and catalog materialization through the existing APIs using fresh private
outputs and an additive receipt. Catalog availability supplies no compatibility
or release acceptance by itself.

If origin cannot supply the exact commit or any required catalog input is
unavailable, stop and retain the failure. Platform/Builds owners must supply the
correct immutable object closure or propose a separate authority/pin repair.
Do not substitute current catalog bytes or another revision.

The operation adds fetched objects and any required pack metadata to local Git
storage. Abandoning qualification leaves these immutable objects in place;
do not prune or repair the shared object database as rollback.

See the [results](story-1-17-current-debug-distribution-results.md) and
[parent verification](evidence/story-1-17-current-debug-distribution-2026-10-29-parent-verification.json).
The approved [spec](spec-1-17-qualify-current-debug-distribution.md) currently
prohibits Git writes; this proposal requires a human amendment before execution.

## Approved disposition - 2026-10-29

Jerome instructed "sync submodules to latest and approve". The broader approved
root-only fetch/synchronization restored the exact pinned object without needing
the proposed standalone fetch command. All twelve root checkouts now match their
fetched origin/main; eight updated gitlinks are staged for the supported source
resolver. No commit, push or nested initialization occurred. Resume the existing
build-only qualification with new tuple bindings and additive evidence.
