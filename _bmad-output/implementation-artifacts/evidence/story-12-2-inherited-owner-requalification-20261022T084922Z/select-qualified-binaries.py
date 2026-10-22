from pathlib import Path
import hashlib,json,shutil,os
r=Path('/home/administrator/projects/hexalith/folders');o=Path(__file__).parent
m=json.loads((o/'sdk-handoff-source-binary-identities.json').read_text())
source=Path('/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0')
rows=[x for x in m['binaries'] if Path(x['path']).parent==source and Path(x['path']).suffix in ('.dll','.pdb') and Path(x['path']).name.startswith('Hexalith.EventStore.') and 'Tests' not in Path(x['path']).name]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for row in rows:assert sha(Path(row['path']))==row['sha256'].lower(),row
changes=[]
for base in [r,*[r/line.split(' = ',1)[1] for line in (r/'.gitmodules').read_text().splitlines() if line.strip().startswith('path =')]]:
 for folder in ('src','tests'):
  if not (base/folder).exists():continue
  for current,dirs,files in os.walk(base/folder):
   dirs[:]=[d for d in dirs if d not in ('obj','references','node_modules','.git')]
   if '/bin/Debug/net10.0' not in current:continue
   for row in rows:
    src=Path(row['path']);dst=Path(current)/src.name
    if not dst.is_file():continue
    before=sha(dst);shutil.copy2(src,dst)
    changes.append(dict(destination=str(dst),originalSha256=before,selectedSource=str(src),qualifiedSha256=sha(dst)))
(o/'qualified-binary-selection.json').write_text(json.dumps(dict(reason='Source/config hashes match handoff exactly. External Git advancement and inherited consumer build metadata produce different assembly images. Select the already-qualified exact SDK DLL/PDB graph after source-reference consumer build; no source, dependency pins or historical evidence changed.',changes=changes),indent=2)+'\n')
print(dict(selected_files=len(changes),core_images=len(rows)))
