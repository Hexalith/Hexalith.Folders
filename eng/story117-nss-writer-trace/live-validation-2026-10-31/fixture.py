#!/usr/bin/env python3
"""Disposable synthetic operations; collector never launches this workload."""
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


def identity():
    text = Path('/proc/self/stat').read_text()
    return {'pid': os.getpid(), 'tid': threading.get_native_id(), 'uid': os.getuid(),
            'start_ticks': int(text[text.rfind(')') + 2:].split()[19]),
            'executable': os.readlink('/proc/self/exe')}


def write_new(path, value):
    with path.open('x') as stream:
        stream.write(json.dumps(value, indent=2) + '\n')


def main():
    runtime, owner = Path(sys.argv[1]), int(sys.argv[2])
    if (runtime.parent != Path('/var/tmp') or not runtime.name.startswith('story117-nss-live.')
            or runtime.is_symlink()):
        raise SystemExit('Refusing any path outside the disposable fixture')
    runtime_info = runtime.lstat()
    if runtime_info.st_uid != 0 or stat.S_IMODE(runtime_info.st_mode) != 0o700:
        raise SystemExit('Fixture runtime must be root-owned and private')
    root = runtime / 'synthetic/nssdb'
    output = runtime / 'captures/03-fixture'
    artifacts = ('fixture-before.json', 'fixture-handshake.json', 'fixture-outcomes.json',
                 'fixture-workload-completed.json', 'fixture-collector-identity.json', '03-controller.log')
    if output.exists() or any((runtime / name).exists() or (runtime / name).is_symlink() for name in artifacts):
        raise SystemExit('Refusing to reuse the fixture run or overwrite its evidence')
    outcomes = []
    def record(route, fn):
        before = time.monotonic_ns()
        try:
            result = fn()
            outcomes.append({'route': route, 'ok': True, 'result': result,
                             'identity': identity(), 'before_ns': before, 'after_ns': time.monotonic_ns()})
        except (OSError, subprocess.CalledProcessError) as exc:
            outcomes.append({'route': route, 'ok': False, 'error': str(exc), 'errno': getattr(exc, 'errno', None),
                             'identity': identity(), 'before_ns': before, 'after_ns': time.monotonic_ns()})

    runtime_fd = os.open(runtime, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    synthetic_fd = os.open('synthetic', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=runtime_fd)
    nss_fd = os.open('nssdb', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=synthetic_fd)
    for anchor in (runtime_fd, synthetic_fd, nss_fd):
        info = os.fstat(anchor)
        if info.st_uid != 0 or info.st_mode & 0o022:
            raise SystemExit('Fixture ancestor ownership/permissions refused')
    for leaf in os.listdir(nss_fd):
        info = os.stat(leaf, dir_fd=nss_fd, follow_symlinks=False)
        if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_nlink != 1:
            raise SystemExit('Fixture descendants must be root-owned single-link regular files')
    fd = os.open('cert9.db', os.O_RDWR | os.O_NOFOLLOW, dir_fd=nss_fd)
    info = os.fstat(fd)
    if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_nlink != 1:
        raise SystemExit('Fixture must be a newly created root-owned regular file with one link')
    os.close(nss_fd); os.close(synthetic_fd); os.close(runtime_fd)
    os.ftruncate(fd, 16384)
    mapping = mmap.mmap(fd, 16384, flags=mmap.MAP_SHARED, prot=mmap.PROT_READ | mmap.PROT_WRITE)
    mapping[0] = 65
    initial = {'identity': identity(), 'preexisting_fd': fd, 'inode': os.fstat(fd).st_ino,
               'device': {'major': os.major(os.fstat(fd).st_dev), 'minor': os.minor(os.fstat(fd).st_dev)},
               'preexisting_writable_shared_mapping': True, 'created_before_capture': True}
    write_new(runtime / 'fixture-before.json', initial)
    log = (runtime / '03-controller.log').open('x')
    collector = subprocess.Popen(['/usr/bin/python3', '-B', 'eng/story117-nss-writer-trace/collector.py',
        'capture', '--nss-dir', str(root), '--output', str(output), '--owner-pid', str(owner),
        '--host-namespaces-confirmed', '--duration', '180', '--max-bytes', '268435456'],
        stdout=log, stderr=subprocess.STDOUT)
    write_new(runtime / 'fixture-collector-identity.json', {'pid': collector.pid,
        'owner_pid': owner, 'boot_id': Path('/proc/sys/kernel/random/boot_id').read_text().strip()})
    ready = None
    until = time.monotonic() + 180
    while collector.poll() is None and time.monotonic() < until:
        try:
            ready = json.loads((output / 'ready.json').read_text())
            break
        except (FileNotFoundError, json.JSONDecodeError):
            time.sleep(0.1)
    write_new(runtime / 'fixture-handshake.json', {'ready': ready, 'workload_executed': False})
    valid = False
    if ready:
        proc = Path('/proc', str(collector.pid), 'stat').read_text()
        parent = Path('/proc', str(owner), 'stat').read_text()
        valid = (ready['pid'] == collector.pid and ready['caller_shell']['pid'] == owner
            and ready['caller_shell']['start_ticks'] == int(parent[parent.rfind(')') + 2:].split()[19])
            and ready['start_ticks'] == int(proc[proc.rfind(')') + 2:].split()[19])
            and ready['boot_id'] == Path('/proc/sys/kernel/random/boot_id').read_text().strip()
            and ready['mandatory_hooks_attached'] and ready['before_snapshot_complete']
            and time.monotonic_ns() < ready['deadline_monotonic_ns']
            and time.clock_gettime_ns(time.CLOCK_BOOTTIME) < ready['deadline_boottime_ns'])
    if valid:
        record('existing_fd_write', lambda: os.write(fd, b'synthetic'))
        record('pwrite', lambda: os.pwrite(fd, b'fixture', 32))
        record('writev', lambda: os.writev(fd, [b'a', b'b']))
        record('pwritev', lambda: os.pwritev(fd, [b'c', b'd'], 48))
        thread = threading.Thread(target=lambda: record('thread_existing_fd', lambda: os.pwrite(fd, b't', 64)))
        thread.start(); thread.join()
        record('already_writable_mmap_UNSUPPORTED', lambda: mapping.__setitem__(1, 66))
        mapping.close()
        record('ftruncate', lambda: os.ftruncate(fd, 8192))
        record('truncate', lambda: os.truncate(root / 'key4.db', 4096))
        record('O_TRUNC', lambda: os.close(os.open(root / 'key4.db', os.O_WRONLY | os.O_TRUNC)))
        record('create', lambda: (root / 'created').touch())
        record('mkdir', lambda: (root / 'directory').mkdir())
        record('rmdir', lambda: (root / 'directory').rmdir())
        record('mknod_fifo', lambda: os.mknod(root / 'fifo', stat.S_IFIFO | 0o600))
        record('rename', lambda: os.rename(root / 'created', root / 'renamed'))
        (root / 'replacement').touch()
        record('rename_replacement', lambda: os.replace(root / 'renamed', root / 'replacement'))
        (root / 'exchange').touch()
        libc = ctypes.CDLL(None, use_errno=True)
        def exchange():
            result = libc.renameat2(-100, os.fsencode(root / 'replacement'), -100, os.fsencode(root / 'exchange'), 2)
            if result: raise OSError(ctypes.get_errno(), 'renameat2')
            return result
        record('rename_exchange', exchange)
        record('hard_link', lambda: os.link(root / 'replacement', root / 'linked'))
        record('unlink', lambda: os.unlink(root / 'linked'))
        record('tmpfile', lambda: os.close(os.open(root, os.O_TMPFILE | os.O_RDWR, 0o600)))
        record('posix_fallocate', lambda: os.posix_fallocate(fd, 0, 32768))
        def fallocate(mode):
            result = libc.fallocate(fd, mode, ctypes.c_longlong(4096), ctypes.c_longlong(4096))
            if result: raise OSError(ctypes.get_errno(), 'fallocate')
            return result
        record('punch_hole', lambda: fallocate(3))
        record('zero_range', lambda: fallocate(16))
        destination = os.open(root / 'destination', os.O_CREAT | os.O_RDWR, 0o600)
        record('copy_file_range', lambda: os.copy_file_range(fd, destination, 512, 0, 0))
        record('sendfile', lambda: os.sendfile(destination, fd, 0, 512))
        read_pipe, write_pipe = os.pipe()
        os.write(write_pipe, b'synthetic splice')
        record('pipe_splice', lambda: os.splice(read_pipe, destination, 16))
        os.close(read_pipe); os.close(write_pipe); os.close(destination)
        os.chown(root / 'pkcs11.txt', 1000, 1000)
        os.chmod(runtime, 0o755); os.chmod(runtime / 'synthetic', 0o755); os.chmod(root, 0o755)
        def uid_child():
            os.setgroups([]); os.setgid(1000); os.setuid(1000)
        code = ("import os,json,pathlib,time; s=pathlib.Path('/proc/self/stat').read_text(); "
                "f=os.open(os.sys.argv[1],os.O_WRONLY|os.O_NOFOLLOW); n=os.write(f,b'synthetic uid'); os.close(f); "
                "print(json.dumps({'pid':os.getpid(),'tid':os.getpid(),'uid':os.getuid(),"
                "'start_ticks':int(s[s.rfind(')')+2:].split()[19]),'bytes':n,"
                "'executable':os.readlink('/proc/self/exe'),'monotonic_ns':time.monotonic_ns()}))")
        def run_uid_child():
            child = subprocess.Popen(['/usr/bin/python3', '-B', '-c', code, str(root / 'pkcs11.txt')],
                preexec_fn=uid_child, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            stdout, stderr = child.communicate(timeout=10)
            if child.returncode:
                raise OSError('UID child failed with exit ' + str(child.returncode))
            receipt = json.loads(stdout)
            if receipt['pid'] != child.pid or receipt['uid'] != 1000:
                raise OSError('UID child execution identity refused')
            return {'owned_child_pid': child.pid, 'exit': child.returncode, 'child_identity': receipt}
        record('uid1000_fork_exec', run_uid_child)
        write_new(runtime / 'fixture-workload-completed.json', {'workload_executed': True})
    else:
        mapping.close()
    os.close(fd)
    # A failed handshake never authorizes the mutation workload. Let the bounded
    # deadline expire so it also exercises setup/collection deadline handling.
    result = collector.wait(timeout=210)
    log.close()
    write_new(runtime / 'fixture-outcomes.json', {'executed': valid, 'operations': outcomes,
        'collector_exit': result, 'kernel_write_route': 'not generated: no fixture-scoped kernel-write trigger',
        'unsupported': ['already-writable mmap stores', 'async/io_uring', 'PMU dispatch omissions',
                        'suspend clock enforcement (no VM suspend performed)']})
    print(json.dumps({'executed': valid, 'operations': len(outcomes), 'collector_exit': result}))


if __name__ == '__main__':
    main()
