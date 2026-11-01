import hashlib,json,subprocess,sys,re,time,os,shutil
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path('/home/administrator/projects/hexalith/folders'); OUT=Path(__file__).parent

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,obj): (OUT/name).write_text(json.dumps(obj,indent=2)+'\n')
def git(repo,*args): return subprocess.check_output(['git','-C',str(repo),*args],text=True).strip()
def repos():
 paths=git(ROOT,'config','--file','.gitmodules','--get-regexp',r'^submodule\..*\.path$')
 return [ROOT]+[ROOT/line.split(' ',1)[1] for line in paths.splitlines()]
def heads(): return {str(r.relative_to(ROOT)):git(r,'rev-parse','HEAD') for r in repos() if (r/'.git').exists()}
def sources():
 allowed={'.cs','.csproj','.props','.targets','.json','.yml','.yaml','.xml','.config','.slnx','.resx','.razor','.js','.ts','.sh','.py','.md'}
 result={}
 for r in repos():
  if not (r/'.git').exists(): continue
  paths=subprocess.check_output(['git','-C',str(r),'ls-files','--cached','--others','--exclude-standard','-z']).decode().split('\0')
  for name in paths:
   p=r/name; parts=Path(name).parts
   if not name or any(x in {'references','.artifacts','bin','obj','node_modules','.agents','.claude','.codex','skills','evidence'} for x in parts): continue
   if p.is_file() and (p.suffix in allowed or p.name in {'.editorconfig','.gitattributes','.gitmodules'}):result[str(p.relative_to(ROOT))]=sha(p)
 return result

def runtime():
 previous=json.loads((ROOT/'.artifacts/story-12-2-current-live-composition-20261101T133424Z/runtime-before.json').read_text())
 result={}
 for group,items in previous.items():
  dirs={Path(x).parent for x in items}
  result[group]={str(p):sha(p) for d in dirs for p in sorted(d.iterdir()) if p.is_file() and p.suffix in {'.dll','.pdb','.json','.config','.xml'}}
 plan=json.loads((OUT/'verification-plan.json').read_text())
 for label,command in plan.items():
  if command[0]=='dotnet' and len(command)>1 and command[1].endswith('.dll'):
   directory=(ROOT/command[1]).parent
   if directory.is_dir() and not any(str(next(iter(group),'')) and Path(next(iter(group))).parent==directory for group in result.values()):result[label]={str(p):sha(p) for p in sorted(directory.iterdir()) if p.is_file() and p.suffix in {'.dll','.pdb','.json','.config','.xml'}}
 for label,project in [('sdk-contracts-tests','Hexalith.EventStore.Contracts.Tests'),('sdk-server-tests','Hexalith.EventStore.Server.Tests')]:
  directory=ROOT/'references/Hexalith.EventStore/tests'/project/'bin/Debug/net10.0'
  if directory.is_dir() and not any(str(next(iter(group),'')) and Path(next(iter(group))).parent==directory for group in result.values()):result[label]={str(p):sha(p) for p in sorted(directory.iterdir()) if p.is_file() and p.suffix in {'.dll','.pdb','.json','.config','.xml'}}
 directory=ROOT/'references/Hexalith.Platform/src/Hexalith.Platform.Composition/bin/Debug/net10.0'
 if directory.is_dir():result['platform-owner-output']={str(file):sha(file) for file in sorted(directory.iterdir()) if file.is_file() and file.suffix in {'.dll','.pdb','.json','.config','.xml'}}
 directory=ROOT/'src/Hexalith.Folders.UI/bin/Debug/net10.0'
 if directory.is_dir():result['ui']={str(p):sha(p) for p in sorted(directory.iterdir()) if p.is_file() and p.suffix in {'.dll','.pdb','.json','.config','.xml'}}
 return result

def native_tools():
 result={}
 for name in ['aspire','dapr','daprd','dotnet','docker','openssl']:
  selected=shutil.which(name)
  if selected:
   file=Path(selected).resolve()
   if file.is_file():result[name]={'path':str(file),'sha256':sha(file)}
 file=Path('/home/administrator/.dapr/bin/daprd')
 if file.is_file():result['dapr-runtime']={'path':str(file.resolve()),'sha256':sha(file)}
 return result

NATIVE_HASHES={}
def native_processes(initial):
 result=[]
 safe={'--app-id','--dapr-http-port','--dapr-grpc-port','--dapr-internal-grpc-port','--app-port','--app-protocol','--resources-path','--config','--log-level','--sentry-address','--enable-mtls'}
 for proc in Path('/proc').iterdir():
  if not proc.name.isdigit() or proc.name in initial:continue
  try:
   if (proc/'comm').read_text().strip()!='daprd':continue
   namespace=next((value.split(b'=',1)[1].decode() for value in (proc/'environ').read_bytes().split(b'\0') if value.startswith(b'NAMESPACE=')),None)
   if not namespace or not namespace.startswith('folders-durability-'):continue
   executable=Path(os.readlink(proc/'exe'));stat=executable.stat();key=(str(executable),stat.st_size,stat.st_mtime_ns)
   if key not in NATIVE_HASHES:NATIVE_HASHES[key]=sha(executable)
   argv=[value.decode() for value in (proc/'cmdline').read_bytes().split(b'\0') if value];args={}
   for index,value in enumerate(argv):
    option=value.split('=',1)[0]
    if option in safe:args.setdefault(option,[]).append((value.split('=',1)[1].lower()=='true' if '=' in value else True) if option=='--enable-mtls' else value.split('=',1)[1] if '=' in value else argv[index+1] if index+1<len(argv) else None)
   result.append({'processId':int(proc.name),'startTicks':(proc/'stat').read_text().rsplit(')',1)[1].split()[19],'namespace':namespace,'executable':str(executable),'sha256':NATIVE_HASHES[key],'arguments':args})
  except (FileNotFoundError,PermissionError,ProcessLookupError,IndexError):continue
 return result

