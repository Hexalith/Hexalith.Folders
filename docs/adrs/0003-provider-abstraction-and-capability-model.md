# ADR 0003: Provider abstraction and capability model

Date: 2026-05-31

Decision identifiers: `A-6`, `A-7`, `C12`. Implementing epic: Epic 3 (provider adapters). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted. The `IGitProvider` port, the `ProviderOperationCatalog` capability model, and the nightly drift lane have been in place since the Epic 3 provider stories.

## Context

Hexalith.Folders integrates two distinct hosting providers, GitHub and Forgejo. They differ in API surface, authentication, branch and ref behavior, rate-limit posture, and webhook semantics. Treating Forgejo as a GitHub base-URL swap would silently mis-handle these differences and produce incorrect operator-facing outcomes.

Architecture decisions `A-6` and `A-7` pin the provider clients: GitHub uses Octokit `14.0.0` inside the adapter. Forgejo uses a hand-written typed `HttpClient` adapter fed by per-version `swagger.v1.json` snapshots for REST operations and LibGit2Sharp `0.32.0`, with bundled libgit2 `1.8.6`, for the smart-HTTPS operations that Forgejo REST cannot express safely. Decision `C12` requires provider contract suites to run in hermetic PR-gate mode and live-nightly-drift mode with a fixture-to-failure-mode coverage matrix.

## Decision

Providers sit behind an `IGitProvider` port and a `ProviderOperationCatalog` capability model; no caller depends on a concrete provider client.

- `A-6`: GitHub support uses Octokit inside the GitHub adapter only; the Octokit type surface never leaks past the provider boundary.
- `A-7`: Forgejo readiness, create, bind, and status observations use a typed `HttpClient` adapter validated against per-version snapshots under `tests/contracts/forgejo/<version>/swagger.v1.json`. File staging and explicit commit use centrally pinned LibGit2Sharp `0.32.0` and bundled libgit2 `1.8.6` over smart HTTPS because Forgejo's combined REST contents write has no expected-old-head compare-and-swap field. "Forgejo is not a GitHub base-URL swap" remains a binding capability rule.
- `C12`: nightly oasdiff drift classifies provider schema changes as additive, breaking, or unknown, and an unsupported or failing provider version cannot report ready.

The Forgejo smart-HTTPS profile is limited to exact versions `15.0.7` and `16.0.3`, SHA-1 repositories, classic upload-pack/receive-pack, and receive-pack `report-status`. Every stage and commit uses a fresh current-user-only bare temporary repository, supplies the short-lived bearer credential only through in-memory custom headers, disables ambient Git configuration and redirects, and performs one exact-head fetch. Stage constructs a tree without a commit or remote update. Commit reconstructs that tree, creates one commit with the expected head as its sole parent, records the created identity, and sends one non-force full-ref update whose advertised old identity is the expected head. One read-only exact-ref REST observation confirms success. Executable or symbolic-link entries at touched paths, SHA-256 repositories, SSH, dumb HTTP, hooks, checkout, submodules, LFS, incompatible native assets, protocol drift, and unsupported Forgejo versions fail closed.

Capability differences (supported operations, branch/ref behavior, credential mode, rate-limit posture, readiness behavior, drift evidence) are documented and tested per provider; the system never claims provider parity.

## Consequences

- Adding a third provider means implementing the port and its capability tests, not editing call sites; the abstraction is N-provider capable, not hardcoded to GitHub plus one.
- Drift is caught by evidence: the nightly lane surfaces breaking and unknown drift instead of failing in production.
- The cost is that every provider capability must be capability-tested; convenience assumptions about API, permission, webhook, rate-limit, or default-branch equivalence are disallowed.
- The Forgejo adapter carries a native dependency and must verify the pinned musl asset in the production Alpine target in addition to ordinary managed-code and retained-REST drift checks.

## Alternatives Considered

- A single provider client parameterized by base URL was rejected because GitHub and Forgejo are not API-compatible and the assumption is unsafe.
- Generating the Forgejo client from a live endpoint was rejected because hermetic PR gates must run without provider credentials or network access; per-version snapshots keep the contract reproducible.
- Forgejo's `POST /repos/{owner}/{repo}/contents` was rejected for the canonical commit path because it creates a commit immediately and has no expected-old-head field. A preflight ref observation cannot substitute for receive-pack compare-and-swap.
- An external `git` executable and SSH transport were rejected because their ambient configuration, credential, process, and protocol surfaces do not meet the adapter isolation contract.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. Provider behavior is enforced by the provider capability suites and the nightly drift lane. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
