from pathlib import Path
import hashlib,json,subprocess
root=Path('/home/administrator/projects/hexalith/folders')
sdk=root/'references/Hexalith.EventStore'
stem=Path('/tmp/story12-2-sdk-review2-consumer')
old=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-20261020'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
base=json.loads((old/'story12-2-sdk-review1-consumer-final-source.json').read_text())
mode=__import__('sys').argv[1]
if mode=='freeze':
 paths=set(base['files_sha256'])
 for folder in ('src','tests'):
  paths.update(str(p.relative_to(root)) for p in (root/folder).rglob('*') if p.is_file() and not any(s in ('bin','obj','TestResults','node_modules') for s in p.relative_to(root).parts))
 data={'files_sha256':{str(p):sha(root/p) for p in sorted(paths)},'assemblies_sha256':{p:sha(root/p) for p in base['assemblies_sha256']},'sdk_source_freeze_sha256':sha(sdk/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review2/source-final.json'),'heads':{'root':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'sdk':subprocess.check_output(['git','rev-parse','HEAD'],cwd=sdk,text=True).strip(),'recorded_sdk_gitlink':subprocess.check_output(['git','ls-tree','HEAD','references/Hexalith.EventStore'],cwd=root,text=True).strip()}}
 Path(str(stem)+'-final-source.json').write_text(json.dumps(data,indent=2)+'\n')
 oldpdbs=json.loads((old/'story12-2-sdk-review1-consumer-pdb-audit.json').read_text())
 r=subprocess.run(['dotnet','/tmp/review1-pdb-verifier/bin/Debug/net10.0/Verifier.dll',*[x['pdb'] for x in oldpdbs]],cwd=root,text=True,capture_output=True)
 Path(str(stem)+'-pdb-source-inspection.json').write_text(r.stdout)
 assert r.returncode==0,r.stderr
 audit=[{k:v for k,v in x.items() if k!='documents'} for x in json.loads(r.stdout)]
 Path(str(stem)+'-pdb-audit.json').write_text(json.dumps(audit,indent=2)+'\n')
 print(json.dumps({'source_files':len(data['files_sha256']),'assembly_files':len(data['assemblies_sha256']),'pdbs':audit},indent=2))
else:
 data=json.loads(Path(str(stem)+'-final-source.json').read_text())
 changes={k:[p for p,h in data[k].items() if not(root/p).exists() or sha(root/p)!=h] for k in ('files_sha256','assemblies_sha256')}
 frozen=json.loads((sdk/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review2/source-final.json').read_text())
 changes['sdk_source']=[x['path'] for x in frozen if not(sdk/x['path']).exists() or sha(sdk/x['path'])!=x['sha256']]
 Path(str(stem)+'-after-live-audit.json').write_text(json.dumps(changes,indent=2)+'\n')
 print(changes)
 assert not any(changes.values())
