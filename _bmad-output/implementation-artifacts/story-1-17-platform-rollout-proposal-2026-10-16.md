# Story 1.17 Platform rollout proposal — 2026-10-16

**Decision: HOLD. Prepare a first installation on the observed Platform target;
do not execute production migration or deployment.** No running Folders or
Projects installation was identified there. This is a qualified inventory
finding, not global absence or an amendment to the accepted migration decision.
The [structured evidence](evidence/story-1-17-platform-qualification-2026-10-16.json)
binds observations, commands, response hashes, source identities, artifacts and
local validation. No deployment, external publication, Git staging, commit or
push occurred. Existing working-tree files, historical approvals, `V1Only`, T0
and Story 1.17's tracker remain unchanged.

**Deployment and consumer inventory.** Native reads at
`2026-10-16T06:31:32Z`–`06:37:28Z` covered the existing static native context
`jpiquot@local`, kube-system UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, one Ready
node and Kubernetes `v1.34.9`. The exact private API/credential/tool paths and
fully expanded commands are retained in owner-only operator custody. This is
the designated installation in Platform's accepted hosted-interface contract.

| Inventory | Actual observation | Coverage limit |
| --- | --- | --- |
| Folders/Projects workloads and routing | No identifying reference in 387 Deployments, StatefulSets, DaemonSets, ReplicaSets, Jobs, CronJobs, Pods, Services and Ingresses; 49 controller image records inspected | One context at capture time; opaque images, external hosts and historical installations are not exhaustively inspected |
| Shared hosting/configuration | No identifying reference in 63 ConfigMaps; no identified EventStore/Tenants/shared-Platform controller image | No application database, stream, backup or arbitrary container filesystem inspection |
| Retained volumes | No Folders/Projects reference among 27 PVCs and 28 PVs | Names/specs cannot establish that shared databases or recovery sets contain no domain data |
| Helm | 26 release metadata records; no Folders/Projects/Platform release | Release metadata only; no Helm secret payload retained |
| Other existing applications | Memories services and supporting infrastructure are present | Their presence supplies neither a qualified Platform release baseline nor reusable staging isolation |
| Target environments | `hexalith-staging`, `hexalith-staging-data`, `hexalith-production`, `hexalith-production-data` absent; Gateway API absent | These are current prerequisites, not permission to create them |
| Observability | Prometheus monitoring APIs absent from successful API discovery | External OTLP/monitoring may exist; no claim that all monitoring is absent |
| Local runtime | Four Dapr support containers; no identified Folders/Projects container; no running Aspire AppHost after the local probe | This machine only |
| Retained registry | Anonymous GET 401; existing credential-helper authenticated catalog GET 200; nine repositories, no pagination and no Folders/Projects/Platform repository | This registry and credential-visible scope only; no registry write-authority qualification |
| GitHub | Same nine Folders/four Projects package-release environment records; Platform deployment/workflow lists empty and latest release GET 404 | GitHub environment records do not prove running workloads; private operations outside accessible scope remain unverified |

The [October 15 trace](evidence/story-1-17-operational-discovery-2026-10-15.json)
already follows every listed deployment through Release jobs and exact historic
callers with `publish-containers: false`. Today's unchanged lists add no runtime
deployment evidence. The earlier categorical deployed-Projects premise remains
superseded; its dated decision records remain intact.

Organization discovery covered 67 accessible repositories. The broad Client
search returned its first 100 of 529 results and is **not** a complete census.
Complete narrowed searches returned 12 Client `.csproj` references, eight C#
references outside Folders and seven v1-route references outside Folders. The
only identified external source consumer is Projects; v1 references outside
Folders are historical Projects documents. GitHub indexing/default-branch and
access limits apply; dynamic callers and non-GitHub consumers remain outside
this source search.

Projects source `42c8e7bc3a58d78e7c6ea4273711d697e81175e7` identifies three
read-only families: lifecycle, effective permissions and file metadata. They
run on folder set/refresh/proposal and file-link/proposal validation. File
metadata uses a read-only POST. `RefreshFileReferenceAsync` sends no Folders
request. No deployed ordinary traffic or periodic schedule can be measured
against a nonexistent identified workload; zero is not a measured baseline or
proof that periodic callers do not exist.

