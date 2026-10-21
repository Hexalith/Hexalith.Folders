from pathlib import Path
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET

root = Path('/home/administrator/projects/hexalith/folders')
out = Path(__file__).parent
capacity_attempt = sys.argv[1] if len(sys.argv) > 1 else '6'
provider_attempt = sys.argv[2] if len(sys.argv) > 2 else '1'
freeze = json.loads((out / 'source-freeze.json').read_text())

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def load_live(lane, attempt):
    stem = out / (lane + '-live-attempt-' + attempt)
    result = json.loads(Path(str(stem) + '-result.json').read_text())
    assert result['exit_code'] == 0, result
    tree = ET.parse(str(stem) + '.xml').getroot()
    for assembly in tree.iter('assembly'):
        assert assembly.get('failed') == '0' and assembly.get('skipped') == '0', assembly.attrib
    return result, list(tree.iter('test'))

capacity, tests = load_live('capacity', capacity_attempt)
assert len(tests) == 1
cases = []
runtime = []
for test in tests:
    assert test.get('result') == 'Pass', test.attrib
    count = int(re.search(r'count: (\d+)', test.get('name')).group(1))
    assert count == 10001
    output = test.findtext('output') or ''
    assert f'PASS CAPACITY count={count} grown={count + 1}' in output
    hashes = re.findall(r'sourceSha256=([A-F0-9]{64}) otherSha256=([A-F0-9]{64})', output)
    assert len(hashes) >= 2 and len(set(hashes)) == 1
    phases = []
    rows = []
    for line in output.splitlines():
        if 'RUNTIME_BINDING ' in line:
            row, _ = json.JSONDecoder().raw_decode(line.split('RUNTIME_BINDING ', 1)[1])
            for key, hashkey in (('path', 'sha256'), ('pdbPath', 'pdbSha256')):
                if row.get(key):
                    relative = str(Path(row[key]).relative_to(root))
                    assert freeze['assemblies_sha256'][relative] == row[hashkey].lower(), relative
            rows.append(row)
        match = re.search(r'CAPACITY phase=(\w+) ownerOrdinal=([^ ]*) part=([^ ]*) events=(\d+) elapsedMs=([0-9.]+) status=(\w+)', line)
        if match:
            phase, owner, part, events, elapsed, status = match.groups()
            assert status in ('Completed', 'AlreadyCompleted')
            if phase == 'AccumulatePart':
                assert 0 < int(events) <= 256
            assert float(elapsed) < 900_000
            sdk_phase = re.search(r'sdkPhase=(\w+)', line)
            phases.append(dict(action=phase, owner_ordinal=owner, part=part, events=int(events), elapsed_ms=float(elapsed), status=status,
                durable_sdk_phase=sdk_phase.group(1) if sdk_phase else None))
    assert phases and rows
    for required in ('Begin', 'AccumulatePart', 'AccumulateComplete', 'Finalize', 'Stage', 'Commit', 'Verify'):
        assert any(x['action'] == required for x in phases), required
    resumed = [i for i, p in enumerate(phases) if p['action'] == 'Begin' and p['durable_sdk_phase'] == 'Prepared']
    assert resumed and all(phases[i + 1]['action'] == 'Commit' for i in resumed)
    processes = {}
    for resource in ('folders', 'folders-workers', 'folders-workers-replica', 'eventstore'):
        relevant = [r for r in rows if r['resource'] == resource]
        launchers = sorted(set(r['launcherProcessId'] for r in relevant))
        managed = sorted(set(r['processId'] for r in relevant))
        assert len(launchers) >= 2 and len(managed) >= 2, resource
        processes[resource] = dict(launcher_pids=launchers, observed_managed_host_pids=managed)
    bounds = re.search(r'retainedValues=(\d+) finalRetainedSharedValueMaxBytes=(\d+) identityMaxBytes=(\d+) sdkCheckpointMaxBytes=(\d+)', output)
    assert bounds
    cases.append(dict(count=count, grown_count=count + 1, input_kind='synthetic original metadata owner histories; separate ordinary primer establishes epoch',
        source_sha256=hashes[0][0], other_source_sha256=hashes[0][1], test=test.attrib, phases=phases,
        process_replacement=processes, final_retained_shared_hash_value_scan=dict(zip(('values', 'maximum_data_bytes', 'identity_maximum_data_bytes', 'sdk_checkpoint_maximum_data_bytes'), map(int, bounds.groups())))))
    runtime.extend(rows)
assert {x['count'] for x in cases} == {10001}
component_capacity = [t.attrib for t in ET.parse(out / 'final-domain-build7.xml').iter('test')
    if 'CompleteOriginalOwnersRebuildGrowAndPreserveHistoricalTasksWithBoundedValues' in t.get('name', '')]
assert len(component_capacity) == 2 and all(t['result'] == 'Pass' for t in component_capacity)
assert {int(re.search(r'count: (\d+)', t['name']).group(1)) for t in component_capacity} == {10001, 32769}

