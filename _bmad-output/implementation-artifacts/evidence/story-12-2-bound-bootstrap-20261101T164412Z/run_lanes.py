import json,subprocess,sys
from pathlib import Path
p=Path(__file__).parent;plan=json.loads((p/'verification-plan.json').read_text())
for name in sys.argv[1:]:
 result=subprocess.run([sys.executable,str(p/'run.py'),name,*plan[name]])
 if result.returncode and name.endswith('-build'):
  print('BUILD BLOCKED: '+name,flush=True);sys.exit(result.returncode)
