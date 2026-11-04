"""Verify sealed portable metadata, actual outcomes and exact approved body transition."""
from pathlib import Path
import hashlib,json,re

P=Path(__file__).parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
seal=json.loads((P/'seal.json').read_text())
actual={p.relative_to(P).as_posix() for p in P.rglob('*') if p.is_file() and p.name!='seal.json'}
assert actual==set(seal['files']),'Sealed inventory differs'
for name,digest in seal['files'].items():
 p=P/name;assert not p.is_symlink() and sha(p)==digest,name
for p in (P/'source').glob('*.json'):
 for repo in json.loads(p.read_text()).get('repos',[]):
  item=repo['source_tree'];tree=json.loads((P/item['path']).read_text())
  assert len(tree)==item['file_count']
  assert hashlib.sha256(json.dumps(tree,sort_keys=True,separators=(',',':')).encode()).hexdigest()==item['canonical_sha256']
receipts={}
for p in (P/'commands').glob('*-receipt.json'):
 d=json.loads(p.read_text());receipts[p.name[:-13]]=d
 assert not d['runtime_file_drift'],p.name
 assert not d['source_or_head_drift'],p.name
 assert not d['cleanup']['remaining_observed_descendants'],p.name
 assert not d['cleanup']['remaining_owned_containers'],p.name
 for a in (d.get('summary') or {}).get('assemblies',[]):
  assert int(a['passed'])+int(a['failed'])+int(a['skipped'])==int(a['total']),(p.name,a)
 for t in (d.get('summary') or {}).get('tests',[]):
  assert 'name' not in t and len(t['display_name_sha256'])==64 and t['case_ordinal']>0
for p in (P/'audit').glob('*-owned-cleanup-audit.json'):
 d=json.loads(p.read_text())
 assert not d['runtimeOwnedProcessRemainders'] and not d['ownedContainerRemainders']
 assert all(not r['exists'] for r in d['ownedDaprDirectories'])
 assert d['actionOnUnattributedResources']=='None'
q=json.loads((P/'audit/qualification-summary.json').read_text())
assert q['status']=='in-progress' and q['workflow_step']=='step-03-implement'
assert q['whole_story_complete'] is False
assert q['full_acceptance'] is False
assert q['approval']['owner_reply']=='approve'
assert q['approval']['original_strict_sha256']=='f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90'
assert q['approval']['approved_strict_sha256']=='d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82'
assert q['approval']['exact_patch_sha256']=='c832f58f6c1b05516c1b62f97e2bc8a820d6ed2b4a51f6006952db28a16d7856'
assert sha(P/'decisions/proposal-12-2-foreign-task-scope-proof.patch')==q['approval']['exact_patch_sha256']
spec=(P/'decisions/spec-12-2-durable-projections-and-task-completion-pipeline.md').read_text()
frozen=re.search(r'<frozen-after-approval\b.*?</frozen-after-approval>',spec,re.S).group()
assert hashlib.sha256(frozen.encode()).hexdigest()=='497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422'
for audit in q['runtime_audits']:
 d=json.loads((P/'audit'/audit).read_text())
 assert not d['source_or_pdb_issues'] and not d['sdk_variant_failures'] and not d['asset_failures']
 assert d['frozen_unchanged'] and all(d['bodies_match_approved_expectations'].values())
 assert d['approved_strict_body_transition']['actual_sha256']==q['approval']['approved_strict_sha256']
 assert all(d['companion_bodies_unchanged'].values())
m=json.loads((P/'audit/matrix-test-audit.json').read_text())
assert m['all_component_rows_passed'] and len(m['matrix_rows'])==5
assert [r['count'] for r in m['required_capacity_components']]==[10001,32769]
assert all(r['result']=='Pass' for r in m['required_capacity_components'])
strict=[d for name,d in receipts.items() if name.startswith('strict-foreign-scope-')]
assert len(strict)>=2 and receipts['strict-foreign-scope-1']['exit_code']==1 and receipts['strict-foreign-scope-2']['exit_code']==1
assert q['revised_strict_passed']==any(d['exit_code']==0 for d in strict)
consumer_labels=['scope-domain-broad','scope-server-broad','scope-workers-broad','scope-integration-broad','scope-gateway-broad','scope-contracts-broad','scope-client-generation']
consumer_assemblies=[]
for label in consumer_labels:
 d=receipts[label];assert d['exit_code']==0,label
 consumer_assemblies.extend(d['summary']['assemblies'])
assert sum(int(a['passed']) for a in consumer_assemblies)==5630
assert sum(int(a['skipped']) for a in consumer_assemblies)==2
assert all(a['failed']=='0' and a['errors']=='0' for a in consumer_assemblies)
sdk=receipts['scope-sdk-server-broad'];assert sdk['exit_code']==0
assert sdk['summary']['assemblies'][0]['passed']=='7684'
assert sdk['summary']['assemblies'][0]['skipped']=='25'
abi=json.loads((P/'audit/scope-sdk-optional-and-abi-audit.json').read_text())
assert abi['current_skips_match_historical_exactly'] and abi['new_optional_skips']==0
assert abi['published_abi_all_pass'] and abi['published_abi_cases']==3
assert abi['expected_archive_hash_or_shared_cache_changed'] is False
runtime=json.loads((P/'audit/actual-sdk-runtime-binding-audit.json').read_text())
assert runtime['all_saved_observations_match'] and len(runtime['attempts'])==len(strict)
assert all(not r['issues'] for r in runtime['attempts'])
preservation=json.loads((P/'audit/prior-packet-preservation.json').read_text())
assert len(preservation)==3 and all(r['exit_code']==0 for r in preservation)
assert q['evidence_privacy']['raw_logs_exported'] is False and q['evidence_privacy']['raw_xml_exported'] is False
privacy=json.loads((P/'privacy-scan.json').read_text());assert not privacy['recognizable_secret_findings']
print(json.dumps({'sealed_files':len(actual),'retained_strict_attempts':len(strict),'revised_strict_passed':q['revised_strict_passed'],'whole_story_complete':False}))
