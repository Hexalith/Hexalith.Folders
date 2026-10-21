from pathlib import Path
import hashlib,json,subprocess,sys,os
root=Path('/home/administrator/projects/hexalith/folders');out=Path(__file__).parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
run=lambda *args,cwd=root:subprocess.check_output(args,cwd=cwd,text=True).strip()
modules=[line.split(' = ',1)[1] for line in (root/'.gitmodules').read_text().splitlines() if line.strip().startswith('path =')]
old=json.loads((root/'_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-review4/story12-2-sdk-review4-consumer-final-source.json').read_text())
if sys.argv[1]=='freeze':
 paths=set(old['files_sha256'])
 for base in [root]+[root/m for m in modules]:
  for folder in ('src','tests','test','Props','eng'):
   top=base/folder
   if not top.exists():continue
   for current,dirs,files in os.walk(top):
    dirs[:]=[d for d in dirs if d not in ('bin','obj','TestResults','node_modules','.git','references','.agents','.codex')]
    for name in files:
     p=Path(current)/name
     if p.suffix in ('.cs','.csproj','.props','.targets','.json','.yaml','.yml','.slnx','.xml','.razor','.resx','.config','.sh','.ps1','.py','.md'):
      paths.add(str(p.relative_to(root)))
  for name in ('global.json','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','.editorconfig','.gitattributes','NuGet.Config','nuget.config','.gitmodules'):
   p=base/name
   if p.is_file(): paths.add(str(p.relative_to(root)))
 binaries=set(old['assemblies_sha256'])
 # All root runtime/test Hexalith PE and portable PDB copies, including incoming dependency code.
 for folder in ('src','tests'):
  for current,dirs,files in os.walk(root/folder):
   dirs[:]=[d for d in dirs if d not in ('obj','node_modules','TestResults')]
   for name in files:
    p=Path(current)/name
    if '/bin/Debug/' in str(p) and '/tests/tools/' not in str(p) and p.suffix in ('.dll','.pdb') and name.startswith('Hexalith.'):
     binaries.add(str(p.relative_to(root)))
 heads={m:{'head':run('git','rev-parse','HEAD',cwd=root/m),'root_gitlink':run('git','ls-tree','HEAD',m),'status':run('git','status','--short',cwd=root/m)} for m in modules if (root/m/'.git').exists()}
 data={'root_head':run('git','rev-parse','HEAD'),'submodules':heads,'files_sha256':{p:sha(root/p) for p in sorted(paths)},'assemblies_sha256':{p:sha(root/p) for p in sorted(binaries)}}
 (out/'source-freeze.json').write_text(json.dumps(data,indent=2)+'\n')
 pdbs=[p for p in binaries if p.endswith('.pdb')]
 r=subprocess.run(['dotnet','/tmp/review1-pdb-verifier/bin/Debug/net10.0/Verifier.dll',*sorted(pdbs)],cwd=root,text=True,capture_output=True)
 (out/'pdb-inspection.json').write_text(r.stdout);(out/'pdb-inspection.stderr').write_text(r.stderr)
 assert r.returncode==0,r.stderr
 rows=json.loads(r.stdout);audit=[{k:v for k,v in x.items() if k!='documents'} for x in rows]
 (out/'pdb-audit.json').write_text(json.dumps(audit,indent=2)+'\n')
 print({'source_files':len(paths),'binary_files':len(binaries),'pdbs':len(pdbs),'pdb_summary':audit[:1]})
else:
 d=json.loads((out/'source-freeze.json').read_text());changes={key:[p for p,h in d[key].items() if not(root/p).exists() or sha(root/p)!=h] for key in ('files_sha256','assemblies_sha256')}
 changes['submodule_heads']=[m for m,s in d['submodules'].items() if run('git','rev-parse','HEAD',cwd=root/m)!=s['head']]
 (out/'after-execution-audit.json').write_text(json.dumps(changes,indent=2)+'\n');print(changes);assert not any(changes.values())
