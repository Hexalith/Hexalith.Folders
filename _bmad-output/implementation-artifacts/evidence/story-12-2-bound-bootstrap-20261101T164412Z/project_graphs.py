import json,hashlib
from pathlib import Path
p=Path(__file__).parent;root=Path.cwd();plan=json.loads((p/'verification-plan.json').read_text());projects={cmd[2] for cmd in plan.values() if cmd[:2]==['dotnet','build']};rows=[]
for project in sorted(projects):
 f=root/Path(project).parent/'obj/project.assets.json'
 if not f.exists():continue
 data=json.loads(f.read_text());libs=data['libraries'];selected={k:v for k,v in libs.items() if k.startswith('Hexalith.')}
 rows.append({'project':project,'assets':str(f.relative_to(root)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'hexalithProjects':{k:v['path'] for k,v in selected.items() if v['type']=='project'},'hexalithPackages':[k for k,v in selected.items() if v['type']=='package']})
(p/'project-reference-graph.json').write_text(json.dumps(rows,indent=2)+'\n');print('graphs',len(rows),'Hexalith package edges',sum(len(v['hexalithPackages']) for v in rows));assert not any(v['hexalithPackages'] for v in rows)
