import subprocess,sys,json,hashlib,collections
from pathlib import Path
p=Path(__file__).parent;root=Path.cwd();name=sys.argv[1]
subprocess.run([sys.executable,str(p/'audit.py'),'before',name],check=True)
tool=json.loads((p/'pdb-tool.json').read_text());dll=str(Path(tool['directory'])/'bin/Debug/net10.0/PdbAudit.dll');label='pdb-'+name
subprocess.run([sys.executable,str(p/'run.py'),label,'dotnet',dll,str(p/(name+'-runtime-before.json'))],check=True)
metadata_name=name+'-generated-metadata.json'
subprocess.run([sys.executable,str(p/'generated_metadata_audit.py'),label+'.raw.log',metadata_name],check=True)
raw=json.loads((p/(label+'.raw.log')).read_text());metadata=json.loads((p/metadata_name).read_text());verified={(row['pdb'],row['sourcePath'],row['expectedPdbSha256']) for row in metadata['rows'] if row['matchesExpected']}
ordinary=[r for r in raw['documents'] if '/obj/' not in r['path'] and r['matches'] is not True]
unresolved=[r for r in raw['documents'] if r['matches'] is False and (r['pdb'],r['path'],r['expected']) not in verified]
versions=sorted({a['version'] for a in raw['assemblies'] if Path(a['dll']).name.startswith('Hexalith.EventStore.') and not a['dll'].endswith('Tests.dll')})
rt=json.loads((p/(name+'-runtime-before.json')).read_text());live={'folders','workers','gateway','tenants','memories','apphost','tests','observer','ui'};byname=collections.defaultdict(lambda:collections.defaultdict(list))
for group,items in rt.items():
 if group not in live:continue
 for path,digest in items.items():
  f=Path(path)
  if f.name.startswith('Hexalith.EventStore.') and f.suffix in {'.dll','.pdb'} and not f.stem.endswith('.Tests'):byname[f.name][digest].append({'group':group,'path':path})
variants={key:dict(value) for key,value in byname.items() if len(value)>1}
initial=json.loads((p/'initial-state.json').read_text());spec=(root/'_bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md').read_text();block=spec.split('<frozen-after-approval',1)[1].split('</frozen-after-approval>',1)[0]
frozen=hashlib.sha256(block.encode()).hexdigest()==initial['frozenBlockSha256'];strict=(root/'tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs').read_bytes()==(p/'strict-acceptance-before.cs').read_bytes()
report={'ordinaryDocumentRecords':sum('/obj/' not in r['path'] for r in raw['documents']),'ordinaryMismatches':ordinary,'unresolvedGeneratedMismatches':unresolved,'exactMetadataReconstructions':len(metadata['rows']),'sdkVersions':versions,'liveSdkArtifactNames':len(byname),'liveSdkVariants':variants,'frozenSpecBlockUnchanged':frozen,'strictBodyUnchanged':strict,'passed':not ordinary and not unresolved and versions==['3.130.2.0'] and not variants and frozen and strict}
(p/(name+'-qualification.json')).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k not in ['ordinaryMismatches','unresolvedGeneratedMismatches','liveSdkVariants']}));assert report['passed']
