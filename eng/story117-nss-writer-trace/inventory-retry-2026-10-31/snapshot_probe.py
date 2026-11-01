#!/usr/bin/env python3
"""Read-only host metadata probe, no BCC import/attachment or fixture mutation."""
import collections
import contextlib
import json
from pathlib import Path
import sys
import time
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import collector as c
from retry_support import validate_runtime, remove_owned_synthetic

runtime = Path(sys.argv[1])
validate_runtime(runtime)
state = {}
with contextlib.ExitStack() as cleanup:
    synthetic = runtime / 'snapshot-synthetic'
    synthetic.mkdir(mode=0o700)
    cleanup.callback(remove_owned_synthetic, synthetic)
    root = synthetic / 'nssdb'
    root.mkdir(mode=0o700)
    output = c.BoundedOutput(runtime / 'snapshot-only', c.MAX_BYTES)
    cleanup.callback(output.close)
    started = time.monotonic_ns()
    try:
        c.snapshot_all(output, 'before', root, state=state)
    except Exception as exc:
        state['exception'] = type(exc).__name__ + ': ' + str(exc)
    finally:
        state['elapsed_seconds'] = (time.monotonic_ns() - started) / 1e9
        state['bytes'] = output.bytes
        state['tracing_imported'] = 'bcc' in sys.modules
        kinds = collections.Counter()
        stream = runtime / 'snapshot-only/before.jsonl'
        if stream.exists():
            with stream.open() as rows:
                for line in rows: kinds[json.loads(line)['kind']] += 1
        state['record_counts'] = kinds
        with (runtime / 'snapshot-only-result.json').open('x') as stream:
            json.dump(state, stream, indent=2)
            stream.write('\n')
print(json.dumps(state, indent=2))
raise SystemExit(0 if state.get('completed') and not state.get('error_count') and not state.get('exception') else 2)