**Installation decision.** The actionable preparation branch is first
installation of Folders/Projects in this observed target. Before execution,
Administrator must record target coverage and retained-data disposition and
accept the material change from the assumed existing-v1 premise. A discovered
v1 installation selects the original bounded migration procedure and requires
its real prior artifacts, configuration, consumers and schedules. Neither a
package tag nor a source snapshot is a prior deployment to restore. Until that
scope decision and the entry gates pass, production stays `V1Only`, T0 stays
unset and no retirement or closure evidence is claimed.

**Exact artifacts and release path.** The selected local qualification set is
`0.0.0-local.20261016.2`, tag `qualification-20261016-2`: five Folders `.nupkg` /
`.snupkg` pairs and six Linux/amd64 SDK-container archives. Full SHA-256 archive
and image-config hashes are in the structured record; the local manifest names
every file. All six images use non-root `app`; the observed base digest is
`sha256:eca1355b1fefb0a93617b7d01749dc8ac821e0eadcedbcf7d64e4b59cad60104`.
Folders and Projects image payloads are checked against the exact local
Client/Contracts package DLLs. Package safety/metadata/dependency validation and
two isolated package-only consumers pass. Projects builds and its 39 adapters
pass against that exact local pair.

These are retained local diagnostics, **not** a published or attested release.
Folders' source label is baseline `2236855ee181bdeed2a103537c66316a1612c792` plus
the separately hashed working-tree/source cut and dirty Builds input. Projects
uses temporary SDK properties and a private package/config overlay; its tracked
pins are unchanged. Archive/config hashes are not registry manifest digests.
The first export's version-metadata mismatch was detected, retained and
superseded by the byte-consistent `.2` set; it is excluded from selection.

Selected immutable **local** files (paths relative to operator custody):

| File | SHA-256 |
| --- | --- |
| `packages-final/Hexalith.Folders.Client.0.0.0-local.20261016.2.nupkg` | `0baf988d8d6a1682ce14adfd81342e0d1876d236838094b5a96065c5fab49c08` |
| `packages-final/Hexalith.Folders.Contracts.0.0.0-local.20261016.2.nupkg` | `82032292481aee702b296db6cf7e1724f5500c46f9d47dcfbc61d54e356c6219` |
| `images-final/hexalith-folders-server.tar.gz` | `ed1f2ee9ebca32698788d2ac2dcb072baec3da8699d6908184f73416fcc4f23a` |
| `images-final/hexalith-folders-ui.tar.gz` | `9ac70ec57bebe554f79d4fb34815e42ccfac911c36ff309a09b9be800f4c0fac` |
| `images-final/hexalith-folders-workers.tar.gz` | `66bce63b9d0deb9a3ee999a96dc868b3626e3f5e281a76a07b63a006818e9eee` |
| `images-final/projects-ui.tar.gz` | `ef69d16f8366ce2055d1ddcdda1b664490b32fce91b0f7a41567e7bed9900fd4` |
| `images-final/projects-workers.tar.gz` | `3d6c1a568bf40e3d076218edd611cd63158ac6be5880394cb93a99baa4a02a81` |
| `images-final/projects.tar.gz` | `5e9aa1e4f4f92d4293ef5822bcdbb53cb7a72a98b5f3a3b25027cce17db01d6e` |

The currently published Client/Contracts `1.2.1` remains a separate tuple from
Folders `17b5a70d2b4b91bbbf73ea63c8a214a0ad426759`:

| Package | Published archive SHA-256 |
| --- | --- |
| Client `1.2.1` | `99c5996dbdcdd58759b8436a0ee3496616eefc071c29c8d40c0c3a77bf0d7391` |
| Contracts `1.2.1` | `325b4509ce924b92d31fb76ba40b21d1ee1eb1e90057ab53a19f6cf01f9ebb3d` |

The default Projects Release build and 39 adapters also pass with that published
pair. It does not establish a release of the current complete Folders candidate.

Use the existing [Folders package release lane](../../docs/operations/release-packages.md)
and Projects' existing `release.yml`, after their exact protected source has
green push CI. Semantic-release chooses the official version; this proposal
does not reserve or publish a guessed stable version. Folders' repository
publication flag is `true` and `NUGET_USER=jpiquot`; Projects' repository flag is
`false`, with environment `NUGET_USER` present. Both use trusted publishing;
absence of stored NuGet secrets is expected. No flag or policy was changed.

