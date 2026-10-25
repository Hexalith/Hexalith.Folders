# Current Story 1.17 Platform qualification proposal

The October 25 reconciliation checks the current **offline source proposal**.
Operational qualification remains **HOLD**: runtime `V1Only`, T0 unset,
activation authority null, target/data disposition unresolved and all fourteen
staging checks unexecuted. This packet grants no publication, deployment,
execution or tracker-transition authority; the accepted rollout remains gated.
The current packet is proposal data, never application configuration.

From the Folders root, with Python 3.10 or newer and its standard library:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 eng/story117-platform-qualification-current/verify.py
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v
git diff --check
```

The verifier reads two strict JSON files and 52 fixed source/configuration/owner
record inputs. It checks byte fingerprints, exact declarations, inert controls,
secret reference mappings and independently sealed requirement/staging content.
It runs no subprocess/network calls and accepts no arguments. Missing/changed
sources, symlinks, special files, unknown authority and fabricated execution
refuse verification without printing credentials, source contents or absolute
paths. Fixed metadata probes refuse 32 nearer catalogs along inspected consumer project
ancestors, plus Projects' earlier nested Builds fallback, before reading any
unreviewed catalog.

The 66 tests separate controlled disposable safety fixtures from current-source
binding. Fixtures provide their own consistent sources, catalogs and retained
owner records; live checkout drift cannot mask their mutation assertions. They
restore every mutation in `finally`. Complete real-source drift/absence tests
remain a separate gate. Rebound-fingerprint mutations exercise effective-catalog structure, explicit
delegated declarations and lexical source anchors; all original
route/authority/secrets/execution/JSON/source-substitution safeguards remain.

Current observations:

- The proposal captures full root and dependency revisions as local snapshots.
  Byte bindings describe the inspected subset, not the full compiled-source
  closure or immutable release provenance. The receipt captures all twelve
  repository revisions and index digests separately. No dependency revision
  alone establishes an accepted catalog, release or deployment.
- Builds defaults select EventStore `3.127.0`, Tenants `5.10.1`, Memories `2.29.9`,
  Folders `1.3.1`, Projects `1.2.0` and FrontComposer `4.7.0`. The owning Folders
  catalog defines EventStore `3.127.0` before import. Projects currently has no
  Folders override; both Client/Contracts selections come from the same imported
  `HexalithFoldersVersion=1.3.1` property. These are local package input defaults.
  The retained released pair predates the current candidate.
- Both owning catalogs and their build props are bound. The offline parser
  accepts only their inspected import/property forms and checks ordering,
  unconditional owning overrides, unconditional catalog groups and exact shared
  Client/Contracts property references. It refuses an earlier nested Projects
  catalog, unsupported local items, conditional overrides, import substitution
  and package overrides. Bound build props also refuse relevant version/package
  overrides. All owning declarations precede imports, and the catalog permits
  only its inspected scalar-property/Include-Version forms with a true
  unconditional import sentinel. It does not evaluate arbitrary MSBuild or externally
  supplied properties; restored/loaded package identities remain separate gates.
- Projects has three direct SDK reads: lifecycle, permissions and file metadata
  (a read-only POST). Its SDK registration now installs
  `EventStoreGatewayTokenForwardingHandler` at `http://folders`. That handler
  forwards an inbound Bearer when outbound authorization is absent; it does not
  itself validate or downscope the credential. Its presence supplies no hosted
  authentication, audience, delegation or consumer-attribution qualification.
- `RefreshFolderReferenceAsync` uses `DelegatedProjectFolderDirectory` when
  `VerifiedQueryCaller` exists. `DelegatedOwnerQueryClient` sends the signed
  bound-owner POST to `api/v1/queries/owner-authority/workload`, retaining the
  originating query/assertion, for owner domain `folders`, owner `/query` and
  `Hexalith.Folders.Queries.AuthorizeFolderRead.v1`. Without that caller it uses
  direct SDK refresh; `ValidateSetProjectFolderAsync` stays direct. The owner
  handler rechecks fresh metadata authority. Allowed maps to Accepted, Stale to
  Stale, safe denial to Denied, and other/failed/malformed outcomes to Unavailable;
  caller cancellation propagates. Substring anchors are lexical declarations:
  comments alone can satisfy them. Reviewed exact source-byte bindings support
  the source observation; these anchors prove no C# control flow or runtime
  behavior. Hosted grants/proof keys remain unresolved.
  The consuming Projects Server configuration inventory names
  `Authentication:WorkloadIssuer:Workload` and
  `Authentication:BoundStreamRead:PrivateKey` (no source fallback), plus
  `Authentication:QueryAuthority:GatewayWorkloadId` (absent-value default
  `eventstore`). Environment and secret bindings remain null; no secret name or
  grant is invented.