def containers():
 p=subprocess.run(['docker','ps','-a','--no-trunc','--format','{{.ID}} {{.Names}}'],capture_output=True,text=True)
 return {'exitCode':p.returncode,'entries':p.stdout.splitlines(),'stderr':p.stderr.strip()}
def aspire():
 p=subprocess.run(['aspire','ps','--format','json'],capture_output=True,text=True)
 return {'argv':['aspire','ps','--format','json'],'exitCode':p.returncode,'stdout':p.stdout,'stderr':p.stderr}
def drift(items,relative=False):
 return [name for name,digest in items.items() if not (p:=ROOT/name if relative else Path(name)).is_file() or sha(p)!=digest]
def seals():
 findings=[]; count=0
 for manifest in (ROOT/'_bmad-output/implementation-artifacts/evidence').glob('*/SHA256SUMS'):
  for line in manifest.read_text().splitlines():
   if not line.strip():continue
   digest,name=line.split(maxsplit=1);p=manifest.parent/name.lstrip('*')
   count+=1
   if not p.is_file() or sha(p)!=digest.lower():findings.append(str(p.relative_to(ROOT)))
 return {'filesChecked':count,'drift':findings}

def mapped(initial_pids):
 data=[]
 for proc in Path('/proc').iterdir():
  if not proc.name.isdigit() or proc.name in initial_pids:continue
  try:
   comm=(proc/'comm').read_text().strip()
   if not any(x in comm.lower() for x in ['dotnet','hexalith','task36']):continue
   paths={line[line.index('/'):].strip() for line in (proc/'maps').read_text().splitlines() if '/' in line}
   expected=[p for p in paths if p.endswith('.dll') and p.startswith(str(ROOT)) and '/bin/Debug/' in p and Path(p).name.startswith(('Hexalith.','Task36FoldObserver'))]
   if not expected:continue
   key=b'EventStore__RouteCatalog__DevelopmentBootstrap__Directory='
   env=(proc/'environ').read_bytes().split(b'\0')
   directory=next((entry[len(key):].decode() for entry in env if entry.startswith(key)),None)
   for p in sorted(expected):
    f=Path(p);data.append({'processId':int(proc.name),'comm':comm,'catalogDirectory':directory,'path':p,'sha256':sha(f),'pdbSha256':sha(f.with_suffix('.pdb')) if f.with_suffix('.pdb').is_file() else None})
  except (FileNotFoundError,PermissionError,ProcessLookupError,IndexError):continue
 return data