Platform must then consume exact module artifacts through its intake and
publish its retained application Helm package, composed `platform/eventstore`
image, catalog, McpCli and signed release record under AD-2/AD-7. The inspected
local Platform revision is `3a29fe039320ab13d95ff7d9801761dad72bfed9`; remote main
is `12279c8830a26c5ffbb85d8a4f09c60e9fff1dff`. A read-only comparison finds no
new hosted release/chart/workflow implementation in those three intervening
commits. The remote recursive tree is complete. Those **required retained
release artifacts and an executable deployment workflow are currently absent**.
Do not invent their digests, substitute direct module manifests, deploy a
second EventStore wrapper, or bypass the accepted executor with a manual Helm
command. No production deployment command is ready for approval.

**Platform configuration and staging target.** The concrete non-executable
[configuration packet](../../eng/story117-platform-qualification/configuration.proposed.json)
binds 25 source files, real .NET keys, secret references and owners. Its
[14 ordered staging checks](../../eng/story117-platform-qualification/staging-checks.json)
are all explicitly unexecuted. The [offline verifier](../../eng/story117-platform-qualification/README.md)
proves proposal consistency only; its 13 mutation tests reject fabricated
evidence, route activation and authority.

Use staging application namespace `hexalith-staging`, data namespace
`hexalith-staging-data`, and the same cluster UID above. Proposed first
production namespaces are `hexalith-production` and
`hexalith-production-data`; these are proposed bindings, not observed resources.
Platform owns exact Gateway/listener/FQDN assignments (`hexalith.com` staging,
`tache.ai` production), isolation, certificates, identity and environment-layer
objects. Never expose callbacks or use the shared compatibility Ingress as a
shortcut. Folders keeps stable IDs `folders`, `folders-workers`, `folders-ui`;
Projects source IDs are `projects`, `projects-workers`, with its UI separately
bound by Platform. A single composed `eventstore` owns shared extensions.

Apply the [accepted Platform MVP precedence](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/reconcile-folders.md):
single-node failure domain, 1h RPO, 4h RTO within declared response coverage,
30-minute backups, seven-day frequent/thirty-day daily retention and prepared
capacity. No Folders-only HA, minimum-two-replica, backend-version or five-minute
RPO gate is added. Common profile/provider compatibility and module safety
remain required. Source manifests are conformance inputs, not another deployer.

Known functional bindings include `Folders:ApiRouting:Mode=V1Only`,
`Folders:Workers:PubSubName=folders-workers-pubsub`,
`EventStore:Publisher:TopicOverrides:folders=folders.events`, shared physical
Server/Workers read-model storage with `keyPrefix=none`, both app-token
references, EventStore-issued workload assertions, the shared command-bound
human receipt and idempotency key ring. The packet records legacy Kubernetes
secret references as inputs; Platform must map them through its accepted
OpenBao/secret contract. No secret material is embedded or generated.

Projects' default `AddFoldersClient` hard-codes its transport and attaches no
authentication handler. The SDK deliberately delegates authentication to its
consumer's `IHttpClientBuilder`. A qualified Platform registration/delegation
path is missing; simply setting `Folders:BaseAddress` or blindly relaying a
user token does not repair it. Prove original-actor/surface and audience binding
plus authenticated `client_id`/`azp=Hexalith.Projects`, which the telemetry maps
to `consumer=projects`. Current UI confidential-client integration and provider
tenancy also remain unqualified. Mocked adapter success cannot close these gaps.

**Owners and credential custody already found.** Platform CODEOWNERS names
`@jpiquot`; the accepted spine assigns Administrator production authority and
recovery ownership. Propose Jérôme Piquot/Administrator as rollout operator
and decision owner, subject to the attempt record. Existing static native
client custody and Docker Desktop's registry helper both support the read-only
queries. GitHub API access and NuGet trusted-publishing configuration are
available. No credential value was printed, saved to Git, or sent to an
unrelated endpoint. Existing Keycloak/OpenBao workloads do not supply qualified
staging realms, per-job deployment credentials or app secret bindings.

The existing recovery maintenance interval ends exclusively
`2026-10-31T23:00:00Z`; it is not Folders release approval or an appointment.
No Folders on-call appointment, response-coverage record, qualified deployment
executor or appointed recovery deputy was identified in inspected scope.
Platform must bind these through its existing roles/access/record model;
this proposal appoints nobody and reuses no expired recovery proof.

