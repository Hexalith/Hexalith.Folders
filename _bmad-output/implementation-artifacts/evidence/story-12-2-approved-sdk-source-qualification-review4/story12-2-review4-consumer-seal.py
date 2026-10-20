from pathlib import Path
import datetime, hashlib, json, re, shutil, subprocess, xml.etree.ElementTree as E

root = Path('/home/administrator/projects/hexalith/folders')
base = root / '_bmad-output/implementation-artifacts/evidence'
p = base / 'story-12-2-approved-sdk-source-qualification-review4'
assert not p.exists()
p.mkdir(parents=True)
for f in Path('/tmp').glob('story12-2-sdk-review4-consumer-*'):
    if f.is_file():
        shutil.copy2(f, p / f.name)
diagnostics = Path('/tmp/story12-2-sdk-review4-consumer-live-diagnostics')
if diagnostics.exists():
    shutil.copytree(diagnostics, p / 'diagnostics' / 'consumer-live')
for name in ('story12-2-sdk-review4-independent-audit.json', 'story12-2-sdk-review4-independent-worker-audit.json', 'story12-2-sdk-review4-independent-pdb-inspection.json', 'story12-2-review4-historical-packet-audit.json', 'story12-2-review4-consumer-provenance.py', 'story12-2-review4-sdk-audit.py', 'story12-2-review4-consumer-build.py', 'story12-2-review4-consumer-seal.py', 'story12-2-review4-consumer-format-files.json'):
    shutil.copy2(Path('/tmp') / name, p / name)
clock = p/'diagnostics/consumer-clock'
clock.mkdir(parents=True)
for f in Path('/tmp').glob('story12-2-review4-clock-*'):
    if f.is_file(): shutil.copy2(f, clock/f.name)
