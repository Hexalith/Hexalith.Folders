"""Offline orchestration checks. BCC and the mutation suite never run."""
import contextlib
import copy
import io
import json
import os
from pathlib import Path
import runpy
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock
import retry_fixture as fixture
import retry_support as support


class RetryFixtureTests(unittest.TestCase):
    def runtime(self):
        temp = tempfile.TemporaryDirectory(prefix='story117-nss-retry.offline-', dir='/var/tmp')
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        (root/'budget.json').write_text(json.dumps({'allocated_seconds':0,'attempts':[]}))
        return root

    def ready(self, owner=12345, pid=424242):
        return {'pid':pid,'start_ticks':pid+100,'caller_shell':{'pid':owner,'start_ticks':owner+100},
                'boot_id':Path('/proc/sys/kernel/random/boot_id').read_text().strip(),
                'mandatory_hooks_attached':True,'before_snapshot_complete':True,
                'deadline_monotonic_ns':time.monotonic_ns()+60_000_000_000,
                'deadline_boottime_ns':time.clock_gettime_ns(time.CLOCK_BOOTTIME)+60_000_000_000}

    def invoke(self, root, ready, *, setup_failure=False, pidfd_failure=False):
        children=[]
        self.children=children
        class Child:
            pid=424242
            returncode=None
            terminated=False
            def __init__(self, args, **kwargs):
                children.append(self)
                output=Path(args[args.index('--output')+1]);output.mkdir()
                (output/'ready.json').write_text(json.dumps(ready))
            def poll(self): return self.returncode
            def wait(self, **kwargs): self.returncode=2;return 2
            def send_signal(self, number): pass
            def terminate(self): self.terminated=True
        original_write=fixture.write_new
        def write(path,value):
            if setup_failure and path.name=='fixture-before.json': raise OSError('injected setup failure')
            original_write(path,value)
        class Lib:
            def unshare(self, flags): return -1
        with contextlib.ExitStack() as stack:
            stack.enter_context(mock.patch.object(sys,'argv',['retry_fixture.py',str(root),'12345','08-fixture','60']))
            stack.enter_context(mock.patch.object(fixture,'validate_runtime'))
            stack.enter_context(mock.patch.object(fixture,'identity',return_value={'pid':1,'tid':1,'start_ticks':1,'task_start_ticks':1}))
            stack.enter_context(mock.patch.object(fixture,'start',side_effect=lambda pid:pid+100))
            stack.enter_context(mock.patch.object(fixture,'write_new',side_effect=write))
            popen=stack.enter_context(mock.patch.object(fixture.subprocess,'Popen',side_effect=Child))
            stack.enter_context(mock.patch.object(fixture.ctypes,'CDLL',return_value=Lib()))
            stack.enter_context(mock.patch.object(fixture.os,'pidfd_open',side_effect=OSError('pidfd failure') if pidfd_failure else lambda pid:os.open('/dev/null',os.O_RDONLY)))
            stack.enter_context(mock.patch.object(fixture.signal,'pidfd_send_signal'))
            guards=[stack.enter_context(mock.patch.object(fixture.os,name,side_effect=AssertionError('mutation before valid handshake'))) for name in ('write','pwrite','writev','pwritev')]
            stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            with contextlib.ExitStack() as cleanup:
                fixture.run(cleanup)
            for guard in guards: guard.assert_not_called()
        return children,popen

    def test_invalid_handshake_runs_zero_mutation_routes(self):
        changes=[('pid',lambda r:r.update(pid=7)),('start',lambda r:r.update(start_ticks=7)),
                 ('owner',lambda r:r['caller_shell'].update(pid=7)),('owner_start',lambda r:r['caller_shell'].update(start_ticks=7)),
                 ('boot',lambda r:r.update(boot_id='wrong')),('hooks',lambda r:r.update(mandatory_hooks_attached=False)),
                 ('snapshot',lambda r:r.update(before_snapshot_complete=False)),
                 ('monotonic_deadline',lambda r:r.update(deadline_monotonic_ns=0)),
                 ('boottime_deadline',lambda r:r.update(deadline_boottime_ns=0))]
        for name,change in changes:
            with self.subTest(name=name):
                root=self.runtime();ready=self.ready();change(ready)
                self.invoke(root,ready)
                result=json.loads((root/'08-fixture/execution.json').read_text())
                self.assertFalse(result['workload_executed'])
                self.assertEqual(result['outcomes'],[])
                self.assertFalse(json.loads((root/'08-fixture/handshake.json').read_text())['valid'])
                self.assertFalse((root/'synthetic-08-fixture').exists())

    def test_early_setup_failure_releases_worker_and_removes_owned_fixture(self):
        root=self.runtime();before={t.ident for t in threading.enumerate()}
        with self.assertRaisesRegex(OSError,'injected setup failure'):
            self.invoke(root,self.ready(),setup_failure=True)
        self.assertEqual(before,{t.ident for t in threading.enumerate()})
        self.assertFalse((root/'synthetic-08-fixture').exists())
        self.assertFalse((root/'08-fixture/capture').exists())

    def test_pidfd_failure_stops_owned_child_and_removes_fixture(self):
        root=self.runtime()
        with self.assertRaisesRegex(OSError,'pidfd failure'):
            self.invoke(root,self.ready(),pidfd_failure=True)
        result=json.loads((root/'08-fixture/execution.json').read_text())
        self.assertFalse(result['workload_executed'])
        self.assertEqual(result['collector_exit'],2)
        self.assertTrue(self.children[0].terminated)
        self.assertFalse((root/'synthetic-08-fixture').exists())

    def test_revalidation_refuses_exited_collector_and_final_receipt(self):
        root=self.runtime();attempt=root/'08-fixture';(attempt/'capture').mkdir(parents=True)
        ready=self.ready();child=mock.Mock(pid=424242);child.poll.return_value=None
        execution={'collector_start_ticks':424342,'owner_start_ticks':12445,'boot_id':ready['boot_id']}
        with mock.patch.object(fixture,'start',side_effect=lambda pid:pid+100):
            self.assertTrue(fixture.readiness_valid(ready,child,12345,execution,attempt))
            child.poll.return_value=2
            self.assertFalse(fixture.readiness_valid(ready,child,12345,execution,attempt))
            child.poll.return_value=None;(attempt/'capture/receipt.json').write_text('{}')
            self.assertFalse(fixture.readiness_valid(ready,child,12345,execution,attempt))
            (attempt/'capture/receipt.json').unlink();(attempt/'capture/after.jsonl').touch()
            self.assertFalse(fixture.readiness_valid(ready,child,12345,execution,attempt))

    def test_successful_snapshot_closes_output_once(self):
        sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
        import collector as c
        root=self.runtime()
        def snapshot(output,phase,nss,state):
            state.update(completed=True,error_count=0)
            output.write('before.jsonl',{'kind':'snapshot_end'})
        close=c.BoundedOutput.close
        with mock.patch.object(sys,'argv',['snapshot_probe.py',str(root)]), mock.patch.object(support,'validate_runtime'), mock.patch.object(c,'snapshot_all',side_effect=snapshot), mock.patch.object(c.BoundedOutput,'close',autospec=True,side_effect=close) as closing, contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit) as stopped:runpy.run_path(str(Path(__file__).with_name('snapshot_probe.py')))
        self.assertEqual(stopped.exception.code,0)
        self.assertEqual(closing.call_count,1)
        self.assertFalse((root/'snapshot-synthetic').exists())
        self.assertTrue(json.loads((root/'snapshot-only-result.json').read_text())['completed'])

    def test_budget_serializes_competing_reservations_and_refuses_reuse(self):
        root=self.runtime();accepted=[];refused=[]
        def reserve(name):
            try: support.reserve(root,name,190);accepted.append(name)
            except ValueError: refused.append(name)
        threads=[threading.Thread(target=reserve,args=(name,)) for name in ('a','b')]
        for t in threads:t.start()
        for t in threads:t.join()
        self.assertEqual((len(accepted),len(refused)),(1,1))
        budget=json.loads((root/'budget.json').read_text())
        self.assertEqual(budget['allocated_seconds'],190)
        self.assertEqual(len(budget['attempts']),1)
        with self.assertRaises(ValueError):support.reserve(root,accepted[0],1)

    def test_snapshot_refuses_preexisting_intermediate_without_deleting_it(self):
        root=self.runtime();owned=root/'snapshot-synthetic';owned.mkdir();sentinel=owned/'sentinel';sentinel.write_text('preserve')
        with mock.patch.object(sys,'argv',['snapshot_probe.py',str(root)]), mock.patch.object(support,'validate_runtime'):
            with self.assertRaises(FileExistsError):runpy.run_path(str(Path(__file__).with_name('snapshot_probe.py')))
        self.assertEqual(sentinel.read_text(),'preserve')
        self.assertFalse((root/'snapshot-only').exists())


if __name__=='__main__':unittest.main()