**Proposed UTC timing, not scheduled or authorized.** Reserve a staging
qualification window `2026-10-20T08:00:00Z`–`18:00:00Z` and a later production
decision window `2026-10-22T08:00:00Z`–`09:00:00Z`. All prerequisites and exact
official artifact/configuration/check-suite identities must be reviewable by
`2026-10-19T16:00:00Z`; otherwise cancel these proposed slots and obtain a new
explicit slot. No job, calendar entry or expiry automation was created.

Staging first confirms target/data/baseline disposition, qualifies the profile
and isolated authority, cuts its recovery point and then rehearses **fresh
install plus candidate** when no production working baseline exists, as Platform
explicitly requires. Execute the packet's finite checks with exact retained
release bytes: readiness, durable persisted end-state, authorization negatives,
all three Projects reads and distinct raw 401/404/503 outcomes, attribution,
restart/route reversal and retained-data recovery. Keep all failures and exact
suite/profile/configuration digests. The 14 declared maxima total 7h25m within
the proposed ten-hour slot. Staging route tests never start production T0.

Production remains a separate Administrator-approved pre-G3 attempt only after
those results, common entry gates and first-install scope disposition exist.
That mode retains staging, locks/epochs, provenance, verification and signed
records. This proposal does not decide an alternative production route mode or
waive the original migration's clock. If the original coexistence branch is
used and actual T0 were `2026-10-22T08:00:00Z`, Projects must deploy by
`2026-10-23T08:00:00Z` and expiry is `2026-10-29T08:00:00Z`; retirement needs at
least 24h of ordinary attributed traffic after successful smoke and every
periodic caller. These are example calculations, not an actual T0. No traffic
or missing periodic schedule is synthesized for the first-install branch.

**Monitoring and stop criteria.** Use existing `folders.api.requests`,
`folders.api.errors`, `folders.api.duration` with `api.version`, `consumer`,
`http.status_code`, and linked traces. Verify actual Projects attribution and
external alert delivery before opening ingress. Collect a 30-minute staged
synthetic baseline separately from any real production baseline. Zero traffic
has no meaningful error-rate or p95 observation; report insufficient samples.

Platform's accepted production verification is five minutes after readiness,
availability samples no more than ten seconds apart, failure after 60 seconds
of continuous required-service unavailability, the same smoke failing twice
with the retry 30 seconds after the first failure, or no latest passing result
at deadline. A single restart/probe alone is not failure. Proposal-only
additional module thresholds for owner acceptance are: unexpected 5xx >1% over
five minutes with at least 100 requests; p95 >1000ms or >2x the measured baseline
for two consecutive five-minute windows with at least 100 samples each; missing
telemetry/attribution for 60 seconds stops admission. The existing 500ms
projection signal remains a warning/recovery investigation threshold, not a
new readiness gate. Expected negative-test 401/404/503 are separately labelled.

Authorization/disclosure regression, unexpected denial/unavailability on an
allowed synthetic read, a failed required smoke, an unplanned v1 caller,
unexplained errors, loss of required evidence or an infeasible coexistence
deadline stops the attempt. Record UTC decision/notification times and exact
artifact/configuration identities. No proposed numeric threshold is represented
as measured, approved or live alert configuration.

**Rollback and recovery.** For a first installation with no working application
baseline and no retained data, close user ingress, set the promotion stop,
remove only the candidate's application workloads through the qualified
executor, retain all persistent/environment-layer/shared objects and diagnostics,
and end the attempt without claiming an old-v1 restore. If data exists, require
compatibility evidence or a separately named data-recovery procedure and usable
post-lock recovery point before attempting production. Never delete data to
make the first-install branch pass.

For a real discovered v1 installation, rehearse the original exact-artifact
procedure before T0. Ordinary reversal keeps v1 available, restores the verified
Projects v1 artifact, checks all three v1 reads, then restarts Folders with
`V1Only`. An authorization/disclosure incident disables v2 first, restores
Projects v1 and verifies those reads. At T0+168h without complete exit evidence,
perform that restoration and seek a new decision; no clock extension. A
compatible Platform application rollback uses the prepared AD-15 rollback set
and forward catalog generation, not an unverified Helm history revision.
Application reversal never rewinds completed writes, broker history, erased
keys, holds, revocations or external effects. Keep callers read-only and
quarantine unknown outcomes until module-approved reconciliation. The accepted
four-hour recovery bound applies within declared coverage; missing current
recovery/coverage evidence blocks the attempt.

