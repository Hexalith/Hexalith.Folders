import json,xml.etree.ElementTree as E,re
from pathlib import Path
p=Path(__file__).parent;name=__import__('sys').argv[1]
a=json.loads((p/(name+'-audit.json')).read_text());o=json.loads((p/(name+'-observations.json')).read_text());rt=json.loads((p/(name+'-runtime-before.json')).read_text());expected={k:v for g in rt.values() for k,v in g.items()}
processes=sorted({x['processId'] for x in o['mappedModules']}|{x['processId'] for x in o.get('nativeProcesses',[])});paths=list(o['catalogDirectories'])+o['temporaryDirectoriesSeen'];mapped=[];unbound=[]
for item in o['mappedModules']:
 if item['path'] not in expected:
  unbound.append(item);continue
 if expected.get(item['path']) != item['sha256']:mapped.append({'path':item['path'],'processId':item['processId'],'reason':'mapped DLL differs from pre-run inventory'})
 if item.get('pdbSha256') and expected.get(str(Path(item['path']).with_suffix('.pdb'))) != item['pdbSha256']:mapped.append({'path':item['path'],'reason':'mapped module PDB differs from pre-run inventory'})
remaining=a['containersAfter']['entries'];seen=o['newContainersSeen'];assemblies=E.parse(p/(name+'.xml')).getroot().findall('assembly');raw=(p/(name+'.raw.log')).read_text(errors='replace')
report={'result':[{k:v.get(k) for k in ['total','passed','failed','errors','skipped','time']} for v in assemblies],
 'sourceAudit':a['source'],'runtimeGroups':a['runtime'],'uniqueRuntimePaths':len(expected),'observedMappedRecords':len(o['mappedModules']),'mappedMismatches':mapped,'observationsOutsidePreRunInventory':unbound,
 'processCleanup':{str(pid):Path('/proc/'+str(pid)).exists() for pid in processes},'directoryCleanup':{path:Path(path).exists() for path in paths},
 'nativeConfigurations':o.get('nativeConfigurations',[]),'mtlsInfrastructure':o.get('mtlsInfrastructure',{}),'nativeProcesses':o.get('nativeProcesses',[]),'containerImageIds':o.get('containerImageIds',{}),'bootstrapReceipts':{k:v.get('bootstrapReceipts',[]) for k,v in o['catalogDirectories'].items()},'newContainersObserved':seen,'newContainersRemaining':[entry for entry in seen if entry in remaining],
 'aspireAfter':a['aspireAfter'],'catalogReadbacks':{k:v['readbacks'] for k,v in o['catalogDirectories'].items()},
 'safeAcceptanceMilestones':[line.strip() for line in raw.splitlines() if line.strip().startswith(('PASS ','PERFORMANCE ','BOOTSTRAP_VERIFIED '))],
 'strictTestSourceSha256':__import__('hashlib').sha256(Path('tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs').read_bytes()).hexdigest()}
(p/(name+'-summary.json')).write_text(json.dumps(report,indent=2)+'\n');print('summary',name,'mapped',len(o['mappedModules']),'mismatches',len(mapped),'remaining resources',sum(report['processCleanup'].values())+sum(report['directoryCleanup'].values())+len(report['newContainersRemaining']))
