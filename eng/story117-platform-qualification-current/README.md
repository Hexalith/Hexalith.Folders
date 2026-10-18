# Current Story 1.17 Platform qualification proposal

This separate packet, reconciled after Story 12.1 on October 18, checks offline
consistency against the reviewed
local inputs. **Operational qualification remains HOLD.** Production is
`V1Only`, T0 is unset, activation authority is null, first-install scope is
unconfirmed, and all fourteen staging checks are owned, finite and unexecuted.
Story 1.17 remains open with tracker `backlog` and its approved migration scope,
original baseline and seven-day coexistence limit unchanged.

Run from the Folders root using Python 3.10 or newer, without third-party packages:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 eng/story117-platform-qualification-current/verify.py
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 eng/story117-builds-publication/verify.py
git diff --check
```

The first command means **packet consistency only**. It reads the two proposal
JSON files and exactly 31 allowlisted source files, binds their SHA-256 bytes,
checks strict JSON keys/types and inert controls, and compares the observed
dependency selections and package/property references with the bound Builds
catalog. It binds each module to its reviewed source and each secret reference
to its complete reviewed name/key/consumer/target mapping. Canonical JSON
checksums protect the reviewed requirement groups and staging plan independently
of JSON formatting. Substantive edits require a reviewed update to those values
and their expected checksums. It runs no subprocess or network calls and accepts
no arguments.
Missing/changed sources produce relative-path diagnostics; packet/secret errors
do not print values or absolute filesystem paths. Packet and source inputs must
be regular files; source symlink redirection is refused before reading. These JSON files are proposal
data and must never be loaded as application configuration.

The 46 tests keep safety mutations independent of live checkout drift. Controlled
disposable sources satisfy the 31 bindings before each mutation; each subtest
restores proposal and source bytes in `finally`, including assertion failures.
Separate current-source tests bind all 31 real files and prove missing/changed
source refusal in a disposable copy. Mutation coverage includes route, authority,
T0, executed evidence, check removal/order/ownership/bounds, secret disclosure,
strict JSON, symlinks, dependency mismatch, artifact substitution, unresolved
admission, first-install scope and read-only Projects operations. Synthetic
fixture success supplies no operational evidence.

The reviewed input deltas are:

- Story 12.1 is complete with retained local authenticated durability evidence in
  `story-12-1-durability-2026-10-18-complete.json`. The current source observations
  are Folders `65f34e7c4b2bd41805aaa1c3b8d87ee3b65363f1`, Platform
  `2749ad8abdafc038ded44f6154d7182c2fd71422` and EventStore
  `269b20d58236f07228bc2afd54807b94bd96f37b`. This uncommitted reconciliation is
  local technical preparation. It establishes no deployed release qualification.
- The Server registers the EventStore gateway client with
  `EventStore:Gateway:BaseAddress` defaulting to `http://eventstore`. New bound
  input 26, `FoldersGatewayHumanBearerHandler.cs`, permits bearer submission only
  for an authenticated direct public mutation with a parsed nonempty Bearer, to
  the gateway that issues its command-bound receipt. This guarded submission
  grants no general downstream delegation. The Server registers Query and
  Metadata workload policies alongside Process and Project.
- `Authentication:HumanAuthorityReceipt:ExpectedWorkload` defaults to `eventstore`;
  `/process` requires the matching authenticated workload plus a signed receipt
  bound to the command. Workload validation selects
  `Authentication:WorkloadJwtBearer`, falling back to `Authentication:JwtBearer`
  only when the dedicated section is absent. Partial dedicated configuration
  fails closed. Dedicated authority/issuer, credentials and environment binding
  remain unresolved. `Authentication:WorkloadIssuer:EnableResourceBindingRequests`
  defaults false; enabling it alone establishes neither authority enrollment nor
  a grant. Actual resource-bound requests require owner qualification.
- Inputs 27–31 explicitly bind the EventStore workload registration, bearer
  configuration, assertion issuer, JWT contract and JWT options implementations.
  Input 26 remains the Folders bearer handler. The dedicated contract inventories
  unresolved Audience, ValidAudiences, AllowedAlgorithms and SigningKey alongside
  authority/issuer; RequireHttpsMetadata stays true and AllowInsecureSymmetricKey
  stays false. These null proposal values grant no usable runtime configuration.
  Contract validation requires an audience before narrowing it to the receiver,
  an explicit asymmetric algorithm list in authority mode, and HTTPS metadata;
  Production cannot use a symmetric signing key.
