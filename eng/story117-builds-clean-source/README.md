# Story 1.17 committed Builds qualification

Run from the Folders checkout that owns these records:

```bash
cd ~/projects/hexalith/folders
python3 eng/story117-builds-clean-source/verify.py
```

The verifier uses `references/Hexalith.Builds`, checks its exact clean committed
revision, fixture bytes and all 4,888 captured Platform inputs, validates the retained diagnostic bundle and both
actual-package seed modes, and checks retained successful push CI for that same
main-branch head. Pass `--builds /absolute/path` only to verify another checkout
of that exact commit.

The preceding `story117-package-seed-20261016` records bind the older dirty source
snapshot. They remain unchanged historical preparation. Their original verifier
must refuse changed source bytes; use this follow-up verifier for the reconciled
commit. No prior release-ineligible inventory is relabeled.

Local diagnostic packages use the observed existing `4.31.2` identity in an
isolated feed. Their bytes are not the published release and must never be
published under that version. A clean technical inventory does not authorize
publication or establish Platform acceptance. The release workflow must select
its actual next version and requalify/repack that version on clean source.

After the current source is clean, pushed and has successful exact-head CI,
publication still requires separate authorization before:

```bash
gh workflow run build-release.yml --repo Hexalith/Hexalith.Builds --ref main
```

Do not bypass its current-head, successful-CI, pre-tag, artifact, catalog, pin,
tool-range, provenance or protected-environment checks. Observe the actual new
release/tag/commit and signed archive hashes afterward. Platform still needs an
explicit acceptance decision, exact tool pin/range, coherent Builds gitlink and
released owner declaration/attestation/intake prerequisites.

Deployment, staging and production activation require their own authorization.
Preserve V1Only, unset T0, historical approvals, all 14 unexecuted staging checks
and Story 1.17 backlog/open.
