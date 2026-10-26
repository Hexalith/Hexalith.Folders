"""Pure guard tests; no adapter import, Git mutation, sandbox, SDK or target load."""
import ast
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import stat
import tempfile
import threading
import unittest

SOURCE = Path(__file__).with_name('execute_after_filter_reset_authorization.py')
TREE = ast.parse(SOURCE.read_text())
NAMES = {'digest', 'sha', 'same', 'require_safe_python', 'validate_recording_state',
    'reject_hidden_index_flags', 'verify_recording_bindings', 'claim_attempt'}
SCOPE = dict(hashlib=hashlib, os=os, Path=Path, stat=stat)
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n, ast.FunctionDef)
    and n.name in NAMES], type_ignores=[]), '<pure guard definitions>', 'exec'), SCOPE)


class AuthorizationGuardTests(unittest.TestCase):
    def test_allowed_recording_state(self):
        row = dict(rawHeader=':000000 100644 ' + '0'*40 + ' ' + 'a'*40 + ' A', path='record.json')
        SCOPE['validate_recording_state']([row], ['record.json'], {'record.json'})

    def test_extra_untracked_file_rejected(self):
        with self.assertRaises(ValueError):
            SCOPE['validate_recording_state']([], ['record.json', 'unapproved-note.txt'], {'record.json'})

    def test_extra_committed_addition_rejected(self):
        row = dict(rawHeader=':000000 100644 ' + '0'*40 + ' ' + 'a'*40 + ' A', path='other.json')
        with self.assertRaises(ValueError): SCOPE['validate_recording_state']([row], [], {'record.json'})

    def test_changed_deleted_renamed_or_executable_paths_rejected(self):
        for header in [':100644 100644 0 1 M', ':100644 000000 0 1 D',
            ':100644 100644 0 1 R100', ':000000 100755 0 1 A']:
            with self.subTest(header=header), self.assertRaises(ValueError):
                SCOPE['validate_recording_state']([dict(rawHeader=header, path='record.json')], [], {'record.json'})

    def test_hidden_index_flags_rejected(self):
        for data in [b'h file\0', b'S file\0', b's file\0']:
            with self.subTest(data=data), self.assertRaises(ValueError):
                SCOPE['reject_hidden_index_flags'](data)
        SCOPE['reject_hidden_index_flags'](b'H file\0H references/module\0')

    def test_optimized_or_nonisolated_python_rejected(self):
        for optimized, isolated in [(1, 1), (2, 1), (0, 0)]:
            with self.subTest(flags=(optimized, isolated)), self.assertRaises(RuntimeError):
                SCOPE['require_safe_python'](optimized, isolated)
        SCOPE['require_safe_python'](0, 1)

    def test_changed_bound_record_detected_with_same_path(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); path = root / 'record.json'; path.write_bytes(b'original')
            bindings = {'record.json': dict(bytes=8, sha256=hashlib.sha256(b'original').hexdigest())}
            SCOPE['verify_recording_bindings'](root, bindings)
            path.write_bytes(b'modified')
            with self.assertRaises(AssertionError): SCOPE['verify_recording_bindings'](root, bindings)

    def test_approval_can_be_claimed_only_once(self):
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory) / 'claims'; key = hashlib.sha256(b'synthetic guard fixture').hexdigest()
            SCOPE['claim_attempt'](parent, key)
            with self.assertRaises(FileExistsError): SCOPE['claim_attempt'](parent, key)

    def test_concurrent_claim_has_one_winner(self):
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory) / 'claims'; key = hashlib.sha256(b'synthetic race fixture').hexdigest()
            barrier = threading.Barrier(2)
            def attempt():
                barrier.wait()
                try: SCOPE['claim_attempt'](parent, key); return 'claimed'
                except FileExistsError: return 'rejected'
            with ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(lambda _: attempt(), range(2)))
            self.assertEqual(sorted(results), ['claimed', 'rejected'])

    def test_claim_parent_symlink_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); target = root / 'target'; target.mkdir(mode=0o700)
            link = root / 'claims'; link.symlink_to(target, target_is_directory=True)
            with self.assertRaises(ValueError): SCOPE['claim_attempt'](link, 'a'*64)

    def test_guard_is_connected_before_claim_or_launch(self):
        calls = [(n.lineno, n.func.id) for n in ast.walk(TREE)
            if isinstance(n, ast.Call) and isinstance(n.func, ast.Name)]
        location = lambda name: min(line for line, symbol in calls if symbol == name)
        self.assertLess(location('require_safe_python'), location('validate_recording_state'))
        self.assertLess(location('validate_recording_state'), location('claim_attempt'))
        launch = min(n.lineno for n in ast.walk(TREE) if isinstance(n, ast.Call)
            and isinstance(n.func, ast.Attribute) and n.func.attr == 'Popen')
        self.assertLess(location('claim_attempt'), launch)
        binding_calls = sorted(line for line, symbol in calls if symbol == 'verify_recording_bindings')
        self.assertTrue(any(line < launch for line in binding_calls))
        self.assertTrue(any(line > launch for line in binding_calls))

    def test_inner_interpreter_is_current_and_isolated(self):
        assignments = [n for n in TREE.body if isinstance(n, ast.Assign)
            and any(isinstance(t, ast.Name) and t.id == 'argv' for t in n.targets)]
        values = assignments[0].value.elts
        self.assertEqual(ast.unparse(values[0]), 'sys.executable')
        self.assertEqual([ast.literal_eval(v) for v in values[1:3]], ['-I', '-B'])

    def test_plan_command_uses_bound_script_and_interpreter(self):
        plan = json.loads((SOURCE.parents[2] / 'story-1-17-cli-4-38-filter-reset-plan-2026-10-26.json').read_text())
        self.assertEqual(plan['exactExecutionCommand'], ['timeout', '--signal=TERM', '--kill-after=5s',
            '600s', plan['pythonInterpreter']['path'], '-I', '-B', str(SOURCE.resolve()),
            '--authorization', plan['authorization']['path']])


if __name__ == '__main__': unittest.main()
