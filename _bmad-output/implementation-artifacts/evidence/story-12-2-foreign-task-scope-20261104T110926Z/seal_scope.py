"""Validate a new evidence packet before its one-time repository copy."""
from pathlib import Path
import datetime,hashlib,json,shutil,subprocess,sys
W=Path(__file__).parent
destination=Path(sys.argv[1]).resolve()
staging=W/'export-scope-final'
assert not destination.exists() and not staging.exists()
for script,args in [('export_scope.py',[str(staging)]),('write_packet_readme.py',[str(staging)])]:
 subprocess.run(['python3',str(W/script),*args],check=True)
shutil.copy2(W/'seal_scope.py',staging/'seal_scope.py')
subprocess.run(['python3',str(W/'privacy_scan.py'),str(staging)],check=True)
files={p.relative_to(staging).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(staging.rglob('*')) if p.is_file()}
(staging/'seal.json').write_text(json.dumps({'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Exact approved foreign-task scope/source qualification; independent open gates retained','files':files},indent=2)+'\n')
subprocess.run(['python3',str(staging/'verify_scope.py')],check=True)
shutil.copytree(staging,destination)
subprocess.run(['python3',str(destination/'verify_scope.py')],check=True)
seal_hash=hashlib.sha256((destination/'seal.json').read_bytes()).hexdigest()
receipt={'repository_packet':str(destination),'sealed_files':len(files),'seal_sha256':seal_hash,'private_staging_verifier_passed':True,'repository_verifier_passed':True,'raw_logs_or_xml_exported':False}
(W/'packet-copy-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt))
