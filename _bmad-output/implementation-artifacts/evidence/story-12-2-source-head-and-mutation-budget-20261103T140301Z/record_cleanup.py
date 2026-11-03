"""Attribute exact runtime-owned cleanup without touching separate topologies."""
from pathlib import Path
import json,re,sys,datetime,os
root=Path(__file__).parent;label=sys.argv[1];assert re.fullmatch(r"[a-z0-9-]+",label)
raw=(root/(label+"-private.log")).read_text(errors="replace");receipt=json.loads((root/(label+"-receipt.json")).read_text())
paths=sorted(set(re.findall(r"/tmp/hexalith-dapr/[a-f0-9]{32}",raw)))
result={"runtimeOwnedProcessRemainders":receipt["cleanup"]["remaining_observed_descendants"],"ownedContainerRemainders":receipt["cleanup"]["remaining_owned_containers"],"ownedDaprDirectories":[{"path":p,"exists":Path(p).exists()} for p in paths],"unattributedNewContainerCount":len(receipt["cleanup"].get("remaining_unattributed_containers",[])),"unattributedNewDirectories":[p for p in receipt["cleanup"]["remaining_observed_directories"] if p not in paths],"actionOnUnattributedResources":"None"}
p=root/(label+"-owned-cleanup-audit.json");p.write_text(json.dumps(result,indent=2)+"\n");p.chmod(0o600)
names=["/proc/loadavg","/sys/fs/cgroup/cpu.max","/sys/fs/cgroup/cpu.stat","/sys/fs/cgroup/cpu.pressure"];p=root/(label+"-host-context-after.json");p.write_text(json.dumps({"utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),"availableCpuAffinity":len(os.sched_getaffinity(0)),**{k:Path(k).read_text().strip() for k in names if Path(k).exists()}},indent=2)+"\n");p.chmod(0o600)
print(json.dumps(result))
