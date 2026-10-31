#!/usr/bin/env python3
"""Bounded controller checks. Never launches a file-mutation workload."""
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def start_ticks(pid):
    text = Path('/proc', str(pid), 'stat').read_text()
    return int(text[text.rfind(')') + 2:].split()[19])


runtime, owner = Path(sys.argv[1]), int(sys.argv[2])
if runtime.parent != Path('/var/tmp') or not runtime.name.startswith('story117-nss-live.'):
    raise SystemExit('Unsafe runtime path')
for name, duration, action in (('05-signal', 60, 'signal'), ('06-paused-deadline-loss', 12, 'pause'),
                               ('07-setup-deadline', 1, 'deadline')):
    output = runtime / 'captures' / name
    execution_path = runtime / (name + '-execution.json')
    if output.exists() or execution_path.exists() or execution_path.is_symlink():
        raise SystemExit('Refusing to reuse an attempt or overwrite evidence')
    log = (runtime / (name + '-controller.log')).open('x')
    child = subprocess.Popen(['/usr/bin/python3', '-B', 'eng/story117-nss-writer-trace/collector.py',
        'capture', '--nss-dir', str(runtime / 'synthetic/nssdb'), '--output', str(output),
        '--owner-pid', str(owner), '--host-namespaces-confirmed', '--duration', str(duration),
        '--max-bytes', '33554432'], stdout=log, stderr=subprocess.STDOUT)
    pidfd = os.pidfd_open(child.pid)
    receipt = {'owned_collector_pid': child.pid, 'start_ticks': start_ticks(child.pid),
        'boot_id': Path('/proc/sys/kernel/random/boot_id').read_text().strip(),
        'owner_pid': owner, 'owner_start_ticks': start_ticks(owner), 'duration_seconds': duration,
        'action': action, 'launched_monotonic_ns': time.monotonic_ns(), 'workload_executed': False}
    try:
        if action != 'deadline':
            until = time.monotonic() + min(duration, 45)
            while child.poll() is None and time.monotonic() < until:
                if (output / 'trace-start.json').exists():
                    metadata = json.loads((output / 'metadata.json').read_text())
                    if (start_ticks(child.pid) != receipt['start_ticks'] or
                            start_ticks(owner) != receipt['owner_start_ticks']):
                        raise RuntimeError('Collector/owner identity changed')
                    receipt['trace_start_observed_monotonic_ns'] = time.monotonic_ns()
                    if action == 'signal':
                        signal.pidfd_send_signal(pidfd, signal.SIGTERM)
                        receipt['signal_sent_monotonic_ns'] = time.monotonic_ns()
                    else:
                        signal.pidfd_send_signal(pidfd, signal.SIGSTOP)
                        receipt['paused_monotonic_ns'] = time.monotonic_ns()
                        until_boot = metadata['deadline_boottime_ns'] + 1_000_000_000
                        while time.clock_gettime_ns(time.CLOCK_BOOTTIME) < until_boot:
                            time.sleep(0.05)
                        signal.pidfd_send_signal(pidfd, signal.SIGCONT)
                        receipt['resumed_monotonic_ns'] = time.monotonic_ns()
                    break
                time.sleep(0.02)
        receipt['collector_exit'] = child.wait(timeout=90)
    finally:
        if child.poll() is None:
            signal.pidfd_send_signal(pidfd, signal.SIGCONT)
            signal.pidfd_send_signal(pidfd, signal.SIGTERM)
            receipt['collector_exit'] = child.wait(timeout=90)
        os.close(pidfd)
        log.close()
        receipt['finished_monotonic_ns'] = time.monotonic_ns()
        with execution_path.open('x') as stream:
            stream.write(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt), flush=True)
