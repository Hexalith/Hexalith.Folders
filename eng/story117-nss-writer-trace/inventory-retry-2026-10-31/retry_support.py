"""Local helpers for the bounded diagnostic retry, never application code."""
import fcntl
import json
import os
from pathlib import Path
import shutil
import signal
import stat
import tempfile


def validate_runtime(runtime):
    if (runtime.parent != Path('/var/tmp') or not runtime.name.startswith('story117-nss-retry.')
            or runtime.is_symlink()):
        raise ValueError('Unsafe runtime path')
    info = runtime.stat()
    if info.st_uid != 0 or stat.S_IMODE(info.st_mode) != 0o700:
        raise ValueError('Runtime must be root-owned and private')


def reserve(runtime, name, duration):
    if '/' in name or not name or not 1 <= duration <= 347:
        raise ValueError('Unsafe attempt or allocation')
    fd = os.open(runtime / 'budget.lock', os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX)
        budget = json.loads((runtime / 'budget.json').read_text())
        if budget['allocated_seconds'] + duration > 347 or any(a['name'] == name for a in budget['attempts']):
            raise ValueError('Capture budget exceeded or attempt reused')
        budget['allocated_seconds'] += duration
        budget['attempts'].append({'name': name, 'duration': duration})
        pending = None
        try:
            with tempfile.NamedTemporaryFile(mode='w', dir=runtime, prefix='budget-pending-', delete=False) as stream:
                pending = Path(stream.name)
                json.dump(budget, stream, indent=2)
                stream.write('\n')
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(pending, runtime / 'budget.json')
            directory = os.open(runtime, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
            try: os.fsync(directory)
            finally: os.close(directory)
        finally:
            if pending is not None and pending.exists(): pending.unlink()
    finally:
        os.close(fd)


def stop_owned(child):
    if child.poll() is None:
        # Popen owns this unreaped child; its methods also check the child state.
        child.send_signal(signal.SIGCONT)
        child.terminate()
        child.wait(timeout=90)


def remove_owned_synthetic(path):
    if path.exists():
        shutil.rmtree(path)
