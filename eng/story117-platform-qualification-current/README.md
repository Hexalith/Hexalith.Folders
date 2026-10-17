# Current Story 1.17 Platform qualification proposal

This separate October 17 packet checks offline consistency against the reviewed
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
JSON files and exactly 25 allowlisted source files, binds their SHA-256 bytes,
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

The 39 tests keep safety mutations independent of live checkout drift. Controlled
disposable sources satisfy the 25 bindings before each mutation; each subtest
restores proposal and source bytes in `finally`, including assertion failures.
Separate current-source tests bind all 25 real files and prove missing/changed
source refusal in a disposable copy. Mutation coverage includes route, authority,
T0, executed evidence, check removal/order/ownership/bounds, secret disclosure,
strict JSON, symlinks, dependency mismatch, artifact substitution, unresolved
admission, first-install scope and read-only Projects operations. Synthetic
fixture success supplies no operational evidence.

The reviewed input deltas are:

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
use. All historical packet, receipt, approval, candidate-manifest and tracker
bytes remain preserved. The historical verifier still exits 1 on the Builds
fingerprint; its 13 tests still report 21 failures and 8 errors because source
drift masks the intended mutation assertions. Those are historical refusals,
not passing safety tests or operational evidence.

The required publication replay currently exits 1 at
`references/Hexalith.Platform/DaprComponents/accesscontrol.eventstore-admin.yaml`.
The retained publication receipt's 4,888-file Platform snapshot differs from the
current clean Platform checkout in 45 paths, including one absent file. This
drift predates the packet refresh; the verifier, snapshot and dependency gitlinks
were preserved. The receipt's 42 protected workspace files remain unchanged,
and this run's separate 45-file preservation baseline also matches. Exact
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