(p / '.gitattributes').write_text('* -text\n')
old = base / 'story-12-2-approved-sdk-source-qualification-20261020'
q = json.loads((old / 'qualification.json').read_text())
components = json.loads((p / 'story12-2-sdk-review4-consumer-final-components.json').read_text())
assert all(x['exit_code'] == 0 for x in components)
passed = sum(int(a['passed']) for c in components for a in c.get('assemblies', []))
skips = sum(int(a['skipped']) for c in components for a in c.get('assemblies', []))
integration = (p / 'story12-2-sdk-review4-consumer-final-integration.log').read_text()
match = re.search(r'(?:Passed|passed|succeeded):\s*(\d+)', integration, re.I)
assert match, integration[-2500:]
passed += int(match[1])
assert passed == 4201 and skips == 2, (passed, skips)
a = next(E.parse(p / 'story12-2-sdk-review4-consumer-live.xml').getroot().iter('assembly'))
assert a.get('passed') == '1' and a.get('failed') == a.get('skipped') == '0'
log = (p / 'story12-2-sdk-review4-consumer-live.log').read_text(errors='replace')
metrics = []
for match in re.finditer(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([\d.]+) p95_ms=([\d.]+) budget_p95_ms=([\d.]+) over_budget=(True|False)', log):
    name, count, p50, p95, budget, over = match.groups()
    metrics.append(dict(operation=name, count=int(count), p50_ms=float(p50), p95_ms=float(p95), budget_p95_ms=float(budget), over_budget=over == 'True'))
assert len(metrics) == 3
(p / 'performance.json').write_text(json.dumps(metrics, indent=2) + '\n')
clean = []
for cmd in (['aspire', 'ps', '--format', 'Json'], ['docker', 'ps', '--format', '{{.Names}}']):
    r = subprocess.run(cmd, cwd=root, text=True, capture_output=True)
    assert r.returncode == 0, r.stderr
    if cmd[0] == 'aspire':
        hosts = json.loads(r.stdout)
        def sanitize(value):
            if isinstance(value, dict):
                return {k: ('<redacted>' if re.search('token|secret|password|apikey|api_key', k, re.I) else sanitize(v)) for k, v in value.items()}
            if isinstance(value, list): return [sanitize(v) for v in value]
            if isinstance(value, str) and value.startswith(('http://','https://')): return value.split('?',1)[0].split('#',1)[0]
            return value
        r.stdout = json.dumps(sanitize(hosts), indent=2) + '\n'
    clean.append(dict(command=cmd, exit_code=r.returncode, stdout=r.stdout, stderr=r.stderr))
hosts = json.loads(clean[0]['stdout'])
containers = clean[1]['stdout'].split()
owned_run_ids = set(re.findall(r'durability-([0-9a-f]{16})', log))
for failed_log in (p / 'diagnostics').rglob('*.log') if (p / 'diagnostics').exists() else ():
    owned_run_ids.update(re.findall(r'durability-([0-9a-f]{16})', failed_log.read_text(errors='replace')))
assert owned_run_ids
owned_names = {prefix + run_id for run_id in owned_run_ids for prefix in ('folders-durability-', 'folders-provider-')}
assert owned_names.isdisjoint(containers), containers
cleanup = dict(observations=clean, fixture_run_ids=sorted(owned_run_ids), fixture_redis_and_provider_containers_absent=True, successful_test_fixture_disposal=True, unrelated_running_applications_preserved=True, note='Other workspace applications started during this qualification. Inventory is retained; exact fixture Redis/provider names are absent and final test including teardown passes. Cleanup does not assert an empty machine or ownership of unrelated CLI AppHosts.')
(p / 'cleanup.json').write_text(json.dumps(cleanup, indent=2) + '\n')
source = json.loads((p / 'story12-2-sdk-review4-consumer-final-source.json').read_text())
audit = json.loads((p / 'story12-2-sdk-review4-independent-audit.json').read_text())
sdkq = audit['qualification']
results = {row['id']:row['counts'] for row in audit['commands']}
builds = json.loads((p / 'story12-2-sdk-review4-consumer-builds.json').read_text())
assert all(x['exit_code'] == 0 for x in builds)
q.update(recorded_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), status='Approved SDK review-four corrections and Folders source adoption locally qualified; pending independent review. Whole Story12.2 remains in progress.', sdk_packet='../../../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review4/qualification.json')
q['sdk_results'] = dict(final_runs=results, unique_live_cases=sdkq['unique_live_cases'], child_workers=dict(passed=sdkq['worker_invocations'], distinct_pids=sdkq['distinct_worker_pids'], distinct_exited_owner_recovery_pairs=sdkq['exited_worker_pairs'], production_named_pairs=sdkq['production_named_pairs'], primitive_late_installer_pairs=sdkq['primitive_late_installer_pairs']), required_live_skips=0, independent_audit='story12-2-sdk-review4-independent-audit.json', independent_raw_worker_audit='story12-2-sdk-review4-independent-worker-audit.json', note='All final required cases have zero skips; prior failed and interrupted attempts remain in the SDK diagnostics. Counts are taken from final XML rather than inherited from earlier packets.')
q['consumer_results'] = dict(components_passed=passed, existing_optional_domain_skips=skips, failures=0, results='story12-2-sdk-review4-consumer-final-components.json', live_passed=1, live_failed=0, live_skipped=0, live_seconds=float(a.get('time')), live_xml='story12-2-sdk-review4-consumer-live.xml', live_log='story12-2-sdk-review4-consumer-live.log')
if diagnostics.exists():
    q['consumer_results']['earlier_failed_live_attempts'] = 'diagnostics/consumer-live'
    q['consumer_results']['earlier_failed_live_count'] = len(list(diagnostics.glob('attempt*')))
    q['consumer_results']['earlier_failed_live_note'] = 'Attempt1 passed physical-retention, SDK process recovery and provider pipeline milestones, then failed during second EventStore restart: DCP process-stop timeout. Attempt2 timed out waiting WorkspaceLockReleased: Lock completed, Release domain evaluation succeeded, then terminal Rejected at EventsStored with redacted DaprApiException amid native actor429 authority/archive errors. Exact persistence-error cause and delayed-shutdown cause remain unclassified. Failed XML/logs, command timeline and zero-drift source/binary audits are retained. The unchanged-binary final pass does not erase or classify either failure as a flake.'
