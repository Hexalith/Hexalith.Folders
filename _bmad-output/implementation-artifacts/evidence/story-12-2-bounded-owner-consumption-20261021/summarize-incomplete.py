from pathlib import Path
import datetime
import hashlib
import json
import re
import xml.etree.ElementTree as ET

out = Path(__file__).parent
load = lambda name: json.loads((out / name).read_text())
freeze = load('source-freeze.json')
components = {}
for name, path in [('domain', 'final-domain-build7.xml'), ('server', 'components-final/final-server.xml'),
        ('workers', 'components-final/final-workers.xml'), ('apphost-helpers', 'final-apphost-helpers-build7.xml'),
        ('docs', 'components-final/final-docs.xml')]:
    assembly = next(ET.parse(out / path).iter('assembly'))
    components[name] = {key: int(assembly.get(key)) for key in ('total', 'passed', 'failed', 'skipped')}
    components[name]['xml'] = path
    assert components[name]['failed'] == 0
integration = (out / 'components-final/integration.log').read_text()
match = re.search(r'total:\s*(\d+).*?failed:\s*(\d+).*?succeeded:\s*(\d+).*?skipped:\s*(\d+)', integration, re.S)
assert match
components['integration'] = dict(zip(('total', 'failed', 'passed', 'skipped'), map(int, match.groups())))
components['integration']['log'] = 'components-final/integration.log'
assert sum(x['passed'] for x in components.values()) == 4249
assert sum(x['failed'] for x in components.values()) == 0
assert sum(x['skipped'] for x in components.values()) == 2
capacity = [t.attrib for t in ET.parse(out / 'final-domain-build7.xml').iter('test')
    if 'CompleteOriginalOwnersRebuildGrowAndPreserveHistoricalTasksWithBoundedValues' in t.get('name', '')]
assert len(capacity) == 2 and all(t['result'] == 'Pass' for t in capacity)
continuity = load('component-binary-continuity-build7.json')
assert all(not row['changes'] for row in continuity)

live = []
for lane, attempts in [('capacity', range(1, 6)), ('real-provider', range(1, 4))]:
    for attempt in attempts:
        path = f'{lane}-live-attempt-{attempt}-result.json'
        row = load(path)
        live.append(dict(lane=lane, attempt=attempt, result=path, exit_code=row['exit_code'],
            seconds=row['seconds'], assemblies=row.get('assemblies', [])))
assert not any(int(a['passed']) for run in live for a in run['assemblies'])
sdk = load('sdk-reused-evidence.json')
assert sdk['required_capability_passed'] == sdk['required_capability_cases'] == 74
assert sdk['sdk_broad_regression_qualified'] is False
drift = load('after-execution-audit.json')
assert not drift['assemblies_sha256']
binding = load('parent-source-binding-build7/summary.json')
observed_heads = load('head-observation-at-handoff.json')
assert binding['passed'] and not binding['errors']
for path in ['parent-preservation-external-recovery-strict3.json',
        'parent-preservation-historical.json', 'parent-preservation-archived-source-filtering.json',
        'parent-runtime-binding-audit.json']:
    assert load(path)['passed'], path

