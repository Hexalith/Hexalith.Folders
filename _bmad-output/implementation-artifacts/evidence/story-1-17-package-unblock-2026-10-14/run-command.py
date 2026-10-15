import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

root = Path('/home/administrator/projects/hexalith/folders')
evidence = root / '_bmad-output/implementation-artifacts/evidence/story-1-17-package-unblock-2026-10-14'
evidence.mkdir(parents=True, exist_ok=True)
check_id, cwd, *command = sys.argv[1:]
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
log = evidence / (check_id + '.log')
with log.open('wb') as stream:
    result = subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT)
record = {'id': check_id, 'command': command, 'working_directory': cwd,
          'started_at_utc': started, 'finished_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'exit_code': result.returncode, 'log': str(log.relative_to(root)),
          'log_sha256': hashlib.sha256(log.read_bytes()).hexdigest()}
(evidence / (check_id + '.command.json')).write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2))
print(log.read_text(errors='replace')[-6500:])
sys.exit(result.returncode)
