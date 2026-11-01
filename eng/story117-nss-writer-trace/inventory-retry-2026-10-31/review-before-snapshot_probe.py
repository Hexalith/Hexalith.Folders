#!/usr/bin/env python3
"""Read-only host metadata probe, no BCC import/attachment or file mutations."""
import collections
import json
from pathlib import Path
import sys
import time
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import collector as c

runtime = Path(sys.argv[1])
root = runtime / 'snapshot-synthetic' / 'nssdb'
root.mkdir(mode=0o700, parents=True)
output = c.BoundedOutput(runtime / 'snapshot-only', c.MAX_BYTES)
state = {}
started = time.monotonic_ns()
try:
    c.snapshot_all(output, 'before', root, state=state)
except Exception as exc:
    state['exception'] = type(exc).__name__ + ': ' + str(exc)
finally:
    output.close()
    state['elapsed_seconds'] = (time.monotonic_ns() - started) / 1e9
    state['bytes'] = output.bytes
    state['tracing_imported'] = 'bcc' in sys.modules
    kinds = collections.Counter()
    for line in (runtime / 'snapshot-only/before.jsonl').open():
        kinds[json.loads(line)['kind']] += 1
    state['record_counts'] = kinds
    with (runtime / 'snapshot-only-result.json').open('x') as stream:
        json.dump(state, stream, indent=2)
        stream.write('\n')
    root.rmdir()
    root.parent.rmdir()
print(json.dumps(state, indent=2))
