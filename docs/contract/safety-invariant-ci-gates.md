# Safety Invariant CI Gates

Story 1.15 wires the metadata-only safety gate for the Contract Spine, generated artifacts, local gate diagnostics, and the current repository examples. It does not implement runtime redaction handlers, provider adapters, tenant-prefixed cache-key lint, exit-criteria gates, or parity-completeness checks; Story 1.16 still owns those adjacent governance gates.

## Local Command

Run from the repository root:

```powershell
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx --no-restore
.\tests\tools\run-safety-invariant-gates.ps1 -NoRestore
```

The command is offline and deterministic. It does not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or nested submodule initialization.

## CI Job

The existing `contract-spine-gates` workflow invokes `./tests/tools/run-safety-invariant-gates.ps1 -NoRestore` after the focused contract/generated-artifact gate. The workflow keeps one restore and one build lane, uses `global.json`, checks out with `submodules: false`, and does not upload scanner inputs, contaminated fixtures, assertion diffs, generated snippets, local paths, or full logs.

## Gate Inputs

- `tests/fixtures/audit-leakage-corpus.json`: authoritative sentinel vocabulary, classifications, forbidden surfaces, and allowed provenance-safe representations.
- `tests/fixtures/safety-channel-inventory.json`: channel manifest with owner, artifact source, prerequisite status, safe absence diagnostic, and scan flag.
- `tests/fixtures/quarantine/safety-negative-controls.json`: opt-in contaminated controls used only to prove detection and sanitized failure output.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`: OpenAPI and Problem Details examples.
- `tests/fixtures/parity-contract.yaml` and `src/Hexalith.Folders.Client/Generated`: generated-artifact leakage checks only.
- `docs/contract/*.md`, `.github/workflows/contract-spine.yml`, and `tests/tools/*.ps1`: developer and CI diagnostic surfaces.

## Diagnostics

Allowed fields are gate name, repository-relative path, output channel, rule ID, synthetic sample ID, category, classification, owner story, content hash, and remediation hint. The gate must not echo forbidden values, raw payloads, file contents, patch bodies, credential material, generated context payloads, local absolute paths, production URLs, tenant data, provider responses, cursor values, counts, ordering hints, stack traces, or unauthorized-resource hints.

Bounded missing-channel diagnostics:

- `SAFETY-CHANNEL-MISSING`: no implementation exists yet for the channel, and the manifest records the owner story.
- `SAFETY-PREREQUISITE-DRIFT`: the channel should have an artifact or seam, but the expected source is absent or stale.

`reference-pending` is acceptable only when the channel has no runtime artifact in the current repository state and the owner story is explicit. Claimed `covered` entries fail closed when their repository-relative source is stale.

## Classification Rules

The corpus is the only vocabulary source for this gate. Unknown classifications, missing `forbidden_output_surfaces`, missing `allowed_provenance_representations`, absent `synthetic_sentinel`, absent `synthetic_data_only`, or local synonyms fail before artifact scans run.

Paths, branch names, repository names, commit messages, actor metadata, folder/workspace/task identifiers, provider correlation references, and diagnostic metadata are classified, not blanket-deleted. Allowed output uses sample IDs, bounded classifications, redaction markers, content hashes, operation IDs, schema pointers, gate names, rule IDs, and repository-relative paths.

## Negative Controls

Contaminated samples live only under `tests/fixtures/quarantine/`. Normal artifact scans exclude that path. Tests opt in to the quarantine fixture to prove forbidden values are detected, and assertion messages report only metadata such as sample ID, channel, classification, category, and rule ID.

## Safe States

Redacted, unknown, missing, hidden, unauthorized, stale, and unavailable states are distinct:

- `redacted`: a value exists for an authorized audience but is deliberately withheld.
- `unknown`: the system cannot determine the value without leaking or exceeding the current authority.
- `missing`: the value is absent after authorization.
- `hidden` and `unauthorized`: the response must not disclose existence.
- `stale`: projection or diagnostic freshness is below the requested consistency.
- `unavailable`: the channel or projection is not currently reachable.

Safe-denial and context-query examples must authorize before observation: tenant access, folder ACL, path policy, sensitivity classification, limits, then search, glob, partial-read, or metadata execution.

## Reviewer Checklist

- Corpus changes are synthetic-only, reviewer-visible, classified, and safe to commit.
- New categories appear in `audit-leakage-corpus.json`, not only in test code.
- Negative controls remain quarantined and are not referenced by normative examples.
- Failure messages and CI logs never echo the forbidden value.
- Channel inventory entries either resolve to existing repository-relative sources or explicitly declare `reference-pending` / `prerequisite-drift`.
- Generated-artifact scans remain leakage-only and do not validate drift, derivation, client correctness, exit criteria, idempotency encoding, or parity completeness.
- Story 1.16 remains owner of tenant-prefixed cache-key lint, exit-criteria gates, idempotency-encoding gates, and parity completeness.
