"""Explain exact portable packet scope and gate outcomes from closed receipts."""
from pathlib import Path
import json,sys
W=Path(__file__).parent
P=Path(sys.argv[1]).resolve()
q=json.loads((W/'qualification-summary.json').read_text())
rows=[]
for phase in q['phase_results']:
 assemblies=phase['assemblies']
 if not assemblies:continue
 rows.append('| '+phase['phase']+' | '+str(sum(int(a['passed']) for a in assemblies))+' | '+str(sum(int(a['failed']) for a in assemblies))+' | '+str(sum(int(a['skipped']) for a in assemblies))+' | '+', '.join(a['time'] for a in assemblies)+' |')
strict='The revised strict class passes on the exact recorded producer.' if q['revised_strict_passed'] else 'The revised strict class has no completed passing run in this packet.'
markers=q['strict_observations']
probes=any(any(line.startswith('PASS real-provider task pipeline:') for line in values) for values in markers.values())
probe_text='Both approved probes passed in the live task-pipeline milestone: real independently authorized different folder404 without original-folder disclosure, and unsupported never-created source503/read_model_unavailable.' if probes else 'Neither approved probe has a completed passing live milestone in these attempts; focused controls do not substitute for that requirement.'
performance=[label+': '+line for label,values in markers.items() for line in values if line.startswith('PERFORMANCE ')]
perf='\n'.join('- '+line for line in performance) if performance else 'No complete current strict performance summary is available. SM4 is unqualified.'
package=q['package_gate_diagnosis']
package_text='The fresh Release/package gate failed: published SDK4.0.4 lacks AddEventStoreGatewayNativeSourceMetadataReader, producing CS1061 in both Folders Server and Workers. Debug/project-reference assets were restored and re-audited afterward. No package pin, publication or release operation was performed.' if package and package['exit_code'] else 'See the actual package gate command receipt and closed diagnosis; no source-lane result substitutes for package qualification.'
text=f'''# Story12.2 approved foreign task scope qualification — 2026-11-04

{strict} Whole-story acceptance remains open. This packet records closed step03 qualification;
independent review advances separately in the spec. {probe_text}

The owner replied **approve** to the exact prepared correction. The original strict SHA256
`f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90` becomes
`d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82`, exactly matching the approved patch
SHA256 `c832f58f6c1b05516c1b62f97e2bc8a820d6ed2b4a51f6006952db28a16d7856`.
Missing hint503 and all other assertions remain. Companion/capacity bodies, frozen intent, proof,
authorization, source semantics and all timing/retry limits are preserved.

The separate fixture compatibility correction uses the current supported human-command shape:
canonical body tenant and unchanged bearer, without a redundant tenant header. Status GETs retain
their independently required explicit header; platform namespaces and native source proofs stay intact.
It changes no production code. Exact phase patches and141fixture/97unchanged SDK owner controls
are retained. The original focused103Domain controls and18pending-inventory controls are also retained;
these selections overlap broad results and are not additional broad passes.

The fixture lifecycle observer adds only bounded opaque Activity/framework IDs. It does not read
notification values, request/context objects, routes/URLs/query, headers/bodies, tags/baggage/trace-state,
or enable Activity creation. The early framework event occurs before Activity creation; its failed
18/19control is retained. The later fixed-name hook passes real Kestrel existing-Activity and disabled-
Activity controls, then28focused lifecycle/privacy controls and18inventory controls;274unchanged source
secret-scanner controls also pass. All host/shutdown and strict limits remain unchanged.

| Closed phase | Pass | Fail | Skip | Seconds |
| --- | ---: | ---: | ---: | ---: |
{chr(10).join(rows)}

The initial seven Folders consumer selections total5630passes and2configured optional Domain skips;
any later affected requalification is a separate phase in the table.
All five frozen matrix rows have executed passing component coverage; required10001/32769 histories
use the real Folders handler and production SDK dispatcher, with unchanged100-operation/one-MiB limits.
They are component evidence, not a new HTTP capacity claim. Existing skipped optional lanes remain
separately unqualified. The SDK broad outcome is listed at its exact receipt; no skipped case is used
to satisfy a required matrix row.

Every failed strict attempt remains independent: strict1scheduler reachability stopped before
bootstrap; strict2passed startup/genuine bootstrap and exposed the human-command header mismatch;
strict3Sentry reachability stopped before bootstrap. Actual targets match the model, but a permanent
startup source/configuration defect was not established. Strict4passes the provider/concurrency/
rebuild milestones then fails the actual DCP EventStore process-stop deadline before revised probes.
Strict5completes the full class. This does not retrospectively identify or repair strict4's blocker.
Failed listener inspections are explicitly
inconclusive. Further attempts retain their actual result and runtime/cleanup receipts without
relabeling earlier failures. No startup/probe deadline was extended or bypassed.

{package_text} The exact command is
`pwsh -NoLogo -NoProfile -File tests/tools/run-contract-spine-gates.ps1`.
The dated PD10 manifest is unchanged; only the pending source inventory reflects the approved test
input changes. Publicv2 remains disabled. Independent review and whole-story completion are not claimed.

Current strict performance observations, against the original budgets:

{perf}

Source/head and runtime-file drift are zero in the closed command receipts. The recorded module audits
bind actual selected DLL/PDB/source checksums and project-only selected Hexalith dependency edges.
Saved actual SDK process mappings and fixture bindings match the audited producer. All owned process,
container and Dapr-directory remainders are zero; unrelated resources are never stopped or changed.
All three prior sealed packet verifiers pass and their exact seals remain unchanged.

Observer candidate DLL/PDB/source are qualified in the expanded98-module audit. Actual new opaque
behavior and its service type/assembly simple name appear on four EventStore PIDs, with2987exact
Activity pairs and703explicit observation_busy records. Actual loaded Tool location/MVID/SHA are
not separately captured; missing poll mappings are a visibility limit, not a known binary mismatch.
The [identity supplement](audit/strict-foreign-scope-5-observer-identity-limit-supplement-private.json)
keeps those facts separate. Successful stops supply no causal shutdown-fix claim.

Start with [qualification summary](audit/qualification-summary.json),
[matrix audit](audit/matrix-test-audit.json), and
[actual runtime binding audit](audit/actual-sdk-runtime-binding-audit.json).
Commands, source trees, focused handoffs and closed diagnoses retain exact hashes/provenance.
Raw logs/XML and parameter display payloads are excluded; only static test identifiers, outcomes,
timings and display-name hashes are exported. Recognizable JWT/private-key scans must report0findings.

Verify every sealed byte and these gate dispositions with `python3 verify_scope.py` from this directory.
'''
(P/'README.md').write_text(text)
