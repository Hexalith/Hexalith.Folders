import json,subprocess,sys
from pathlib import Path
p=Path(__file__).parent;name=sys.argv[1];plan=json.loads((p/'verification-plan.json').read_text());command=list(plan['strict-real-provider']);command[-1]=str((p/(name+'.xml')).resolve())
subprocess.run([sys.executable,str(p/'audit.py'),'before',name],check=True)
with (p/(name+'-monitor.raw.log')).open('w') as output:
 monitor=subprocess.Popen([sys.executable,str(p/'audit.py'),'monitor',name],stdout=output,stderr=subprocess.STDOUT)
 try:result=subprocess.run([sys.executable,str(p/'run.py'),name,*command])
 finally:
  try:monitor.wait(timeout=15)
  except subprocess.TimeoutExpired:monitor.terminate();monitor.wait()
subprocess.run([sys.executable,str(p/'audit.py'),'after',name],check=True)
sys.exit(result.returncode)
