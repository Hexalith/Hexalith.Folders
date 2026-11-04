"""Verify sealed bytes and the explicitly partial Story12.2 continuation."""
from pathlib import Path
import hashlib,json
P=Path(__file__).parent
seal=json.loads((P/'seal.json').read_text())
actual={p.relative_to(P).as_posix() for p in P.rglob('*') if p.is_file() and p.name!='seal.json'}
assert actual==set(seal['files']),'Sealed inventory differs'
for name,digest in seal['files'].items():
 p=P/name;assert not p.is_symlink() and hashlib.sha256(p.read_bytes()).hexdigest()==digest,name
for p in (P/'source').glob('*.json'):
 d=json.loads(p.read_text())
 for repo in d.get('repos',[]):
  item=repo['source_tree'];tree=json.loads((P/item['path']).read_text())
  assert len(tree)==item['file_count']
  assert hashlib.sha256(json.dumps(tree,sort_keys=True,separators=(',',':')).encode()).hexdigest()==item['canonical_sha256']
for p in (P/'commands').glob('*-receipt.json'):
 d=json.loads(p.read_text());assert not d['runtime_file_drift'],p.name
 assert not d['cleanup']['remaining_observed_descendants'],p.name
 assert not d['cleanup']['remaining_owned_containers'],p.name
 for a in (d.get('summary') or {}).get('assemblies',[]):
  assert int(a['passed'])+int(a['failed'])+int(a['skipped'])==int(a['total']),(p.name,a)
 for t in (d.get('summary') or {}).get('tests',[]):
  assert 'name' not in t and len(t['display_name_sha256'])==64 and t['case_ordinal']>0
for p in (P/'audit').glob('*-owned-cleanup-audit.json'):
 d=json.loads(p.read_text());assert not d['runtimeOwnedProcessRemainders'] and not d['ownedContainerRemainders']
 assert all(not row['exists'] for row in d['ownedDaprDirectories'])
 assert d['actionOnUnattributedResources']=='None'
q=json.loads((P/'audit/qualification-summary.json').read_text())
assert q['status']=='in-progress' and q['workflow_step']=='step-03-implement'
assert q['whole_story_complete'] is False and q['full_acceptance'] is False
m=q['approved_fixture_maintenance'];assert m['standalone_live_qualified'] and m['full_strict_replacement'] is False
assert m['exact_disposable_administrator_permission']=='tenants:update'
assert (m['foreground_native_source_age_trigger_seconds'],m['whole_operation_budget_seconds'],m['production_freshness_seconds'],m['mutation_evidence_budget_seconds'])==(120,90,300,2)
assert m['live_control']['exit_code']==0 and m['live_control']['assemblies'][0]['passed']=='1'
assert not m['live_control']['runtime_file_drift']
for row in q['source_transition_assessments']:
 if row['phase']==m['live_control']['phase']:assert all(not c.get('changed_sources') for c in row['changes'])
assert len(q['strict_attempts'])>=3
original={s['phase']:s for s in q['strict_attempts']}
assert all(original['strict-tenant-maintenance-'+str(i)]['exit_code']==1 for i in [1,2,3])
a=q['pre_live_runtime_audit'];assert not a['source_or_pdb_issues'] and not a['sdk_variant_failures'] and not a['asset_failures']
assert a['frozen_unchanged'] and all(a['strict_bodies_unchanged'].values())
abi=q['sdk_failure_rechecks']['public_package'];assert abi['exit_code']==0 and abi['assemblies'][0]['passed']=='3'
o=q['sdk_failure_rechecks']['public_package_origin'];assert o['official_archive_matches_pinned_expected'] and o['signed_entry_present']
assert o['archive_sha256']==o['pinned_expected_sha256']=='e21f5ac7b5f332f1921f70a249ab2032b9978d56ffdd6641a0a4b94d04850bdc'
assert o['previous_restored_archive_sha256']!=o['archive_sha256']
assert o['shared_package_cache_or_expected_hash_mutated'] is False
assert q['sdk_failure_rechecks']['whole_sdk_broad_green_claimed'] is False
assert q['evidence_privacy']['raw_logs_exported'] is False and q['evidence_privacy']['raw_xml_exported'] is False
assert len(q['consumer_phase_selections'])==7
assert all(s['exit_code']==0 for s in q['consumer_phase_selections'] if s['phase']!='admission-server-tests')
failed_consumer=[s for s in q['consumer_phase_selections'] if s['exit_code']!=0]
assert len(failed_consumer)==1 and failed_consumer[0]['phase']=='admission-server-tests'
assert failed_consumer[0]['failures']==[{'type':'Hexalith.Folders.Server.Tests.ParallelPermissionObservationTests','method':'CallerOrCatchupCancellationDrainsBothSourcesAndAcceptsNoLateAuthority'}]
assert q['test_hygiene_handoff'] is not None
assert q['strict_body_decision']['status']=='pending' and q['strict_body_decision']['patch_applied'] is False
hygiene=[]
for file in (P/'focused/broad-hygiene').glob('*.json'):
 doc=json.loads(file.read_text())
 if 'assemblies' in doc and doc['assemblies'] and all(int(a.get('failed',0))==0 and int(a.get('errors',0))==0 for a in doc['assemblies']):hygiene.extend(doc.get('tests',[]))
for cls in ['Hexalith.EventStore.Admin.Server.Tests.Services.DaprStreamQueryServiceTests','Hexalith.Folders.Server.Tests.ParallelPermissionObservationTests','Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests']:
 cases=[t for t in hygiene if t.get('type')==cls];assert cases and all(t['result']=='Pass' for t in cases),cls

print(json.dumps({'sealed_files':len(actual),'standalone_real_maintenance_qualified':True,'public_abi_focused_passes':3,'retained_failed_strict_attempts':sum(s['exit_code']!=0 for s in q['strict_attempts']),'whole_story_complete':False}))
