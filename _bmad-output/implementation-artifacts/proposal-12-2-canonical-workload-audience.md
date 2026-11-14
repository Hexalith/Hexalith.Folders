# Story 12.2: canonical gateway audience correction

Status: approved by Jerome on 2026-11-13 (“approve”); applied and focused qualification passed. Current strict acceptance remains blocked by the EventStore process-stop timeout.

The selected Platform producer grants the run-specific Dapr gateway app ID as the
JWT audience. The current native SDK and Tenants bootstrap request the logical
audience `eventstore`. Caller identity and token audience are separate protocol
fields. The fixture's physical app IDs must retain their run-specific identities.

The unchanged strict class first failed during owner bootstrap in 141.069 seconds,
with no skipped tests. A separate disposable run-owned authority control then
observed HTTP 200 for the physical gateway audience and HTTP 400 for `eventstore`,
using the same enrolled caller, operation and binding set. Cleanup observed zero
remaining owned containers, temporary paths and descendants. The first strict run
also exposed stale .NET 10 catalog metadata selection; that independent Folders
path has been corrected. The second strict run has matching loaded identities
and still fails bootstrap observation in 142.301 seconds, zero skips. The audience probe
does not establish sole causality for every bootstrap or later acceptance failure.

## Proposed bounded change

[The proposed Platform patch](proposal-12-2-canonical-workload-audience.patch)
replaces the gateway audience in the C# inventory, Python validator and component
inventory with `eventstore`, and configures that exact audience on the EventStore
receiver. The existing inventory test is aligned with the native SDK protocol.
The patch uses zero-context unified hunks and is relative to the Platform root;
it remains the original review artifact. The approved correction and its regression controls
are now implemented in the Platform working tree; the patch artifact is preserved unchanged.

All actual caller/client IDs, callback audiences, query digests, operation counts,
exact tenant/domain bindings, independent RSA proofs, TLS, signer validation,
grant cardinality, token lifetimes and revocation rules remain enforced. The
physical gateway app ID is replaced as an audience; a second audience or a
second grant is not added. Owner enrollment remains required outside the
disposable fixture. No dependency update, commit, push or deployment is proposed.

Approval permits only this audience-selection correction to the Platform
consumer and producer, the corresponding runbook/test updates, and new controls
that acquire assertions through the real SDK using the canonical audience while
rejecting physical-app-ID substitution. Qualification must rerun the focused
provider/consumer controls, audit the rebuilt source/runtime and execute the
unchanged strict class. Any remaining failure stays a failure.

## Approval boundary

The [approved external-authority spec](spec-12-2-external-workload-authority.md)
requires: “Unchanged consumer verifies TLS, signer and every probe.” Its fixture
task also requires calling the unchanged `PlatformWorkloadEnrollment.ConfigureAsync`.
The proposed receiver-audience correction changes that consumer's configuration
selection; Jerome explicitly approved this bounded exception on 2026-11-13. Its cryptographic
verification and refusal rules remain intact. The child frozen intent was renegotiated only
for this approved exception; the parent frozen intent, strict bodies, original deadlines
and retries are unchanged.

## Applied qualification — 2026-11-14

The [focused evidence](evidence/story-12-2-canonical-audience-focused-20261113.json)
records passing real SDK issuance, canonical audience and physical-audience substitution
refusal controls against the actual run-owned authority. The corrected audience lets the
unchanged strict class complete bootstrap and its original provider-denial control. A
subsequent expired cached fixture assertion was corrected by renewing credentials before
new ordinary discovery/rebuild actions, preserving assertion lifetime and request budgets.

The [current runtime evidence](evidence/story-12-2-canonical-audience-runtime-20261114.json)
records the final strict result: one failure, zero errors/skips, 474.271 seconds. It verifies
six-concern rebuilds, interrupted-generation recovery and concurrent Workers convergence
before the second EventStore stop command times out. No generic SDK shutdown repair was
authorized or applied. Story 12.2 remains in-progress.
