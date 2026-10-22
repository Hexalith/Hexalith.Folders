from pathlib import Path
import subprocess,json,time,re,datetime
out=Path(__file__).resolve().parent;target=out/'state-snapshots-v2';target.mkdir(exist_ok=True)
script="local c='0';local a={};repeat local r=redis.call('SCAN',c,'MATCH','shared-*','COUNT',1000);c=r[1];for _,k in ipairs(r[2]) do if string.match(k,'^shared%-epoch:[%w_%-]+$') or string.match(k,'^shared%-checkpoint:[%w_%-]+:[%x]+$') then if redis.call('TYPE',k).ok=='hash' then table.insert(a,{k,redis.call('HGET',k,'data'),redis.call('HGET',k,'version')}) end end end until c=='0';return cjson.encode(a)"
while not (out/'stop-state-observer-v2').exists():
 stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
 names=subprocess.check_output(['docker','ps','--format','{{.Names}}'],text=True).splitlines()
 for lane in ('capacity-final-live','strict-live'):
  log=out/(lane+'.log')
  if not log.exists():continue
  ids=set(re.findall(r'app_id=(?:folders|eventstore|tenants|memories)[^ ]*-([0-9a-f]{16})(?:[ ,]|$)',log.read_text()))
  for ident in ids:
   name='folders-durability-'+ident
   if name not in names:continue
   cmd=['docker','exec',name,'redis-cli','--raw','EVAL',script,'0'];r=subprocess.run(cmd,text=True,capture_output=True,timeout=45)
   record=dict(utc=stamp,lane=lane,container=name,argv=cmd,exit_code=r.returncode,stdout=r.stdout,stderr=r.stderr)
   (target/(lane+'-'+stamp+'.json')).write_text(json.dumps(record,indent=2)+'\n')
 time.sleep(20)
print('State observer stopped.')
