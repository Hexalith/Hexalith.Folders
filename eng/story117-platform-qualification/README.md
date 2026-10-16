# Story 1.17 Platform qualification proposal

This inert packet prepares first-install qualification in the observed target.
**Operational qualification remains HOLD.** Production stays `V1Only`, T0 stays
unset, and the packet carries no deployment, publication or activation authority.
It does not complete Story 1.17 or alter its original migration decision,
historical approvals, execution record or seven-day coexistence limit.

`configuration.proposed.json` separates actual module keys, existing secret
reference inputs, catalog/dependency identities and unresolved Platform bindings.
Null values are unresolved and each has an owner and boundary explanation. The
colon-separated .NET keys map to double underscores in environment variables;
`APP_API_TOKEN` and `OTEL_EXPORTER_OTLP_ENDPOINT` are literal environment keys.
These are proposal data, never application configuration to load or apply.

`staging-checks.json` defines 14 ordered checks with finite bounds, accountable
roles and exact required evidence. All are unexecuted with no results. Staging
Coexistence/V2Only rehearsals never change the production hold or start its T0.
Future execution and signed results belong to qualified Platform owners outside
this packet; this diagnostic accepts no operational evidence or approval flags.

Run from the Folders root, with Python 3.10 or newer and no third-party packages:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s eng/story117-platform-qualification -p 'test_*.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 eng/story117-platform-qualification/verify.py
```

A successful diagnostic means **packet consistency only**. It reads the two JSON
files and a fixed allowlist of explicit source files, checking their SHA-256
fingerprints. It makes no subprocess, network, infrastructure or deployment calls.
Missing or changed source inputs fail closed; reconcile changes through a new
reviewed proposal rather than treating a fingerprint update as qualification.
Full canonical revisions identify local snapshots, not clean/committed build
provenance. The local Platform revision is older than remote main according to
the handoff; it is not labelled current remote Platform. Builds' existing dirty
package-audit edits remain recorded inputs, not an accepted release.

Platform must supply its accepted release and hosting mechanism: retained
application package, composed EventStore image and extension identities,
attestations, exact successful CI, profile/provider qualifications, environment
bindings, scoped executor, locks/epochs/stops and signed records. No Helm assets
or hosted publication workflow are supplied by this packet. Existing Folders
AppHost/server wrappers and deployment fragments are conformance inputs, not a
second hosted topology or an accepted composed EventStore release. The shared
host must receive `EventStore:Publisher:TopicOverrides:folders=folders.events`;
Server and Workers must share physical read-model keys with `keyPrefix=none`.

Authentication and provider boundaries remain unresolved. Projects' default
`AddFoldersClient` supplies a hard-coded transport address and no authentication
handler; the SDK's `Folders:BaseAddress` setting does not change that registration.
Platform/Projects must qualify target-scoped delegation, original actor/surface
and authenticated `client_id`/`azp` attribution (`Hexalith.Projects` maps to
`consumer=projects`). UI options expose no confidential-client secret key and
currently forward bearer tokens; hosted confidential UI integration and compliant
delegation need owner evidence. Real OIDC, app-channel tokens, workload assertions,
command-bound human receipts, OpenBao/secret-contract scopes and per-environment
provider tenancy must be qualified without fixture auth or secret material here.

The first-install branch requires target/data confirmation and material scope
approval. With no production working baseline, Platform staging rehearses fresh
install plus candidate. A discovered installation returns to the original
migration branch; package releases do not identify a prior v1 deployment. Empty
first-install failure removes workloads with ingress closed and keeps data.
Retained data requires compatible rehearsal or a separately named recovery and
usable post-lock recovery point. No ordinary traffic, periodic schedule, prior
artifact or completed reversal is inferred from zero observed workloads.

The accepted Platform envelope is single-node, 1-hour RPO/4-hour RTO within
declared response coverage, 30-minute backup runs, 7-day frequent/30-day daily
retention and prepared-capacity restoration. It adds no Folders-only HA, backend
or 5-minute RPO enrollment gate. Module authorization, durable end-state,
erasure/hold/revocation safety and external-effect reconciliation still require
evidence. Missing release, hosting or provider evidence keeps qualification HOLD;
Administrator-approved pre-G3 mode retains staging, provenance, locks,
verification and records.