provider, provider_tests = load_live('real-provider', provider_attempt)
assert len(provider_tests) == 1 and provider_tests[0].get('result') == 'Pass'
provider_output = provider_tests[0].findtext('output') or ''
performance = []
for operation, count, p50, p95, budget, over in re.findall(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([\d.]+) p95_ms=([\d.]+) budget_p95_ms=([\d.]+) over_budget=(True|False)', provider_output):
    performance.append(dict(operation=operation, count=int(count), p50_ms=float(p50), p95_ms=float(p95), budget_p95_ms=float(budget), over_budget=over == 'True'))
assert len(performance) == 3

continuity = json.loads((out / 'component-binary-continuity-build7.json').read_text())
assert all(not x['changes'] for x in continuity)
audit = json.loads((out / 'after-execution-audit.json').read_text())
assert not any(audit.values()), audit
sdk = json.loads((out / 'sdk-reused-evidence.json').read_text())
assert sdk['required_capability_cases'] == sdk['required_capability_passed'] == 74
assert sdk['sdk_broad_regression_qualified'] is False
for path, expected in sdk['artifact_sha256'].items():
    assert sha(root / path) == expected, path
binding_summary = json.loads((out / 'parent-source-binding-build7/summary.json').read_text())
assert binding_summary['passed'] and not binding_summary['errors']

report = dict(task36_source_consumption_qualified=True, whole_story_accepted=False,
    source_graph='Debug project references; UseHexalithProjectReferences=true; UseNuGetDeps=false',
    source_inputs=len(freeze['files_sha256']), pe_pdb_files=len(freeze['assemblies_sha256']),
    current_consumer_components=dict(passed=4249, failed=0, unchanged_optional_domain_skips=2,
        evidence='components-final/components.json; component-build7-results.json; component-binary-continuity-build7.json'),
    large_history_components=component_capacity, synthetic_authenticated_http=cases,
    authenticated_real_provider=provider, latest_performance_observations=performance,
    sdk_reuse=dict(evidence='sdk-reused-evidence.json', mandatory_cases=74, newly_executed_folders_cases=False, broad_regression_qualified=False),
    binding=dict(ordinary_source_audit='pdb-audit.json', pe_pdb_identity_audit='pe-pdb-identities.json',
        supplemental_parent_audit='parent-source-binding-build7/summary.json',
        exact_executed_pe_pdb_and_harness_bytes='final-build7-byte-archive.json',
        generated_metadata_variants=dict(current_disk_copy_differences=len(binding_summary['currentGeneratedDiskDifferences']),
            unique_source_files=len({x['source'] for x in binding_summary['currentGeneratedDiskDifferences']}),
            archived_generated_sources=binding_summary['uniqueArchivedGeneratedSources'],
            note='Exact embedded/current bytes and earlier build variants retained in parent binding packets; ordinary source/PE binding passes.')),
    retained_failures=['capacity attempt1: Dapr watcher ENOSPC before fixture readiness',
        'capacity attempt2: shared Begin without prior operational epoch; explicitly cancelled exact test invocation',
        'capacity attempt3: observer initially inspected launcher PID; corrected to owned managed descendants',
        'capacity attempt4: immediate post-start observer raced managed readiness; later tiny primer denied with an expired workload assertion; no passing32769 HTTP claim',
        'capacity attempt5: helper replayed transport parts after the SDK Prepared phase; SDK failed closed, then authenticated Abort Completed496.642s. Corrected helper resumes from durable phase with unchanged capture validation.'],
    limits=dict(recoverable_batch_operations=100, recoverable_canonical_manifest_bytes=1048576,
        qualification_event_admission=40000, transport_page_events=256, capacity_phase_budget_minutes=15),
    limitations=['Large HTTP histories are synthetic; the real-provider scenario separately proves retained actor sources.',
        'Folders process replacement occurs after sealed staging; true mid-stage and partial-owner handoff use explicitly reused SDK process evidence.',
        'The Redis scan measures final retained shared-key hash data values, not every physical write or canonical batch. SDK instrumented maxima remain separate.',
        'The capacity tenant is primed through authenticated ordinary delivery; shared Begin on an absent epoch is not qualified.',
        'Both sizes run the actual Folders dispatcher/handler through supported bounded protocol DTOs in component tests. Only10001 additionally runs Folders authenticated HTTP;32769 Dapr/process properties reuse exact SDK evidence.',
        'Runtime observations bind actual descendant process mappings to PE hashes/MVIDs and PDB files; source binding is checked independently.'],
    separate_open_gates=['SDK full regression gate remains failed', 'Release/package/PD10 requalification', 'SM4 performance acceptance, retaining historical over-budget observations', 'Independent approved-scope and whole-story review'],
    scope='No SDK/product source edit, dependency pin, retained-event replacement, public v2, Git index/commit, publication or deployment mutation.')
(out / 'qualification.json').write_text(json.dumps(report, indent=2) + '\n')
(out / 'runtime-bindings.json').write_text(json.dumps(runtime, indent=2) + '\n')
print(json.dumps(dict(task36_source_consumption_qualified=True, component_passes=4249, large_handler_protocol_cases=2, synthetic_http_cases=1, real_provider_cases=1, runtime_binding_rows=len(runtime))))
