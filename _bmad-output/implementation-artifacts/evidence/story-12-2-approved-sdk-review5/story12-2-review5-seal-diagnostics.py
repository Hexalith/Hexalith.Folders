from pathlib import Path
import hashlib,json,shutil,datetime
root=Path('/home/administrator/projects/hexalith/folders');base=root/'_bmad-output/implementation-artifacts';p=base/'evidence/story-12-2-approved-sdk-review5';assert not p.exists();p.mkdir(parents=True);sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
for name,directory in [('edge','/tmp/edge-review5-repro'),('blind','/tmp/review5-blind-probes'),('parent-controls','/tmp/story12-2-review5-parent-probes'),('historical','/tmp/story12-2-review5-historical-probe'),('cancellation','/tmp/story12-2-review5-cancellation-probe')]:
 for f in Path(directory).iterdir():
  if f.is_file() and f.suffix in ('.cs','.csproj'):
   d=p/'probes'/name/f.name;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,d)
for f in Path('/tmp').glob('story12-2-review5-*.log'):shutil.copy2(f,p/f.name)
for name in ['story12-2-review5-concurrent-recording-source-audit.json','story12-2-review5-external-owned-overlaps.json','story12-2-review5-external-restore-plan.json','story12-2-review5-prepare-loopback.py','story12-2-review5-record-triage.py','story12-2-review5-seal-diagnostics.py']:
 shutil.copy2(Path('/tmp')/name,p/name)
for f in Path('/tmp').glob('story12-2-review5-external-*.diff'):shutil.copy2(f,p/f.name)
keep=Path('/tmp/story12-2-sdk-review5-keep-79zyj2r0')
for name in ['RecordingDaprClient.cs','ReviewThreeBatchAccessor.cs']:
 f=next((keep/'approved-working-tree/references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Projections').glob(name));d=p/'probes/compiled-helper-sources'/name;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,d)
record=json.loads((base/'review-12-2-approved-sdk-round5-20261020.json').read_text());(p/'review-at-loopback.json').write_text(json.dumps(record,indent=2)+'\n')
guard=json.loads(Path('/tmp/story12-2-review5-canonical-stage-guard.json').read_text());historical=json.loads((Path('/tmp/story12-2-approved-sdk-review5-mmf1kc3q/new')/'_bmad-output/implementation-artifacts/evidence/story-12-2-durability.json').read_text());assert guard['stages']==historical['stages'];records=[]
for parent in [base/'evidence',root/'references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch']:
 for manifest in parent.glob('**/SHA256SUMS'):
  if 'story-12-2' not in str(manifest):continue
  for line in manifest.read_text().splitlines():
   h,n=line.split(None,1);assert sha(manifest.parent/n.lstrip('*'))==h,(manifest,n)
  records.append(dict(packet=str(manifest.parent),manifest_sha256=sha(manifest),artifacts=len(manifest.read_text().splitlines())))
(p/'historical-audit.json').write_text(json.dumps(dict(packets=records,ten_canonical_stages_unchanged=True),indent=2)+'\n')
commands=[dict(command=['dotnet','run','--project','/tmp/edge-review5-repro/edge.csproj','-p:NuGetAudit=false'],exit_code=0,log='story12-2-review5-edge-repro.log'),dict(command=['dotnet','run','--project','/tmp/review5-blind-probes/repro.csproj','--verbosity','quiet'],exit_code=0,log='story12-2-review5-blind-repro.log'),dict(command=['dotnet','run','--project','/tmp/story12-2-review5-parent-probes/parent.csproj','--verbosity','quiet'],exit_code=0,log='story12-2-review5-parent-repro3.log'),dict(command=['dotnet','run','--project','/tmp/story12-2-review5-historical-probe/historical.csproj','--verbosity','quiet'],exit_code=0,log='story12-2-review5-historical-probe.log'),dict(command=['dotnet','run','--project','/tmp/story12-2-review5-cancellation-probe/cancel.csproj','--verbosity','quiet'],exit_code=0,log='story12-2-review5-cancellation-probe.log')]
(p/'diagnostics.json').write_text(json.dumps(dict(recorded_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='diagnostic only; not acceptance',commands=commands,earlier_attempts=['parent-repro.log is initial control before adding valid terminal time and restoring original foreign batch profile.','parent-repro2.log is empty: dotnet host aborted with exit134 before diagnostic output. Exact reason unclassified; subsequent unchanged-source run exited0.','Warnings in standalone helper compilation are retained and not final repository build results.'],source_provenance='SDK/consumer binaries referenced by temporary projects originate from Stage10/SDKreview4 groups. External merges changed current sources during the review; this diagnostic is not qualification of integrated current source. Preserved helper sources and positive snapshot retain all inputs; fresh SDKreview5 and consumer qualification are required.',external_changes_preserved=True,positive_preservation=str(keep),git_mutations=False),indent=2)+'\n')
(p/'.gitattributes').write_text('* -text\n');lines=[sha(f)+'  '+str(f.relative_to(p)) for f in sorted(p.rglob('*')) if f.is_file() and f.name!='SHA256SUMS'];(p/'SHA256SUMS').write_text('\n'.join(lines)+'\n')
print(json.dumps(dict(packet=str(p),artifacts=len(lines),manifest_sha256=sha(p/'SHA256SUMS'),prior_packets_verified=len(records),canonical_stages=10),indent=2))
