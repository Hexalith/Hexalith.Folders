"""Run the approved strict body on the audited producer, with private lifecycle evidence."""
from pathlib import Path
import datetime,json,os,subprocess
W=Path(__file__).parent;label='strict-foreign-scope-4'
summary=json.loads((W/'scope-final-audit-summary.json').read_text());assert summary['frozen_unchanged'] and all(summary['bodies_match_approved_expectations'].values())
env={'FOLDERS_TASK36_SHUTDOWN_LOG':str(W/(label+'-lifecycle-private.jsonl')),
'Logging__LogLevel__Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogAuthority':'Debug',
'Logging__LogLevel__Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler':'Debug',
'Logging__LogLevel__Hexalith.EventStore.ServiceDefaults.Routing.TechnicalHttpRouteCatalogReceiver':'Debug',
'Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics':'Information'}
for name,data in [(label+'-extra-environment.json',{'requested_parent_environment':env,'approval':'2026-11-04 exact foreign-task scope patch; all production proof and deadline checks retained'}),(label+'-host-context-before.json',{'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'availableCpuAffinity':len(os.sched_getaffinity(0)),**{k:Path(k).read_text().strip() for k in ['/proc/loadavg','/sys/fs/cgroup/cpu.max','/sys/fs/cgroup/cpu.stat','/sys/fs/cgroup/cpu.pressure'] if Path(k).exists()}})]:
 p=W/name
 with p.open('x') as f:json.dump(data,f,indent=2)
 p.chmod(0o600)
subprocess.run(['python3',str(W/'source_patch.py'),label],check=True)
subprocess.run(['python3',str(W/'qualify.py'),'run','--port','36037',label,'--','dotnet','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll','-class','Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests','-showLiveOutput','-result-xml',str(W/(label+'-private.xml'))],env={**os.environ,**env},check=True)
