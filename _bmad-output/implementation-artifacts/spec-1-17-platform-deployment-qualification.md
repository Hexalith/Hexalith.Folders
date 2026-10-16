---
title: 'Story 1.17 Platform deployment qualification'
type: 'qualification'
created: '2026-10-16'
status: 'in-progress'
baseline_commit: '2236855ee181bdeed2a103537c66316a1612c792'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/operations/container-images-and-dapr-app-ids.md'
  - '{project-root}/docs/runbooks/rollback.md'
  - '{project-root}/references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/reconcile-folders.md'
  - '{project-root}/references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md'
---

## Intent and authorized scope

Prepare an inert, locally verifiable qualification packet for the existing Story
1.17 hold. The user explicitly authorized preserving the dirty working tree,
read-only infrastructure queries, and reversible local preparation. They forbid
deployment, publication, Git staging/commit/push, tracker transitions, changing
V1Only or T0, or rewriting historical approvals. The original migration spec and
its frozen block remain unchanged. This increment qualifies a proposal; it does
not complete Story 1.17 or accept a different migration decision.

## Evidence already established by the parent

At 2026-10-16 approximately 06:29–06:34 UTC the existing static native context
`jpiquot@local` reports Kubernetes v1.34.9, kube-system UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, and one Ready node. Across 387 workload,
service and ingress objects (including ReplicaSets) and 63 ConfigMaps, no
Folders/Projects/shared-Platform reference was found. No staging/production
application/data namespaces or Gateway API are present. Monitoring APIs are
absent; Dapr APIs are present. Twenty-six Helm release metadata records contain
no Folders/Projects/Platform release. Local Docker has only four Dapr support
containers. This is one context at capture time, not global or historical absence.

Read-only registry GETs using the existing Docker credential helper return 401
anonymously and an authenticated complete nine-repository catalog, with no
Folders/Projects/Platform repository. Credentials were never printed or retained.
GitHub Platform reports no release, workflow or deployment. Module Release
workflows publish NuGet, not hosted workloads. Current Folders package release is
v1.2.1; current Projects release v1.0.0. Projects source pins Folders 1.2.1.
Folders main is the baseline above; Projects source is
`42c8e7bc3a58d78e7c6ea4273711d697e81175e7`. Local Platform is
`3a29fe039320ab13d95ff7d9801761dad72bfed9`; remote main is newer, so do not label
the local snapshot current remote Platform. Existing local Builds package-audit
fixes must remain intact and appear as dirty source inputs in any artifact claim.

The proposal therefore prepares a **first installation in the observed target**,
with material scope approval still required; a discovered existing installation
returns to the original migration branch. Do not synthesize ordinary traffic,
prior deployment artifacts, live baseline, or periodic-call schedules from zero
workloads. Platform's explicit first-install staging rule rehearses fresh install
plus candidate when no production working baseline exists. Administrator-approved
pre-G3 mode retains staging, provenance, locks, verification and records.

## Implementation boundary

Only add files under `eng/story117-platform-qualification/`. Do not edit any
existing files or another repository. Work from Folders. Do not invoke network,
Aspire lifecycle, package restore/build, containers, Git writes, or live APIs in
this implementation handoff. The parent owns infrastructure evidence, artifact
construction, and the final rollout proposal. Your output is a bounded local
configuration and staging-check packet, not a deployment compiler, new host,
module CLI, or a replacement Platform release mechanism.

## Tasks and acceptance

- [x] Add `configuration.proposed.json`: inert status; V1Only; T0 null; no
  production authority. Separate exact existing module configuration keys from
  Platform-owned bindings and unresolved values. Inspect actual source before
  recording keys. Include Folders server/workers/UI, Projects read-only client
  operations, EventStore topic override, read-model keyPrefix=none, app-token and
  workload/receipt authentication references, OTLP, dependency identities and
  secret reference sources. Use null for unresolved values and explain who owns
  each. No credentials, real endpoints, tenant identifiers or fixture auth.
- [x] Add `staging-checks.json`: ordered finite checks for artifact/provenance,
  first-install baseline selection, Platform profile and namespace isolation,
  readiness, authentication/authorization negative tests, durable end-state,
  Projects lifecycle/permissions/file metadata with distinct 401/404/503 outcomes,
  consumer attribution, route restart/reversal and retained-data recovery. Each
  check records the exact evidence required and owner. Include no claimed result
  for checks not executed. Staging Coexistence/V2Only tests never change the
  production hold or start production T0.
- [x] Add `verify.py` that validates packet consistency offline: required keys,
  V1Only, T0 null, no activation authority; every check has owner/evidence; no
  unexecuted check marked passing. Read only this packet and explicit source
  files. No subprocess/network calls, approval flags or execution mode. A
  diagnostic success means packet consistency only and must say so.
- [x] Add focused tests for fail-closed behavior on changed route mode, claimed
  authority, fabricated passing staging evidence, and removed required check.
  Use stdlib unittest and temporary copies; do not create mirrored assertion-only
  tests. Run all packet tests and verification and report exact commands/results.
- [x] Add a concise README describing unresolved Platform release/hosting and
  auth/provider boundaries. Platform's accepted single-node, 1h RPO/4h RTO,
  7-day frequent/30-day daily retention applies. Do not reinstate Folders' older
  HA/backend/5-minute RPO as enrollment gates. Do not invent Helm assets or
  workflows where the repository has none. Existing source wrappers are inputs,
  not a second hosted topology or an accepted composed EventStore release.

Given this packet, offline verification must pass for honest proposed defaults
and fail if a local edit can imply deployment authority or completed staging.
Given any missing release/provider/hosting evidence, qualification remains HOLD.
All results must clearly distinguish proposal consistency from operational
qualification. Preserve full canonical source revisions when recording VCS.

## Qualification result — 2026-10-16

All five bounded packet tasks pass local verification (13 tests; zero staging
checks executed). The parent also repaired the existing capacity fixture's
tenant/organization identity and rejected metadata tag, plus whitespace in five
Server test files. The required audited Release build, capacity 12/12, Server
979/979, final affected classes 145/145, routing/golden 114/114 and contract
conformance 40/40 pass. Projects builds in its normal package profile and passes
39/39 adapters; the selected local .2 package pair also builds and passes those
39 tests. Five package/symbol pairs and six local SDK image archives are
prepared, with matching packaged Client/Contracts payloads in the images.

The [rollout proposal](story-1-17-platform-rollout-proposal-2026-10-16.md) and
[structured evidence](evidence/story-1-17-platform-qualification-2026-10-16.json)
record source/registry/native inventory, exact local artifact hashes, proposed
UTC windows, monitoring, rollback branches, failed attempts and corrective
actions. Required Platform chart/composed-host/workflows, staging infrastructure,
qualified authentication/providers, current recovery and scope/attempt authority
remain missing. Qualification stays HOLD; the proposal is not an execution
approval request. The original Story 1.17 spec, frozen block, historical approval
bytes, V1Only, unset T0 and tracker remain unchanged. Existing 54 preservation
hashes and 29 historical/control hashes match; the 229-artifact candidate and
20 current bindings reproduce without changing the existing manifests.