- Folders registers operational/provider persistence and projection endpoints.
  Its gateway bearer handler now permits an authenticated direct public mutation,
  a content-authority marker with a stream-read/command-status target, or the
  content-retention marker, with a nonempty parsed Bearer. The packet records
  those actual source alternatives without claiming general or hosted delegation.
  Workload JWT fallback, command-bound receipts and enrollment gates remain.
- Platform records Stories 1.8/1.9 done, tool pin `4.35.2`, range
  `[4.35.2, 5.0.0-0)` and retained October 22 acceptance at Builds
  `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0`. Its current Builds gitlink
  `0ade5ec2b4f9b81fcd3a4bbb1672c11b0131a274` differs from direct Builds
  `142b06e6d05c94b55ac35e587450c35fcac87590`; their Git-normalized catalog
  bytes match. The accepted-tool catalog differs. The receipt retains each
  comparison; revision inequality alone is not called catalog drift. Recorded
  acceptance does not qualify this changed exact composed tuple. The verifier
  checks the dated receipt's actual successful CI/release, exact manifest and
  signed/restored package bindings and passing kit result. Expected real-module
  diagnostic rejections, changed runtime inputs and absent human SM-2 remain
  limitations; they are not converted into accepted module behavior. Historical
  Builds `4.32.0` publication remains a distinct unchanged receipt.

Jerome's October 18 conditional first-install decision is already accepted.
After the actual attempt lock, owners must establish target/consumer coverage
and retained-data disposition. Unknown disposition blocks execution. Retained
data needs a usable post-lock recovery point and compatibility evidence or named
approved recovery. Verified first installation rehearses `V2Only`, canonical
external v1 404s, direct/delegated v2 outcomes, Platform's existing five-minute
verification/observation and candidate workload removal with admission/execution
fencing, drained effects and retained data/shared infrastructure. This creates
no fictional v1 restore, migration T0, ordinary traffic or retirement evidence.

A discovered existing v1 installation uses the original migration: T0 is the
earlier routable-v2/first-production-request time; Projects deploys within 24
hours; observation includes 24 hours of ordinary attributed traffic and every
periodic call; retirement completes by T0+168 hours. Original reversal and expiry
rules remain. All staging checks are owned, finite and unexecuted; the packet
creates no additional hosted mechanism or production authorization.

The [pre-review receipt](../../_bmad-output/implementation-artifacts/evidence/story-1-17-current-source-qualification-2026-10-25.json)
and its original archive remain unchanged as dated implementation evidence. Its
assertion-based replay is retained historically; use the corrected final-review
replay below for integrity checking under optimized Python.

The [final-review receipt](../../_bmad-output/implementation-artifacts/evidence/story-1-17-current-source-qualification-final-review-2026-10-25.json)
binds the corrected packet, narrowly affected verification, parent review probes,
and all three compared Git catalog blobs (two distinct contents), including raw
Git-normalized hashes and the checkout normalization comparison. Extract its
bound archive to a disposable directory, then run:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 replay.py
PYTHONDONTWRITEBYTECODE=1 python3 -O replay.py
PYTHONDONTWRITEBYTECODE=1 python3 test_replay.py
```

Replay uses explicit integrity/outcome checks that remain active under `-O`.
The retained corruption test changes an evidence member without updating its
manifest and proves optimized replay refuses it. These commands establish only
retained-byte integrity and offline consistency. Historical receipts/approvals,
planning artifacts, pending candidate inventory, tracker and existing spec/
readiness/triage text remain untouched by the review patch. The successful dated
local topology observation retains its authentication/end-to-end limits; no new
startup, build or service test is claimed.

Remaining gates are the current candidate's exact release, immutable composed
and module artifacts, accepted actual tool/catalog/composition, module intake,
hosted identity/delegation and attribution, attempt-bound target/data disposition,
staging, observation/recovery, production latency and authorized closure/tracker
change. Packet consistency supplies none of these operational acceptances.