**Remaining blockers and corrective actions.** Exact expanded commands,
response hashes and local logs are retained in operator custody; the evidence
record provides sanitized invocations and results.

| Blocker / exact query or check | Result | Next corrective action and owner |
| --- | --- | --- |
| `kubectl --context jpiquot@local get namespace hexalith-staging hexalith-staging-data hexalith-production hexalith-production-data -o json` | Exit 1: all four `NotFound` | Platform completes approved shared prerequisites and environment-layer provisioning/isolation; do not create namespaces in this task |
| `kubectl --context jpiquot@local get gateways.gateway.networking.k8s.io -A -o json` | Exit 1: server has no `gateways` resource type | Platform qualifies its Gateway API/Traefik/environment Gateway mechanism after the existing retirement/recovery/upgrade/Rancher prerequisites |
| `gh api repos/Hexalith/Hexalith.Platform/actions/workflows?per_page=100`; `gh api repos/Hexalith/Hexalith.Platform/releases/latest`; authenticated registry catalog GET | `[]`; exit 1 / HTTP 404; HTTP 200 with no candidate repositories | Platform supplies hosted Folders/Projects declaration/bindings, export/chart, intake, publication/deployment workflow and retained attested release record; applicable backlog includes 4.5, 4.6, 4.8, 4.12, 4.14–4.17, 4.22 and exact-release staging/production attempts |
| `gh api repos/Hexalith/Hexalith.Folders/actions/runs/38765800332/jobs?per_page=100`; job log GETs | Exact-main CI failed: whitespace and two capacity fixture failures | Local fixture/format repairs now pass; Builds/Folders must later publish the preserved dependency fix and obtain reviewed committed-source green CI through the existing lane. No commit/push is authorized here |
| `gh api repos/Hexalith/Hexalith.Projects/actions/jobs/116339559964/logs` | CI initialization exit 128: EventStore `5b0df296abc86adf8d37a92fae02e26574d19ad5` is not fetchable | EventStore/Projects owners make the approved history reachable or review a reachable gitlink, then rerun exact-source CI and G-6. Local package-mode success is separate |
| `rg -n -e AddFoldersClient -e AddHttpMessageHandler references/Hexalith.Projects/src/Hexalith.Projects.Server/ProjectsServerServiceCollectionExtensions.cs` | Only the EventStore client has a handler; default Folders registration does not | Projects/Platform qualify explicit transport, target-scoped delegation, actor/surface/audience and client attribution; retain default-deny and repair hosted UI/provider bindings |
| Existing target/data inventory, recovery/coverage and execution-record inspection | No accepted first-install scope/data disposition, complete hosted release, current qualifying recovery, Folders schedule/on-call/deputy or operational baseline/reversal | Administrator/Platform bind the named attempt and scope, recovery/coverage/notification duties and real consumer schedules; requery target before execution. No request for the user to locate these inputs |

The local Aspire baseline attempt is a separate limitation:
`aspire start --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --non-interactive`
exited 2 with `CS1704`, duplicate `Hexalith.Commons.UniqueIds`; describe and final
`aspire ps` found no running AppHost. This is not a passing topology or staging
qualification. Resolve the shared source/package closure in the hosting owner
before a real-service run; package-mode builds and hermetic checks do not waive it.

Local validation passes: audited required Folders and default Projects Release
builds; formatting; capacity 12/12; full Folders Server 979/979 plus final
affected classes 145/145; routing/golden 114/114; contract/package/NFR 40/40;
Projects adapters 39/39 for both published and selected local pairs; package
consumer/archive validation; offline packet 13/13. Current conformance reproduces
twice across 229 artifacts, all 20 current bindings match, and all 54 initial
preservation hashes and 29 historical/control hashes match. The dated pre-SignalR verifier fails its old candidate
digest as expected against the already resealed initial working tree; it was
not rewritten. Current validation uses the preserved SignalR receipt's
`4b8470beda4f19c1293be1c40b4b36914e5148fa0f1a05ab2817d4fe43156cce`
candidate and records that supersession explicitly.

The next approval packet must name actual official registry/chart/check-suite
digests, a usable qualified target, scope/data branch, current recovery and
signed executor/attempt identities. These missing artifacts prevent a concrete
production execution approval request today. Review this qualification and
its local repairs; keep production entry and Story 1.17 closure open.