q['consumer_source'] = dict(inventory='story12-2-sdk-review4-consumer-final-source.json', files=len(source['files_sha256']), assembly_files=len(source['assemblies_sha256']), pdb_inspection='story12-2-sdk-review4-consumer-pdb-source-inspection.json', pdb_audit='story12-2-sdk-review4-consumer-pdb-audit.json', mismatches=0, after_live_audit='story12-2-sdk-review4-consumer-after-live-audit.json', note='All captured source and binary hashes remained unchanged through component and live runs. SDK and consumer dependency binary groups are recorded separately.')
q['builds'] = builds
format_records = json.loads((p / 'story12-2-sdk-review4-consumer-format.json').read_text())
assert len(format_records) == 2 and all(x['exit_code'] == 0 for x in format_records)
q['checks'] = dict(format_and_analyzers='story12-2-sdk-review4-consumer-format.json', include_files=json.loads((p/'story12-2-review4-consumer-format-files.json').read_text()), note='All twelve owned C# files checked with whitespace/analyzers; both passed. Workspace-loading warning retained. Final serialized source build has zero warnings/errors.')
q['clock_regression'] = dict(red_xml='diagnostics/consumer-clock/story12-2-review4-clock-red.xml', green_xml='diagnostics/consumer-clock/story12-2-review4-clock-green.xml', red='All six actual root/Server/manual x selected/inherited cases fail at pre-deadline duplicate line63 because the old writer coordinator hides still-current receipts. Initial analyzer setup failure is retained separately.', green='All six clock cases and fifteen existing retention/capacity cases pass; final full suite includes the six new cases.')
q['live_command']['command'][-1] = '/tmp/story12-2-sdk-review4-consumer-live.xml'
for key in ('package_gate', 'contract_fallback'):
    q[key]['log'] = '../story-12-2-approved-sdk-source-qualification-20261020/' + q[key]['log']
    q[key]['retained_for_unchanged_api_and_candidate'] = 'WriteExpiring API and frozen package/PD10 candidate inventory are unchanged; this packet does not claim package publication or requalification.'
q['historical_evidence'].update(all_nine_prior_stages_preserved=True, previous_consumer_packets_unchanged=True, historical_packet_audit='story12-2-review4-historical-packet-audit.json')
ack = next(x for x in metrics if x['operation'] == 'command_acknowledgement')
q['remaining_story_acceptance'] = ['SDK owner-identity capability beyond10000, qualified consumption and Task36 rerun', 'Release/package and frozen PD10 source-inventory requalification', f"SM4 acknowledgement p95:{ack['p95_ms']}ms versus1000ms on this run; earlier observations preserved", 'Independent review pending for approved SDK/consumer scope at packet seal; whole-story review remains separate']
q['remaining_story_acceptance'].extend(f"Latency observation remains open: {x['operation']} p95={x['p95_ms']}ms versus{x['budget_p95_ms']}ms on this run; concurrent other-workspace activity is recorded without causal attribution" for x in metrics if x['over_budget'] and x['operation'] != 'command_acknowledgement')
q['sdk_packet_sha256_manifest'] = audit['manifest_sha256']
q['heads'] = source['heads']
(p / 'qualification.json').write_text(json.dumps(q, indent=2) + '\n')
(p / 'components.json').write_text(json.dumps(components, indent=2) + '\n')
manifest = [hashlib.sha256(f.read_bytes()).hexdigest() + '  ' + str(f.relative_to(p)) for f in sorted(p.rglob('*')) if f.is_file() and f.name != 'SHA256SUMS']
(p / 'SHA256SUMS').write_text('\n'.join(manifest) + '\n')
print(json.dumps(dict(packet=str(p), artifacts=len(manifest), components_passed=passed, live_seconds=float(a.get('time')), performance=metrics), indent=2))