report = dict(schema='hexalith-folders-task36-frozen-consumer/v1',
    recorded_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    status='Implementation and component qualification retained; authenticated live qualification incomplete.',
    task36_source_consumption_qualified=False, current_checkout_qualified=False, whole_story_accepted=False,
    frozen_graph=dict(build=7, configuration='Debug; UseHexalithProjectReferences=true; UseNuGetDeps=false',
        source_config_inputs=len(freeze['files_sha256']), pe_pdb_files=len(freeze['assemblies_sha256']),
        root_head=freeze['root_head'], sdk_head=freeze['submodules']['references/Hexalith.EventStore']['head'],
        inventory='source-freeze.json', exact_pe_pdb_and_harness_bytes='final-build7-byte-archive.json'),
    components=dict(passed=4249, failed=0, unchanged_optional_domain_skips=2, lanes=components,
        command_receipts=['components-final/components.json', 'component-build7-results.json'],
        unchanged_other_component_outputs='component-binary-continuity-build7.json'),
    large_history_components=capacity,
    coverage_map=[
        dict(property='Complete original owners at10001/32769,98 historical tasks,growth/repeated rebuild,inherited generations,original identity conflicts',
            evidence='final-domain-build7.xml', boundary='Actual Folders dispatcher/handler and supported bounded protocol DTOs with in-memory conditional store; no overlapping CAS race claimed from Task.WhenAll alone.'),
        dict(property='Buffered large-owner Dapr,controlled concurrency,true mid-stage and partial multi-owner handoff',
            evidence='sdk-reused-evidence.json', boundary='Previously executed SDK coverage, explicitly reused; not fresh Folders execution.'),
        dict(property='Authenticated Folders HTTP large-history staging/process replacement/Abort',
            evidence='capacity-attempt-5-diagnosis.json', boundary='Failed complete case:10001 Stage499.334s; changed main managed-host PIDs; wrong phase replay rejected; Abort496.642s Completed. No large HTTP pass or32769 HTTP execution claimed.'),
        dict(property='Corrected phase resumption,real retained source,six-concern rebuild,retention,ordinary-write substeps',
            evidence='real-provider-attempt-2-diagnosis.json', boundary='Partial failed strict run; later canonical eventstore stop timed out. Proof-a80102/proof-b80063 are DCP launcher IDs; actual proof managed-host identities were not obtained.'),
    ],
    live=dict(complete_case_passes=0, attempts=live, blocked_by='Dapr component watcher ENOSPC at near-exhausted shared inotify quota.',
        observation='live-continuation-decision.json', cleanup='all-attempts-cleanup.json',
        remaining=['One complete strict real-provider pass with unchanged assertions/deadlines/retry limits.',
            'One complete added10001 synthetic HTTP test on the frozen candidate; no32769 Folders HTTP claim is required.']),
    source_binding=dict(initial_parent_audit='parent-source-binding-build7/summary.json',
        portable_pdbs=binding['pdbCount'], ordinary_document_copies=binding['ordinaryDocumentCopies'],
        generated_document_copies=binding['generatedDocumentCopies'], archived_generated_sources=binding['uniqueArchivedGeneratedSources'],
        generated_metadata_disk_copy_differences=len(binding['currentGeneratedDiskDifferences']),
        post_external_drift='after-execution-audit.json', external_input_paths=len(drift['external_source_paths']),
        authored_documentation_paths=drift['authored_post_freeze_documentation_paths'],
        current_binary_differences=0,
        recovered_originals=['parent-preservation-external-recovery-strict3.json', 'parent-external-drift-final-additional-recovery.json'],
        runtime_identity_audit='parent-runtime-binding-audit.json',
        note='Frozen original bytes preserve executed-source binding; current source checksum mismatch is real and current checkout is not qualified.'),
    external_git_mutation=dict(evidence='parent-external-git-state.json', latest_head_observation='head-observation-at-handoff.json',
        observed_at_utc=observed_heads['observed_at_utc'], observed_root_head=observed_heads['root_head'],
        observed_sdk_head=observed_heads['sdk_head'],
        note='Concurrent external pushall changed root/SDK HEAD/index and dependency gitlinks; this implementation agent issued no staging, commit, push, branch or dependency-update operations. Do not infer unchanged global Git state.'),
    sdk_reuse=dict(evidence='sdk-reused-evidence.json', mandatory_cases=74, sdk_broad_gate_qualified=False,
        original_prebuild_audit='sdk-prebuild-audit.json', pre_external_after_build_audit='sdk-after-build7-audit.json',
        current_sdk_checkout='Substantively changed externally after build7; not qualified by original-candidate evidence.'),
    bounds=dict(recoverable_batch_operations=100, canonical_manifest_bytes=1048576,
        qualification_event_admission=40000, transport_page_events=256, capacity_phase_budget_minutes=15,
        note='SDK instrumented run-wide bounds are reused separately. Redis observations are point-in-time hash-value/key counts, not an instrumented observer of all Folders physical writes or canonical batches.'),
    preservation=dict(historical='parent-preservation-historical.json', historical_files=25017,
        archived_source_filtering='parent-preservation-archived-source-filtering.json', attributes='.gitattributes',
        parent_verification='parent-task36-verification.json', final_source_diff='parent-task36-final-diff.json',
        exact_source_patch='parent-task36-source.patch'),
    open_gates=['Task36 authenticated live qualification', 'Current integrated source after external SDK/dependency changes',
        'SDK broad regression gate', 'Release/package/PD10', 'SM4 performance acceptance retaining historical observations',
        'Independent scoped and whole-story review'])
(out / 'component-summary.json').write_text(json.dumps(components, indent=2) + '\n')
(out / 'qualification.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(dict(task36_source_consumption_qualified=False, current_checkout_qualified=False,
    frozen_component_passes=4249, complete_live_case_passes=0, external_source_differences=len(drift['external_source_paths']))))
