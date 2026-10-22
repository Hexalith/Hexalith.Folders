from pathlib import Path
import datetime,json,os,re,subprocess
out=Path(__file__).parent
ids=set()
for log in out.glob('*live*.log'):
 ids.update(re.findall(r'app_id=(?:folders|eventstore|tenants|memories)[^ ]*-([0-9a-f]{16})(?:[ ,]|$)',log.read_text()))
remaining=[];checked=0
for process in Path('/proc').iterdir():
 if not process.name.isdigit():continue
 try:
  if process.stat().st_uid!=os.getuid():continue
  environment=(process/'environ').read_bytes();checked+=1
  matches=[value for value in ids if value.encode() in environment]
  if matches:remaining.append(dict(pid=int(process.name),name=(process/'comm').read_text().strip(),fixture_ids=matches))
 except OSError:pass
command=['docker','ps','--all','--format','{{.ID}}\t{{.Names}}']
r=subprocess.run(command,capture_output=True,text=True,check=True)
containers=[line for line in r.stdout.splitlines() if any(value in line for value in ids)]
aspire=subprocess.run(['aspire','ps','--format','Json'],capture_output=True,text=True,check=True)
start=aspire.stdout.index('[');applications=json.JSONDecoder().raw_decode(aspire.stdout[start:])[0]
applications=[{key:app.get(key) for key in ('appHostPath','appHostPid','status')} for app in applications]
row=dict(time=datetime.datetime.now(datetime.timezone.utc).isoformat(),fixture_ids=sorted(ids),same_uid_environments_checked=checked,proven_owned_remaining_processes=remaining,matching_containers=containers,running_cli_apphosts=applications,note='Read-only exact fixture-ID ownership probe; no environment values are exported and no unrelated process/container mutation was issued.')
(out/('cleanup-'+__import__('sys').argv[1]+'.json')).write_text(json.dumps(row,indent=2)+'\n');print(json.dumps(row))
assert not remaining and not containers
