from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

OUT = Path(__file__).resolve().parent
ROOT = Path('/home/administrator/projects/hexalith/folders')
EVIDENCE = OUT.parent
PRIOR = EVIDENCE / 'story-12-2-strict-continuation-20261022T100829Z'
CAPACITY = EVIDENCE / 'story-12-2-inherited-owner-requalification-20261022T084922Z'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
now = datetime.datetime.now(datetime.timezone.utc).isoformat()

freeze = json.loads((OUT / 'executed-freeze.json').read_text())
errors = []
for group, files in freeze.items():
    for name, expected in files.items():
        path = Path(name)
        actual = sha(path) if path.is_file() else None
        if actual != expected:
            errors.append(dict(group=group, path=name, expected=expected, actual=actual))

seals = []
for packet in (PRIOR, CAPACITY):
    mismatches = []
    lines = (packet / 'SHA256SUMS').read_text().splitlines()
    for line in lines:
        expected, name = line.split('  ', 1)
        actual = sha(packet / name) if (packet / name).is_file() else None
        if actual != expected:
            mismatches.append(dict(path=name, expected=expected, actual=actual))
    seals.append(dict(packet=packet.name, seal_sha256=sha(packet / 'SHA256SUMS'), files=len(lines), errors=mismatches))
historical = json.loads((PRIOR / 'historical-evidence-freeze.json').read_text())
historical_errors = [name for name, expected in historical.items()
                     if not Path(name).is_file() or sha(Path(name)) != expected]
audit = dict(utc=now, counts={group: len(files) for group, files in freeze.items()},
             errors=errors, prior_seals=seals, historical_files=len(historical), historical_errors=historical_errors,
             passed=not errors and not historical_errors and all(not seal['errors'] for seal in seals))
(OUT / 'final-preservation-audit.json').write_text(json.dumps(audit, indent=2) + '\n')
assert audit['passed'], audit

def test_result(packet, label):
    path = packet / (label + '.xml')
    assembly = ET.parse(path).getroot().find('assembly')
    return dict(label=label, packet=packet.name, xml_sha256=sha(path),
                **{field: assembly.attrib[field] for field in ('total', 'passed', 'failed', 'errors', 'skipped', 'time')})

current = [test_result(OUT, label) for label in ('strict-live', 'strict-selected-live', 'strict-observed-live', 'fixture-helpers')]
reused = [test_result(CAPACITY, label) for label in ('capacity-final-live', 'final-consumer-components', 'sdk-focused', 'sdk-full', 'sdk-domain-full')]
capacity_tests = [test.attrib['name'] for test in ET.parse(CAPACITY / 'final-consumer-components.xml').getroot().iter('test')
                  if any(size in test.attrib['name'] for size in ('10001', '32769'))]
observations = json.loads((OUT / 'strict-observed-observations.json').read_text())
assert observations['modules'] and all(row['matches'] for row in observations['modules'])
for sidecar in observations['sidecars']:
    resources = [sidecar['argv'][i + 1] for i, item in enumerate(sidecar['argv'][:-1]) if item == '--resources-path']
    for config in sidecar['config_files']:
        assert all(not Path(config['path']).is_relative_to(Path(resource)) for resource in resources)
log = (OUT / 'strict-observed-live.log').read_text()
performance = [dict(name=name, count=int(count), p50_ms=float(p50), p95_ms=float(p95), budget_p95_ms=float(budget), over_budget=over == 'True')
               for name, count, p50, p95, budget, over in re.findall(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([\d.]+) p95_ms=([\d.]+) budget_p95_ms=([\d.]+) over_budget=(True|False)', log)]
passes = [line.split('[OUTPUT] ', 1)[1] for line in log.splitlines() if '[OUTPUT] PASS ' in line]
runtime = dict(observations=len(observations['modules']), identity_errors=0, sidecars=len(observations['sidecars']),
               all_configs_outside_watched_resource_directories=True,
               configuration_triggered_sighup_reloads=log.count('triggering SIGHUP reload'),
               admission_unavailable_messages=log.count('idempotency_admission_unavailable'),
               production_runtime='Byte-identical to prior sealed selection; only the fixture test DLL/PDB were rebuilt.',
               current_external_sdk_source='Seven pre-existing source deltas remain preserved and are not compiled or qualified here.')
repositories = {}
for name, directory in [('folders', ROOT), ('eventstore', ROOT.parent / 'eventstore')]:
    repositories[name] = {key: subprocess.check_output(['git', *arguments], cwd=directory, text=True).strip()
                          for key, arguments in [('head', ['rev-parse', 'HEAD']), ('branch', ['branch', '--show-current']),
                                                 ('status', ['status', '--short']), ('remotes', ['remote', '-v']), ('recent_history', ['log', '-3', '--format=%H %s'])]}
(OUT / 'repositories.json').write_text(json.dumps(repositories, indent=2) + '\n')
result = dict(created_utc=now, task36_selected_candidate_consumer_qualified=True, whole_story_complete=False,
              current_results=current, explicitly_reused_results=reused, required_component_sizes=capacity_tests,
              sdk_reused_process_evidence=dict(packet=CAPACITY.name, path='sdk-reused-process-evidence.json', sha256=sha(CAPACITY / 'sdk-reused-process-evidence.json')),
              folders_http_32769_executed=False, runtime=runtime, performance=performance, strict_pass_markers=passes,
              task36_assessment='The byte-bound prior production-handler component sizes, authenticated 10001 recovery/inheritance lane, and explicitly reused SDK process evidence combine with this unchanged strict authenticated real-provider pass. Only Task 36 selected-candidate consumer qualification is complete; no current-external-SDK-source, package/release, performance or whole-story acceptance is inferred.',
              remaining=['Current external SDK graph and separately owned SDK broad-regression disposition', 'Release/package and PD10 qualification', 'SM4 acknowledgement latency', 'Independent whole-story review and acceptance'],
              preservation_audit='final-preservation-audit.json', cleanup='cleanup-final.json')
(OUT / 'qualification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(dict(current=current, runtime=runtime, preservation_passed=audit['passed'], performance=performance), indent=2))
