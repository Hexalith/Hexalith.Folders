#!/usr/bin/env python3
"""Owned collector loss/deadline/cleanup checks; no NSS mutation workload."""
import json
import os
from pathlib import Path
import signal
import stat
import subprocess
import sys
import time

runtime, owner = Path(sys.argv[1]), int(sys.argv[2])
if (runtime.parent != Path('/var/tmp') or not runtime.name.startswith('story117-nss-retry.')
        or runtime.is_symlink() or runtime.stat().st_uid != 0 or stat.S_IMODE(runtime.stat().st_mode) != 0o700):
    raise SystemExit('Unsafe runtime')
collector = Path(__file__).resolve().parent.parent / 'collector.py'


def start(pid):
    text = Path('/proc', str(pid), 'stat').read_text()
    return int(text[text.rfind(')') + 2:].split()[19])


for name, duration, action, maximum in [('11-signal', 25, 'signal', 268435456),
        ('12-paused-deadline-loss', 15, 'pause', 268435456),
        ('13-setup-deadline', 1, 'deadline', 268435456), ('14-overflow', 5, 'overflow', 131072)]:
    budget = json.loads((runtime / 'budget.json').read_text())
    if budget['allocated_seconds'] + duration > 347 or any(a['name'] == name for a in budget['attempts']):
        raise SystemExit('Budget exceeded or attempt reused')
    budget['allocated_seconds'] += duration
    budget['attempts'].append({'name':name,'duration':duration})
    (runtime / 'budget.json').write_text(json.dumps(budget, indent=2)+'\n')
    attempt = runtime / name
    attempt.mkdir(mode=0o700)
    synthetic = runtime / ('synthetic-' + name)
    synthetic.mkdir(mode=0o700)
    root = synthetic / 'nssdb'
    root.mkdir(mode=0o700)
    log = (attempt / 'controller.log').open('x')
    child = subprocess.Popen(['/usr/bin/python3','-B',str(collector),'capture','--nss-dir',str(root),
        '--output',str(attempt/'capture'),'--owner-pid',str(owner),'--host-namespaces-confirmed',
        '--duration',str(duration),'--max-bytes',str(maximum)], stdout=log,stderr=subprocess.STDOUT)
    pidfd = os.pidfd_open(child.pid)
    record = {'collector_pid':child.pid,'start_ticks':start(child.pid),'owner_pid':owner,'owner_start_ticks':start(owner),
              'boot_id':Path('/proc/sys/kernel/random/boot_id').read_text().strip(),'duration_seconds':duration,
              'action':action,'workload_executed':False,'launched_monotonic_ns':time.monotonic_ns()}
    try:
        if action in ('signal','pause'):
            until = time.monotonic() + duration
            while child.poll() is None and time.monotonic() < until:
                if (attempt/'capture/trace-start.json').exists():
                    metadata=json.loads((attempt/'capture/metadata.json').read_text())
                    if start(child.pid) != record['start_ticks'] or start(owner) != record['owner_start_ticks']:
                        raise RuntimeError('Identity changed')
                    if action == 'signal':
                        signal.pidfd_send_signal(pidfd,signal.SIGHUP)
                        record['signal_sent_monotonic_ns']=time.monotonic_ns()
                        record['signal']=int(signal.SIGHUP)
                    else:
                        signal.pidfd_send_signal(pidfd,signal.SIGSTOP)
                        record['paused_monotonic_ns']=time.monotonic_ns()
                        until_boot=metadata['deadline_boottime_ns']+1_000_000_000
                        while time.clock_gettime_ns(time.CLOCK_BOOTTIME)<until_boot: time.sleep(0.05)
                        signal.pidfd_send_signal(pidfd,signal.SIGCONT)
                        record['resumed_monotonic_ns']=time.monotonic_ns()
                    break
                time.sleep(0.02)
        record['collector_exit']=child.wait(timeout=90)
    finally:
        if child.poll() is None:
            signal.pidfd_send_signal(pidfd,signal.SIGCONT)
            signal.pidfd_send_signal(pidfd,signal.SIGTERM)
            record['collector_exit']=child.wait(timeout=90)
        os.close(pidfd)
        log.close()
        record['finished_monotonic_ns']=time.monotonic_ns()
        with (attempt/'execution.json').open('x') as stream: json.dump(record,stream,indent=2);stream.write('\n')
        root.rmdir();synthetic.rmdir()
    print(json.dumps(record),flush=True)
