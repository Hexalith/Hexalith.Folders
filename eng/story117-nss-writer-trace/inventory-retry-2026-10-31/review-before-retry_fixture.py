#!/usr/bin/env python3
"""Bounded synthetic-only retry. Never mutates after an invalid handshake."""
import ctypes
import json
import mmap
import os
from pathlib import Path
import signal
import stat
import subprocess
import sys
import threading
import time

HERE = Path(__file__).resolve().parent
COLLECTOR = HERE.parent / 'collector.py'


def write_new(path, value):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'w') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def start(pid):
    text = Path('/proc', str(pid), 'stat').read_text()
    return int(text[text.rfind(')') + 2:].split()[19])


def identity():
    info = os.stat('/proc/self/exe')
    return {'pid': os.getpid(), 'tid': threading.get_native_id(), 'uid': os.getuid(),
            'start_ticks': start(os.getpid()), 'executable': os.readlink('/proc/self/exe'),
            'executable_device': [os.major(info.st_dev), os.minor(info.st_dev)], 'executable_inode': info.st_ino}


def meta(path):
    info = os.stat(path, follow_symlinks=False)
    return {'path': str(path), 'device': [os.major(info.st_dev), os.minor(info.st_dev)], 'inode': info.st_ino}


def main():
    runtime, owner, name, duration = Path(sys.argv[1]), int(sys.argv[2]), sys.argv[3], int(sys.argv[4])
    if (runtime.parent != Path('/var/tmp') or not runtime.name.startswith('story117-nss-retry.')
            or runtime.is_symlink() or runtime.stat().st_uid != 0 or stat.S_IMODE(runtime.stat().st_mode) != 0o700
            or '/' in name or not 1 <= duration <= 347):
        raise SystemExit('Unsafe fixture runtime or capture allocation')
    # Allocate conservatively before starting BCC, including setup on failure.
    ledger = runtime / 'budget.json'
    budget = json.loads(ledger.read_text())
    if budget['allocated_seconds'] + duration > 347 or any(a['name'] == name for a in budget['attempts']):
        raise SystemExit('Capture budget exceeded or attempt reused')
    budget['allocated_seconds'] += duration
    budget['attempts'].append({'name': name, 'duration': duration})
    # Only this root-private, newly created fixture ledger is replaced.
    ledger.write_text(json.dumps(budget, indent=2) + '\n')
    attempt = runtime / name
    attempt.mkdir(mode=0o700)
    synthetic = runtime / ('synthetic-' + name)
    synthetic.mkdir(mode=0o700)
    root = synthetic / 'nssdb'
    root.mkdir(mode=0o700)
    for leaf in ['cert9.db', 'key4.db', 'pkcs11.txt']:
        f = os.open(root / leaf, os.O_CREAT | os.O_EXCL | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        os.ftruncate(f, 32768)
        os.close(f)
    fd = os.open(root / 'cert9.db', os.O_RDWR | os.O_NOFOLLOW)
    mapping = mmap.mmap(fd, 32768, flags=mmap.MAP_SHARED, prot=mmap.PROT_READ | mmap.PROT_WRITE)
    ready = None
    outcomes, thread_state = [], {}
    go, prepared = threading.Event(), threading.Event()
    def require_active():
        if (ready is None or not ready['before_snapshot_complete'] or
                time.monotonic_ns() >= ready['deadline_monotonic_ns'] or
                time.clock_gettime_ns(time.CLOCK_BOOTTIME) >= ready['deadline_boottime_ns']):
            raise RuntimeError('No active readiness authorization for fixture operation')
    def record(route, fn, paths=()):
        require_active()
        row = {'route': route, 'identity': identity(), 'before_ns': time.monotonic_ns(),
               'before_paths': [meta(p) for p in paths]}
        try:
            row.update(ok=True, result=fn())
        except (OSError, subprocess.SubprocessError) as exc:
            row.update(ok=False, error=str(exc), errno=getattr(exc, 'errno', None))
        row.update(after_ns=time.monotonic_ns(), after_paths=[meta(p) for p in paths if p.exists()])
        outcomes.append(row)
    libc = ctypes.CDLL(None, use_errno=True)
    def private_thread():
        private = None
        try:
            if libc.unshare(0x400) != 0:
                raise OSError(ctypes.get_errno(), 'unshare(CLONE_FILES)')
            private = os.open(root / 'key4.db', os.O_RDWR | os.O_NOFOLLOW)
            thread_state.update(identity=identity(), fd=private, file=meta(root / 'key4.db'), unshared=True)
        except OSError as exc:
            thread_state.update(error=str(exc), unshared=False)
        prepared.set()
        go.wait()
        try:
            if thread_state.get('authorized') and private is not None:
                record('thread_private_preexisting_fd', lambda: os.pwrite(private, b'private', 16))
        finally:
            if private is not None:
                os.close(private)
    worker = threading.Thread(target=private_thread)
    worker.start()
    if not prepared.wait(10):
        raise RuntimeError('Private descriptor preparation did not finish')
    write_new(attempt / 'fixture-before.json', {'identity': identity(), 'preexisting_fd': fd,
        'file': meta(root / 'cert9.db'), 'mapping_preexisting': True, 'mapping_stores_before_handshake': 0,
        'thread_private': dict(thread_state), 'owner_pid': owner, 'owner_start_ticks': start(owner)})
    collector = None
    pidfd = None
    valid = False
    execution = {'duration_seconds': duration, 'workload_executed': False, 'outcomes': outcomes}
    log = (attempt / 'controller.log').open('x')
    try:
        launched = time.monotonic_ns()
        collector = subprocess.Popen(['/usr/bin/python3', '-B', str(COLLECTOR), 'capture',
            '--nss-dir', str(root), '--output', str(attempt / 'capture'), '--owner-pid', str(owner),
            '--host-namespaces-confirmed', '--duration', str(duration), '--max-bytes', '268435456'],
            stdout=log, stderr=subprocess.STDOUT)
        pidfd = os.pidfd_open(collector.pid)
        execution.update(collector_pid=collector.pid, collector_start_ticks=start(collector.pid),
                         boot_id=Path('/proc/sys/kernel/random/boot_id').read_text().strip(), launched_monotonic_ns=launched)
        ready = None
        reason = 'collector exited without readiness'
        while collector.poll() is None and time.monotonic_ns() - launched < duration * 1_000_000_000:
            if (attempt / 'capture/receipt.json').exists():
                reason = 'receipt observed before handshake'
                break
            try:
                ready = json.loads((attempt / 'capture/ready.json').read_text())
            except (FileNotFoundError, json.JSONDecodeError):
                time.sleep(0.02)
                continue
            valid = (ready['pid'] == collector.pid and ready['start_ticks'] == start(collector.pid)
                and ready['caller_shell']['pid'] == owner and ready['caller_shell']['start_ticks'] == start(owner)
                and ready['boot_id'] == execution['boot_id'] and ready['mandatory_hooks_attached']
                and ready['before_snapshot_complete'] and time.monotonic_ns() < ready['deadline_monotonic_ns']
                and time.clock_gettime_ns(time.CLOCK_BOOTTIME) < ready['deadline_boottime_ns'])
            reason = 'valid' if valid else 'readiness/ownership/deadline refused'
            break
        write_new(attempt / 'handshake.json', {'valid': valid, 'reason': reason, 'ready': ready,
            'checked_monotonic_ns': time.monotonic_ns(), 'workload_executed_at_check': False})
        if valid:
            execution['workload_executed'] = True
            record('existing_fd_write', lambda: os.write(fd, b'synthetic'))
            record('pwrite', lambda: os.pwrite(fd, b'synthetic', 32))
            record('writev', lambda: os.writev(fd, [b'a', b'b']))
            record('pwritev', lambda: os.pwritev(fd, [b'c', b'd'], 48))
            thread_state['authorized'] = True
            go.set()
            worker.join(timeout=10)
            if worker.is_alive():
                raise RuntimeError('Private thread did not finish')
            record('mmap_first_write_FAULT_EVIDENCE_ONLY', lambda: mapping.__setitem__(0, 65))
            record('already_writable_mmap_UNSUPPORTED', lambda: mapping.__setitem__(1, 66))
            mapping.close()
            record('ftruncate', lambda: os.ftruncate(fd, 8192))
            record('truncate', lambda: os.truncate(root / 'key4.db', 4096))
            record('O_TRUNC', lambda: os.close(os.open(root / 'key4.db', os.O_WRONLY | os.O_TRUNC | os.O_NOFOLLOW)))
            record('create', lambda: (root / 'created').touch(exist_ok=False))
            record('mkdir', lambda: (root / 'directory').mkdir())
            record('rmdir', lambda: (root / 'directory').rmdir())
            record('mknod_fifo', lambda: os.mknod(root / 'fifo', stat.S_IFIFO | 0o600))
            record('rename', lambda: os.rename(root / 'created', root / 'renamed'), [root / 'created'])
            require_active()
            (root / 'replacement').touch(exist_ok=False)
            record('rename_replacement', lambda: os.replace(root / 'renamed', root / 'replacement'), [root / 'renamed', root / 'replacement'])
            require_active()
            (root / 'exchange').touch(exist_ok=False)
            def exchange():
                if libc.renameat2(-100, os.fsencode(root / 'replacement'), -100, os.fsencode(root / 'exchange'), 2):
                    raise OSError(ctypes.get_errno(), 'renameat2')
                return 0
            record('rename_exchange', exchange, [root / 'replacement', root / 'exchange'])
            record('hard_link', lambda: os.link(root / 'replacement', root / 'linked'), [root / 'replacement'])
            record('unlink', lambda: os.unlink(root / 'linked'), [root / 'linked'])
            record('tmpfile', lambda: os.close(os.open(root, os.O_TMPFILE | os.O_RDWR, 0o600)))
            record('posix_fallocate', lambda: os.posix_fallocate(fd, 0, 32768))
            def allocate(mode):
                if libc.fallocate(fd, mode, ctypes.c_longlong(4096), ctypes.c_longlong(4096)):
                    raise OSError(ctypes.get_errno(), 'fallocate')
                return 0
            record('punch_hole', lambda: allocate(3))
            record('zero_range', lambda: allocate(16))
            destination = os.open(root / 'destination', os.O_CREAT | os.O_EXCL | os.O_RDWR | os.O_NOFOLLOW, 0o600)
            try:
                record('copy_file_range', lambda: os.copy_file_range(fd, destination, 512, 0, 0))
                record('sendfile', lambda: os.sendfile(destination, fd, 0, 512))
                rd, wr = os.pipe()
                try:
                    os.write(wr, b'synthetic splice')
                    record('pipe_splice', lambda: os.splice(rd, destination, 16))
                finally:
                    os.close(rd); os.close(wr)
            finally:
                os.close(destination)
            require_active()
            os.chown(root / 'pkcs11.txt', 1000, 1000)
            for p in [runtime, synthetic, root]: os.chmod(p, 0o755)
            def change_uid():
                os.setgroups([]); os.setgid(1000); os.setuid(1000)
            code = "import os,json,pathlib,time; s=pathlib.Path('/proc/self/stat').read_text(); f=os.open(os.sys.argv[1],os.O_WRONLY|os.O_NOFOLLOW); n=os.write(f,b'synthetic uid'); os.close(f); print(json.dumps({'pid':os.getpid(),'tid':os.getpid(),'uid':os.getuid(),'start_ticks':int(s[s.rfind(')')+2:].split()[19]),'executable':os.readlink('/proc/self/exe'),'bytes':n,'monotonic_ns':time.monotonic_ns()}))"
            def uid_child():
                child = subprocess.Popen(['/usr/bin/python3','-B','-c',code,str(root/'pkcs11.txt')], preexec_fn=change_uid,
                                         stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
                try:
                    stdout, stderr = child.communicate(timeout=10)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.communicate()
                    raise
                if child.returncode: raise OSError('UID child failed: '+str(child.returncode))
                receipt = json.loads(stdout)
                if receipt['pid'] != child.pid or receipt['uid'] != 1000: raise OSError('Owned child mismatch')
                return {'owned_child_pid':child.pid,'exit':child.returncode,'identity':receipt}
            try: record('uid1000_fork_exec', uid_child)
            finally: os.chmod(runtime, 0o700)
            execution['workload_finished_monotonic_ns'] = time.monotonic_ns()
        # End this owned collector early with SIGINT, even for failed handshake.
        if collector.poll() is None and start(collector.pid) == execution['collector_start_ticks']:
            signal.pidfd_send_signal(pidfd, signal.SIGINT)
            execution['stop_signal'] = int(signal.SIGINT)
        execution['collector_exit'] = collector.wait(timeout=90)
    finally:
        go.set()
        worker.join(timeout=10)
        if not mapping.closed: mapping.close()
        os.close(fd)
        os.chmod(runtime, 0o700)
        if collector is not None and collector.poll() is None:
            signal.pidfd_send_signal(pidfd, signal.SIGCONT)
            signal.pidfd_send_signal(pidfd, signal.SIGTERM)
            execution['collector_exit'] = collector.wait(timeout=90)
        if pidfd is not None: os.close(pidfd)
        log.close()
        execution.update(finished_monotonic_ns=time.monotonic_ns(), thread_private=dict(thread_state),
            unsupported=['already-writable MAP_SHARED stores','page-mkwrite gives no content credit','async/io_uring',
                         '__kernel_write fixture trigger unavailable','generic filemap fault not guaranteed',
                         'PMU entry dispatch/recursion and paired omissions','actual VM suspend untested'])
        write_new(attempt/'execution.json',execution)
        # The only deletions are within this helper's exclusively created synthetic tree.
        removed=[]
        for entry in root.iterdir():
            if entry.is_dir() and not entry.is_symlink(): entry.rmdir()
            else: entry.unlink()
            removed.append(entry.name)
        root.rmdir(); synthetic.rmdir()
        write_new(attempt/'fixture-cleanup.json',{'removed_synthetic_entries':removed,'synthetic_root_absent':not synthetic.exists()})
    print(json.dumps({'name':name,'valid_handshake':valid,'operations':len(outcomes),'collector_exit':execution.get('collector_exit')}))


if __name__ == '__main__':
    main()
