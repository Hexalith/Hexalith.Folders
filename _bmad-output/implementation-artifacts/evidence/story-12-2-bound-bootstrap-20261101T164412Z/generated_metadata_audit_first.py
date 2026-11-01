import json,re,hashlib,sys
from pathlib import Path
p=Path(__file__).parent;raw=json.loads((p/sys.argv[1]).read_text());by_pdb={a['pdb']:a for a in raw['assemblies']};rows=[]
for doc in raw['documents']:
 if doc['matches'] is not False:continue
 file=Path(doc['path']);assert '/obj/' in str(file) and file.name.endswith('.AssemblyInfo.cs')
 asm=by_pdb[doc['pdb']];binary=Path(asm['dll']).read_bytes();data=file.read_bytes();replacements={}
 values=set(v.decode() for v in re.findall(rb'[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[0-9]{14})?\+[0-9a-f]{40}',binary))
 assert len(values)==1,(asm['dll'],values)
 info=next(iter(values));version=asm['version']
 for attribute,value in [('AssemblyFileVersionAttribute',version),('AssemblyVersionAttribute',version),('AssemblyInformationalVersionAttribute',info)]:
  pattern=rb'(System\.Reflection\.'+attribute.encode()+rb'\(")([^"]+)("\))';matches=list(re.finditer(pattern,data));assert len(matches)==1
  old=matches[0].group(2).decode()
  if old!=value:replacements[attribute]={'currentSharedObj':old,'compiledAssembly':value}
  data=re.sub(pattern,lambda m:m.group(1)+value.encode()+m.group(3),data)
 digest=hashlib.sha256(data).hexdigest();rows.append({'pdb':doc['pdb'],'sourcePath':doc['path'],'expectedPdbSha256':doc['expected'],'currentSharedObjSha256':doc['actual'],'reconstructedSha256':digest,'matchesExpected':digest==doc['expected'],'replacements':replacements,'assembly':asm})
report={'method':'Recover the compiled assembly version and its unique informational-version attribute from the actual PE. Substitute only those standard AssemblyInfo version attributes into the current shared generated template, then require exact equality with the PDB document SHA-256. This is reconstruction for audit, not a claim that the old obj file was retained. No product output is modified.','rows':rows}
(p/(sys.argv[2] if len(sys.argv)>2 else 'generated-metadata-profiles.json')).write_text(json.dumps(report,indent=2)+'\n');print('metadata profiles',len(rows),'exact reconstructions',sum(r['matchesExpected'] for r in rows));assert all(r['matchesExpected'] for r in rows)