mode=sys.argv[1]
if mode=='before':
 name=sys.argv[2];save(name+'-source-before.json',{'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'heads':heads(),'sha256':sources()});save(name+'-runtime-before.json',runtime());save(name+'-containers-before.json',containers());save(name+'-aspire-before.json',aspire());save(name+'-native-tools-before.json',native_tools());save(name+'-processes-before.json',[x.name for x in Path('/proc').iterdir() if x.name.isdigit()]);save(name+'-temporary-directories-before.json',[str(d) for b in [Path('/tmp'),Path('/dev/shm')] for d in b.glob('folders-durability-*') if d.is_dir()]);print('captured',name)
elif mode=='after':
 name=sys.argv[2];source=json.loads((OUT/(name+'-source-before.json')).read_text());rt=json.loads((OUT/(name+'-runtime-before.json')).read_text());current=heads();raw=(OUT/(name+'.raw.log')).read_text(errors='replace') if (OUT/(name+'.raw.log')).exists() else '';ids=sorted(set(re.findall(r'(?:folders-durability-|folders-provider-|durability-)([0-9a-f]{16})',raw)));temps=sorted(set(re.findall(r'/tmp/folders-durability-[a-z0-9.]+',raw)));cont=containers();bound=[]
 for line in raw.splitlines():
  if 'RUNTIME_BINDING ' in line:
   try: bound.append(json.loads(line.split('RUNTIME_BINDING ',1)[1]))
   except json.JSONDecodeError:pass
 save(name+'-runtime-bindings.json',bound)
 save(name+'-audit.json',{'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'source':{'count':len(source['sha256']),'drift':drift(source['sha256'],True),'headDrift':{k:[v,current.get(k)] for k,v in source['heads'].items() if current.get(k)!=v}},'runtime':{g:{'count':len(v),'drift':drift(v)} for g,v in rt.items()},'runIds':ids,'ownedContainersRemaining':[c for c in cont['entries'] if any(i in c for i in ids)],'temporaryDirectories':{p:Path(p).exists() for p in temps},'containersAfter':cont,'aspireAfter':aspire(),'priorSeals':seals(),'nativeToolsAfter':native_tools(),'actualBindings':len(bound)});print('audited',name)
elif mode=='monitor':
 name=sys.argv[2];initial=set(json.loads((OUT/(name+'-processes-before.json')).read_text()));seen={};cs={};directories={};catalogs={};images={};natives={};mtls={};nativeconfigs={}
 while not (OUT/(name+'.json')).exists():
  for native in native_processes(initial):
   natives[(native['processId'],native['startTicks'])]=native
   for flag in ['--config','--resources-path']:
    for value in native['arguments'].get(flag,[]):
     path=Path(value)
     files=[path] if path.is_file() else list(path.rglob('*.yaml'))+list(path.rglob('*.yml')) if path.is_dir() else []
     for file in files:
      try:
       digest=sha(file);key=str(file)+'|'+digest
       nativeconfigs.setdefault(key,{'path':str(file),'sha256':digest,'argument':flag,'processIds':[]})
       if native['processId'] not in nativeconfigs[key]['processIds']:nativeconfigs[key]['processIds'].append(native['processId'])
      except FileNotFoundError:pass
  for item in mapped(initial):
   seen[(item['processId'],item['path'])]=item
   if item.get('catalogDirectory'):
    directory=Path(item['catalogDirectory']);snapshot=catalogs.setdefault(str(directory),{'files':{},'readbacks':{},'bootstrapReceipts':[]})
    if directory.is_dir():
     for f in directory.iterdir():
      if f.is_file():
       try:snapshot['files'][f.name]=sha(f)
       except FileNotFoundError:continue
       if f.name.endswith('.committed.json'):
        try:
         doc=json.loads(f.read_text());readback=doc.get('readback',doc)
         snapshot['readbacks'][f.name]={k:v for k,v in readback.items() if k.lower() in {'hostid','appid','localappid','localhostid','attemptid','authoritydigest','localhostdigest','fingerprintdigest','hostfingerprint','serving','epoch','rootdigest','activationbindingdigest','generation','profile'}}
        except (FileNotFoundError,json.JSONDecodeError):pass
       if f.name=='tenants.bootstrap.json':
        try:
         receipt=json.loads(f.read_text());allowed={'attemptid','rootdigest','messageid','correlationid','phase','httpstatus'}
         safe={key:value for key,value in receipt.items() if key.lower() in allowed}
         principal=next((value for key,value in receipt.items() if key.lower()=='principalid'),None)
         safe['principalSha256']=hashlib.sha256(principal.encode()).hexdigest() if isinstance(principal,str) else None
         safe['receiptSha256']=sha(f)
         if safe not in snapshot['bootstrapReceipts']:snapshot['bootstrapReceipts'].append(safe)
        except (FileNotFoundError,json.JSONDecodeError):pass
  raw=OUT/(name+'.raw.log')
  if raw.is_file():
   for line in raw.read_text(errors='replace').splitlines():
    if 'MTLS_RUNTIME_BINDING ' not in line:continue
    try:binding=json.loads(line.split('MTLS_RUNTIME_BINDING ',1)[1])
    except json.JSONDecodeError:continue
    for key in ['runDirectory','certificateDirectory']:
     value=binding.get(key)
     if isinstance(value,str) and Path(value).is_absolute():
      directories[value]=True
      directory=Path(value);item=mtls.setdefault(value,{'kind':key,'files':{}})
      if directory.is_dir():
       for file in directory.rglob('*'):
        if file.is_file() and file.suffix in {'.crt','.yaml','.yml'}:
         try:item['files'][str(file.relative_to(directory))]=sha(file)
         except FileNotFoundError:pass
  before=set(json.loads((OUT/(name+'-containers-before.json')).read_text())['entries'])
  for item in containers()['entries']:
   if item not in before:
    cs[item]=True
    if item not in images:
     identity=subprocess.run(['docker','inspect','--format','{{json .Image}}',item.split()[0]],capture_output=True,text=True)
     if identity.returncode==0:
      try:images[item]=json.loads(identity.stdout)
      except json.JSONDecodeError:pass
  for base in [Path('/tmp'),Path('/dev/shm')]:
   for d in base.glob('folders-durability-*'):
    if d.is_dir() and str(d) not in json.loads((OUT/(name+'-temporary-directories-before.json')).read_text()):directories[str(d)]=True
  save(name+'-observations.json',{'mappedModules':list(seen.values()),'newContainersSeen':list(cs),'temporaryDirectoriesSeen':list(directories),'catalogDirectories':catalogs,'containerImageIds':images,'nativeProcesses':list(natives.values()),'mtlsInfrastructure':mtls,'nativeConfigurations':list(nativeconfigs.values())})
  time.sleep(3)
 print('monitor complete',len(seen),'mappings')
elif mode=='seals':print(json.dumps(seals()))