- Platform's current Builds gitlink is
  `83850cc62e7855ca64dee70e33f2e5c9bab1bc7c`, distinct from direct Builds
  `4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77`. Platform Story 1.8 remains
  `in-progress` and Story 1.9 remains `backlog`. The mismatch is recorded without
  changing dependencies or accepting the catalog/tool tuple.
- The local Builds catalog now selects EventStore `3.124.0`, replacing the old
  packet's `3.121.0`. Other recorded dependency selections remain unchanged.
  Current clean Builds source is
  `4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77`. Historical published Builds `4.32.0`
  came from `abdfca89ed82f388b54664825a8a79fed73e7ccf`, whose locally retained
  source catalog selected `3.123.0`. That catalog observation is separate from
  signed package identities and their historical publication receipt. No source
  observation substitutes for an accepted tool, release artifact or attestation.
- The bound Platform spine adds AD-9 canonical hosting
  (`Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and
  `CommunityToolkit.Aspire.Hosting.Dapr`), with Commons supporting primitives;
  logical-role/recovery-class bindings through actual owner keys and scoped
  servers; explicit receiver/route/topic/dead-letter subscription identities;
  deny-default caller/path/verb ACLs; local mTLS with `localhost` control-plane
  trust domain, `default` namespace and `public` workload SPIFFE ACL matching;
  and declarative subscriptions with discovery disabled and delivery routes
  retained. Applicable project/container/signed-plan resource paths need actual
  scoped files. Wildcard syntax does not prove provider support. No model or
  fixture check proves live enforcement, delivery or image availability; hosted
  policy migration and concurrent-run isolation remain with their owners.

This packet supersedes only the historical packet's current-input diagnostic
use. Historical packet, receipt, approval and tracker bytes remain preserved.
The current candidate inventory is regenerated twice with identical bytes:
229 paths remain in scope, only the three changed Server rows are refreshed,
and all 20 current manifest provenance bindings match. Historical candidate
approval bindings and lifecycle fields remain unchanged. The retained
[post-12.1 readiness evidence](../../_bmad-output/implementation-artifacts/evidence/story-1-17-post-12-1-readiness-2026-10-18.json)
records exact commands, hashes, local checks and preservation results.
The historical verifier's October 17 observation remains exit 1 on the Builds
fingerprint; its 13 tests still report 21 failures and 8 errors because source
drift masks the intended mutation assertions. Those are historical refusals,
not passing safety tests or operational evidence.

The October 17 publication replay exited 1 at
`references/Hexalith.Platform/DaprComponents/accesscontrol.eventstore-admin.yaml`.
The retained publication receipt's 4,888-file Platform snapshot differs from the
current clean Platform checkout in 45 paths, including one absent file. This
drift predated that packet refresh; the verifier, snapshot and dependency gitlinks
were preserved. That run preserved the receipt's 42 protected workspace files
and its separate 45-file preservation baseline. Exact
old/current hashes and unchanged command outcomes remain in the
[current evidence](../../_bmad-output/implementation-artifacts/evidence/story-1-17-current-qualification-2026-10-17.json).
The same unchanged verifier passes with its supported `--workspace` option
against a disposable historical snapshot: all 4,888 hashes match after restoring
the 45 differing blobs from retained Platform Git revision
`a29daf6b1bb3471dded2196cf17064aa2b632554`. This separately labelled historical
replay preserves the current checkout and confirms the original publication
evidence. On October 17 the user accepted this historical replay plus the
unchanged 42 protected files and 45 current-run preservation inputs as the
publication-preservation criterion for this bounded refresh. The passing current
diagnostic and safety tests establish its current-input consistency. The failed
current-checkout publication replay remains a recorded observation. Current
Platform qualification is separate owner work and remains HOLD; this decision
grants no tool/range acceptance, staging authority or Story 1.17 closure.
Relative to the October 18 task baseline, 40 of those 42 paths stay byte-identical;
the other two are the explicitly refreshed current conformance inventory and
its planning-manifest provenance entry. Equality to the historical receipt is
39 of 42: the tracker already differed before this task and is preserved at its
current baseline. All original receipt bindings remain unchanged and confer no
approval on the refreshed bytes.

The remaining owner prerequisites are explicit nulls in the configuration:

- Platform must qualify Story 1.8 startup/readiness for the exact composition and
  complete Story 1.9 acceptance of a released tool, its exact tool manifest pin,
  accepted `toolVersionRange` and coherent Builds catalog gitlink. Publication
  alone grants no tool or range acceptance.
- Platform and module owners must complete intake with exact released owner v2
  declarations, image/package attestations and classification/evidence chain.
  The October 16 admission handoff is historical; its releases/observations are
  not current intake acceptance. Tenants/Memories declaration and attestation
  gaps recorded there still need owner disposition.
- Platform, Builds and EventStore must supply qualified clean Platform source,
  successful exact-revision CI, signed retained application package, composed
  EventStore host/extensions, supported hosting and common profile/provider
  qualification. Existing module wrappers/deploy fragments remain inputs.
- Platform identity, Projects and FrontComposer owners must qualify real OIDC,
  confidential UI integration, scoped downstream delegation, workload/human
  receipt binding, app-channel protection, secret contracts, telemetry and
  provider tenancy/egress. Projects' explicit SDK registration and UI bearer
  forwarding do not establish compliant hosted authentication.
  Resolve the actual gateway address, dedicated workload JWT contract and
  command-bound receipt/workload identity, and qualify any resource-binding
  authority profile and scoped grants before enabling requests.
- Administrator and Platform must confirm target and retained data and approve
  any material first-install scope change. A discovered installation returns
  to the approved migration branch. Supply named operators/UTC slots, numeric
  thresholds, exact deployed artifacts, attributed ordinary baseline/census,
  periodic schedules, locks/epochs/stops, reversal/recovery, observation,
  retirement and closure evidence. The accepted single-node 1-hour RPO/4-hour
  RTO, coverage, 30-minute backup cadence and 7-/30-day retention remain intact.

Future staging execution and acceptance belong to separately authorized owners
and external signed records. This packet grants no publication, deployment,
staging execution, route change, production clock or tracker transition.

Portable evidence is retained beside the receipt as
`story-1-17-post-12-1-readiness-2026-10-18.artifacts.tar.gz` and its member manifest.
It contains the initial baseline, original and corrected audit programs, both
candidate generations, all safe captured raw command outputs, and the exact
initial/current readiness-document bytes. Every command output is bound to an
archive member and SHA-256. Historical reads use the receipt's full baseline and
recorded dependency revisions; root HEAD may advance when this change is recorded.
The preservation gate compares complete tracked and nonignored untracked path
sets, allowing exactly the approved spec, receipt and two portable artifacts.
Its reversible unexpected-source probe must refuse the added path.

Receipt `beforeAfterSha256` values explicitly describe captured worktree bytes.
Separate Git-normalized SHA-256 and blob object IDs are verified with read-only
`git hash-object --path`; CRLF is converted to LF only when the resulting blob
identity matches Git. The existing readiness prefix is retained without rewriting.

To replay from the Folders root, verify and extract the bound local archive, then
run its retained programs (Python standard library only):

```bash
python3 - <<'PY'
import hashlib, json, pathlib, subprocess, tarfile, tempfile
root = pathlib.Path.cwd()
e = json.loads((root / "_bmad-output/implementation-artifacts/evidence/story-1-17-post-12-1-readiness-2026-10-18.json").read_bytes())
b = e["portableEvidence"]
a = root / b["archivePath"]
assert hashlib.sha256(a.read_bytes()).hexdigest() == b["archiveSha256"]
destination = pathlib.Path(tempfile.mkdtemp(prefix="story117-replay-"))
with tarfile.open(a) as archive:
    archive.extractall(destination, filter="data")
subprocess.run(["python3", str(destination / "replay.py"), str(root)], check=True)
PY
```

Replay verifies retained member checksums, both independent candidate audits and
the complete preservation inventory. It requires the captured worktree byte
representation; newer source changes must refuse rather than inherit acceptance.
The finite staging matrix remains a proposal: four matching operation cases,
twelve wrong-operation denials and eight Query/Metadata resource-binding denials,
all within the existing 1200-second bound with per-case 30-second deadlines.
