"""Pure offline regressions. Fake BCC tests simulate control flow, never a kernel.

Temporary synthetic metadata lives under this diagnostic directory and is
removed. No NSS store, tracing facility, workload or SDK is exercised.
"""

import builtins
import contextlib
import ctypes
import io
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import time
import types
import unittest
from unittest import mock

import collector as c


class Fixture(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="offline-test-", dir=c.HERE)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.parent = self.root / "monitored"
        self.parent.mkdir()
        self.nss = self.parent / "nssdb"
        self.nss.mkdir()
        self.output = self.root / "capture"

    def output_sink(self, maximum=131072):
        sink = c.BoundedOutput(self.output, maximum)
        self.addCleanup(sink.close)
        return sink

    def synthetic_proc(self):
        proc = self.root / "proc"
        proc.mkdir()
        target_a, target_b, executable = (self.root / name for name in ("target-a", "target-b", "exe"))
        for path in (target_a, target_b, executable):
            path.touch()
        leader = proc / "42"
        (leader / "task").mkdir(parents=True)
        def proc_text(pid, start):
            return f"{pid} (synthetic worker) S 0 " + " ".join(["0"] * 17 + [str(start), "0"])
        (leader / "stat").write_text(proc_text(42, 100))
        for tid, start, fd, target in ((42, 100, 3, target_a), (43, 200, 9, target_b)):
            task = leader / "task" / str(tid)
            (task / "fd").mkdir(parents=True)
            (task / "fdinfo").mkdir()
            (task / "stat").write_text(proc_text(tid, start))
            (task / "exe").symlink_to(executable)
            (task / "fd" / str(fd)).symlink_to(target)
            (task / "fdinfo" / str(fd)).write_text(f"pos:\t0\nflags:\t0100002\nmnt_id:\t12\nino:\t{target.stat().st_ino}\n")
            (task / "maps").write_text(f"1000-2000 rw-s 00000000 08:11 {target.stat().st_ino} /metadata/shared-{tid}\n2000-3000 rw-p 00000000 08:11 1 /private\n")
        return proc


class PolicyTests(Fixture):
    def test_duration_boundaries_and_types(self):
        for valid in (1, 120, 900):
            self.assertEqual(c.validate_duration(valid), valid)
        for invalid in (-1, 0, 901, 1.0, True, "1"):
            with self.subTest(invalid=invalid), self.assertRaises(c.PolicyError):
                c.validate_duration(invalid)

    def test_new_output_outside_nss_parent(self):
        self.assertEqual(c.validate_output(self.output, self.nss), (self.output, self.nss))
        for invalid in (self.parent / "new", self.nss / "new", self.parent, self.nss):
            with self.subTest(path=invalid), self.assertRaises(c.PolicyError):
                c.validate_output(invalid, self.nss)

    def test_existing_or_dangling_output_refused(self):
        self.output.mkdir()
        with self.assertRaises(c.PolicyError):
            c.validate_output(self.output, self.nss)
        dangling = self.root / "dangling"
        dangling.symlink_to(self.root / "absent")
        with self.assertRaises(c.PolicyError):
            c.validate_output(dangling, self.nss)

    def test_symlink_ancestor_refused(self):
        alias = self.root / "alias"
        alias.symlink_to(self.parent, target_is_directory=True)
        with self.assertRaises(c.PolicyError):
            c.validate_output(self.output, alias / "nssdb")

    def test_missing_output_parent_refused(self):
        with self.assertRaises(c.PolicyError):
            c.validate_output(self.root / "absent" / "output", self.nss)

    def test_directory_and_file_modes_ignore_permissive_umask(self):
        previous = os.umask(0)
        try:
            sink = self.output_sink()
            sink.write("metadata.json", {"metadata": True})
        finally:
            os.umask(previous)
        self.assertEqual(stat.S_IMODE(self.output.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE((self.output / "metadata.json").stat().st_mode), 0o600)

    def test_file_mode_with_restrictive_umask(self):
        previous = os.umask(0o777)
        try:
            sink = self.output_sink()
            sink.write("metadata.json", {"metadata": True})
        finally:
            os.umask(previous)
        self.assertEqual(stat.S_IMODE((self.output / "metadata.json").stat().st_mode), 0o600)

    def test_output_budget_preserves_receipt_reserve(self):
        sink = self.output_sink()
        with self.assertRaises(c.OutputLimit):
            sink.write("events.jsonl", {"comm": "x" * 70000})
        sink.write("receipt.json", {"complete": False, "overflow": True}, final=True)
        self.assertFalse(json.loads((self.output / "receipt.json").read_text())["complete"])
        self.assertLessEqual(sink.bytes, sink.max_bytes)

    def test_budget_range_and_filename_escape(self):
        with self.assertRaises(c.PolicyError):
            c.BoundedOutput(self.output, 1024)
        sink = self.output_sink()
        with self.assertRaises(c.PolicyError):
            sink.write("../escape.json", {})

    def test_output_files_exclusive_against_existing_entry(self):
        sink = self.output_sink()
        (self.output / "receipt.json").write_text("preserve")
        with self.assertRaises(FileExistsError):
            sink.write("receipt.json", {})
        self.assertEqual((self.output / "receipt.json").read_text(), "preserve")

    def test_swapped_output_symlink_cannot_chmod_unrelated_target(self):
        unrelated = self.root / "unrelated"
        unrelated.mkdir(mode=0o755)
        unrelated.chmod(0o755)
        real_fchmod = os.fchmod
        def swap_then_fchmod(fd, mode):
            if self.output.exists() and not self.output.is_symlink():
                self.output.rename(self.root / "displaced")
                self.output.symlink_to(unrelated, target_is_directory=True)
            return real_fchmod(fd, mode)
        with mock.patch.object(c.os, "fchmod", side_effect=swap_then_fchmod), self.assertRaises(c.PolicyError):
            c.BoundedOutput(self.output, 131072)
        self.assertEqual(stat.S_IMODE(unrelated.stat().st_mode), 0o755)

    def test_untrusted_or_shared_output_parent_refused(self):
        self.root.chmod(0o777)
        try:
            with self.assertRaises(c.PolicyError):
                c.BoundedOutput(self.output, 131072)
        finally:
            self.root.chmod(0o700)

    def test_internal_device_round_trip_differs_from_userspace_encoding(self):
        userspace = os.makedev(259, 65537)
        internal = c.kernel_device(userspace)
        self.assertEqual(c.device(internal), {"major": 259, "minor": 65537})
        self.assertNotEqual(internal, userspace)

    def test_suspend_aware_deadline_expires_even_if_monotonic_pauses(self):
        with mock.patch.object(c.time, "monotonic_ns", return_value=5), mock.patch.object(c.time, "clock_gettime_ns", return_value=100):
            self.assertTrue(c.deadline_elapsed(50, 99))

    def test_proc_stat_comm_parentheses_and_rounded_start_identity(self):
        text = "42 (worker (nested) name)) S 10 " + " ".join(["0"] * 17 + ["12345", "0"])
        result = c.proc_stat(text)
        self.assertEqual(result["pid"], 42)
        self.assertEqual(result["comm"], "worker (nested) name)")
        self.assertEqual(result["start_ticks"], 12345)


class OfflineModeTests(unittest.TestCase):
    def test_default_help_check_render_never_import_bcc(self):
        original = builtins.__import__
        def guarded(name, *args, **kwargs):
            if name == "bcc" or name.startswith("bcc."):
                raise RuntimeError("BCC import forbidden in offline mode")
            return original(name, *args, **kwargs)
        for argv in ([], ["check"], ["render"]):
            with self.subTest(argv=argv), mock.patch("builtins.__import__", side_effect=guarded), mock.patch.object(c.importlib.util, "find_spec", return_value=None), contextlib.redirect_stdout(io.StringIO()) as output:
                result = c.main(argv)
            self.assertIn(result, (0, 2))
            self.assertTrue(output.getvalue())

    def test_missing_dependencies_are_explicit_and_btf_not_headers(self):
        with mock.patch.object(c.importlib.util, "find_spec", return_value=None):
            report = c.dependency_report()
        self.assertFalse(report["tracing_imported"])
        self.assertFalse(report["attached"])
        self.assertTrue(any("bcc" in message for message in report["missing"]))
        if not report["prepared_headers_match"]:
            self.assertTrue(any("BTF is not a substitute" in message for message in report["missing"]))

    def test_stale_unprepared_header_directory_is_not_sufficient(self):
        with mock.patch.object(c.importlib.util, "find_spec", return_value=object()), mock.patch.object(c.Path, "is_dir", return_value=True), mock.patch.object(c.Path, "is_file", return_value=True), mock.patch.object(c.Path, "read_text", return_value='#define UTS_RELEASE "wrong-kernel"\n'):
            report = c.dependency_report()
        self.assertTrue(report["matching_headers_present"])
        self.assertFalse(report["prepared_headers_match"])

    def test_render_inert_by_default_and_has_both_kernel_deadlines(self):
        source = c.render_source(0)
        self.assertIn("#define DEADLINE_NS 0ULL", source)
        self.assertIn("#define BOOT_DEADLINE_NS 0ULL", source)
        self.assertNotIn("@DEADLINE_NS@", source)
        self.assertIn("bpf_ktime_get_boot_ns()", source)
        self.assertIn("now >= DEADLINE_NS || boot_now >= BOOT_DEADLINE_NS", source)

    def test_render_rejects_constant_injection(self):
        for value in (-1, "0; injected", True):
            with self.subTest(value=value), self.assertRaises(c.PolicyError):
                c.render_source(value)

    def test_source_never_reads_user_buffers_or_filters_processes(self):
        source = c.render_source(0)
        self.assertNotIn("bpf_probe_read_user", source)
        self.assertNotIn("bpf_get_current_pid_tgid() !=", source)
        self.assertNotIn("bpf_get_current_uid_gid() !=", source)
        self.assertNotIn("bpf_trace_printk", source)


class EventTests(unittest.TestCase):
    def event(self, op=1, result=7):
        value = c.Event()
        value.entry_ns, value.return_ns = 1_000_000_000, 1_000_000_007
        value.entry_boot_ns, value.return_boot_ns = 2_000_000_000, 2_000_000_007
        value.op, value.result = op, result
        value.process.pid, value.process.tid = 201, 202
        value.process.start_boottime, value.process.parent_start_boottime = 123456789, 987654
        value.process.ppid = 100
        value.process.comm = b"short-lived"
        value.process.exe.ino, value.process.exe.dev = 300, (8 << 20) | 1
        value.source.ino, value.source.dev, value.source.fs_magic = 400, (8 << 20) | 2, c.EXT4_MAGIC
        value.destination.ino, value.destination.dev, value.destination.fs_magic = 500, (8 << 20) | 3, c.EXT4_MAGIC
        value.target_after.ino, value.target_after.fs_magic = 600, c.EXT4_MAGIC
        return value

    def decode(self, value):
        return c.decode_event(value, "boot-identity", {"monotonic_ns": 1_000_000_000, "utc_ns": 1_700_000_000_000_000_000, "uncertainty_ns": 20})

    def test_success_semantics_excludes_failed_queued_zero_and_mkwrite(self):
        for op in c.BYTE_RESULT_OPS:
            self.assertTrue(c.mutation_credit(op, 1))
            for invalid in (0, -1, -529):
                self.assertFalse(c.mutation_credit(op, invalid))
        for op in c.ZERO_RESULT_OPS:
            self.assertTrue(c.mutation_credit(op, 0))
            self.assertFalse(c.mutation_credit(op, -1))
            self.assertFalse(c.mutation_credit(op, 1))
        for op in (101, 102, 103, 201, 202, 999):
            for result in (0, 1, -1):
                self.assertFalse(c.mutation_credit(op, result))

    def test_binary_decoding_retains_entry_identity_and_both_file_identities(self):
        original = self.event()
        encoded = bytes(original)
        decoded = self.decode(c.Event.from_buffer_copy(encoded))
        original.process.exe.ino = 999999
        self.assertEqual(decoded["process"]["executable_at_event"]["inode"], 300)
        self.assertEqual(decoded["process"]["group_leader_start_boottime_ns"], 123456789)
        self.assertEqual(decoded["process"]["parent_group_leader_start_boottime_ns"], 987654)
        self.assertEqual(decoded["process"]["pid"], 201)
        self.assertEqual(decoded["process"]["tid"], 202)
        self.assertEqual(decoded["source_before"]["inode"], 400)
        self.assertEqual(decoded["destination_before"]["inode"], 500)
        self.assertEqual(decoded["target_after"]["inode"], 600)
        self.assertEqual(decoded["process"]["boot_id"], "boot-identity")
        self.assertFalse(c.event_uncertainties(decoded))

    def test_utc_calibration_and_return_precision(self):
        result = self.decode(self.event())
        self.assertEqual(result["entry_utc_estimate"], "2023-11-14T22:13:20.000000000Z")
        self.assertEqual(result["return_utc_estimate"], "2023-11-14T22:13:20.000000007Z")

    def test_metadata_attr_size_and_page_evidence_have_separate_credit(self):
        value = self.event(5, 0)
        value.detail = 1  # ATTR_MODE; no content-operation credit
        decoded = self.decode(value)
        self.assertTrue(decoded["successful_mutation"])
        self.assertFalse(decoded["successful_content_mutation"])
        value.detail = 8
        self.assertTrue(self.decode(value)["successful_content_mutation"])
        value.op, value.result = 202, 0
        self.assertTrue(self.decode(value)["page_mkwrite_evidence_only"])
        self.assertFalse(self.decode(value)["successful_mutation"])

    def test_fallocate_success_never_implies_content_write(self):
        value = self.event(12, 0)
        for mode in (0, 1, 2, 3, 16, 32):
            value.detail = mode
            decoded = self.decode(value)
            self.assertEqual(decoded["detail"], mode)
            self.assertTrue(decoded["successful_mutation"])
            self.assertFalse(decoded["successful_content_mutation"])

    def test_exec_old_pid_and_fork_creator_are_preserved(self):
        value = self.event(102, 0)
        value.old_pid = 777
        self.assertEqual(self.decode(value)["exec_old_pid"], 777)
        value.op = 101
        value.fork_creator.pid, value.fork_creator.tid = 50, 51
        value.fork_creator.start_boottime = 987
        value.fork_creator.exe.ino = 333
        result = self.decode(value)
        self.assertEqual(result["fork_creator"]["pid"], 50)
        self.assertEqual(result["fork_creator"]["tid"], 51)
        self.assertEqual(result["process"]["ppid"], 100)
        self.assertEqual(result["fork_creator"]["group_leader_start_boottime_ns"], 987)

    def test_kernel_thread_has_explicit_no_executable_state(self):
        value = self.event()
        value.process.flags = c.IDENTITY_NO_MM | c.IDENTITY_KTHREAD
        value.process.exe.ino = 0
        result = self.decode(value)
        self.assertFalse(result["process"]["has_user_mm"])
        self.assertFalse(c.event_uncertainties(result))

    def test_exiting_user_without_mm_is_not_a_kernel_thread(self):
        value = self.event(103, 0)
        value.process.flags = c.IDENTITY_NO_MM | c.IDENTITY_EXIT_EXE_UNAVAILABLE
        value.process.exe.ino = 0
        decoded = self.decode(value)
        self.assertFalse(decoded["process"]["is_kernel_thread"])
        self.assertEqual(decoded["process"]["executable_availability"], "exiting-user-unavailable")
        self.assertFalse(c.event_uncertainties(decoded))
        value.op = 1
        self.assertTrue(c.event_uncertainties(self.decode(value)))

    def test_hook_return_widths_and_rename_flags(self):
        for op in c.ZERO_RESULT_OPS:
            self.assertEqual(c.normalize_return_value(op, 0xFFFFFFEA), -22)
            self.assertEqual(c.normalize_return_value(op, 0xDEADBEEF00000000), 0)
        self.assertEqual(c.normalize_return_value(1, 0xFFFFFFFFFFFFFFEA), -22)
        self.assertEqual(c.normalize_return_value(201, 0xFFFFFFFF80000000), 0x80000000)
        value = self.event(6, 0)
        value.detail = 2
        decoded = self.decode(value)
        self.assertEqual(decoded["rename_flags"], 2)
        self.assertTrue(decoded["rename_exchange"])
        self.assertEqual(decoded["source_before"]["inode"], 400)
        self.assertEqual(decoded["destination_before"]["inode"], 500)
        source = c.render_source(0)
        self.assertIn("context->target = e->op == 6 ? src", source)
        self.assertIn("sizeof(rd->flags), &rd->flags", source)
        self.assertIn("(s64)(s32)PT_REGS_RC(ctx)", source)
        self.assertIn("(s64)(u32)PT_REGS_RC(ctx)", source)

    def test_flags_missing_identity_and_unsupported_filesystem_invalidate(self):
        for mutate in (lambda v: setattr(v.source, "flags", c.PATH_READ_ERROR),
                       lambda v: setattr(v.destination, "flags", c.PATH_TRUNCATED),
                       lambda v: setattr(v.process, "start_boottime", 0),
                       lambda v: setattr(v.process.exe, "ino", 0),
                       lambda v: setattr(v.source, "fs_magic", 0x6969),
                       lambda v: setattr(v, "flags", c.OUTPUT_ANCESTRY_UNCERTAIN)):
            value = self.event()
            mutate(value)
            self.assertTrue(c.event_uncertainties(self.decode(value)))

    def test_create_uses_post_operation_target_inode(self):
        value = self.event(9, 0)
        value.destination.ino = 0
        self.assertFalse(c.event_uncertainties(self.decode(value)))
        value.target_after.ino = 0
        self.assertIn("successful mutation target inode missing", c.event_uncertainties(self.decode(value)))

    def test_non_utf8_name_round_trip(self):
        self.assertEqual(c.decode_text(b"name\xff\0ignored").encode("utf-8", "surrogateescape"), b"name\xff")


class SnapshotTests(Fixture):
    def test_actual_self_snapshot_keeps_its_enumeration_fd_alive(self):
        rows = []
        fd = os.open(c.HERE, os.O_RDONLY | os.O_DIRECTORY)
        try:
            errors = c.snapshot_process(os.getpid(), rows.append)
        finally:
            os.close(fd)
        self.assertTrue(any(row["kind"] == "descriptor" and row["fd"] == fd for row in rows))
        self.assertFalse(any(row.get("error") == "FileNotFoundError" and row.get("observation") == "descriptor" for row in errors), errors)

    def test_actual_proc_traversal_includes_thread_private_fds_and_shared_maps(self):
        proc, rows = self.synthetic_proc(), []
        with mock.patch.object(c, "PROC_ROOT", proc):
            errors = c.snapshot_process(42, rows.append)
        self.assertEqual(errors, [])
        descriptors = [row for row in rows if row["kind"] == "descriptor"]
        self.assertEqual({(row["pid"], row["tid"], row["fd"], row["task_start_ticks"]) for row in descriptors}, {(42, 42, 3, 100), (42, 43, 9, 200)})
        self.assertTrue(all(row["group_leader_start_ticks"] == 100 and row["identity_rechecked"] for row in descriptors))
        maps = [row for row in rows if row["kind"] == "shared_mapping"]
        self.assertEqual({row["path"] for row in maps}, {"/metadata/shared-42", "/metadata/shared-43"})
        self.assertEqual(maps[0]["device"], {"major": 8, "minor": 17})
        self.assertEqual(maps[0]["permissions"], "rw-s")

    def test_descriptor_reuse_is_flagged_and_other_thread_and_maps_continue(self):
        proc, rows = self.synthetic_proc(), []
        changed = proc / "42/task/42/fd/3"
        original = c.os.readlink
        swapped = False
        def swap(path, *args, **kwargs):
            nonlocal swapped
            result = original(path, *args, **kwargs)
            if Path(path) == changed and not swapped:
                swapped = True
                changed.unlink()
                changed.symlink_to(self.root / "target-b")
            return result
        with mock.patch.object(c, "PROC_ROOT", proc), mock.patch.object(c.os, "readlink", side_effect=swap):
            errors = c.snapshot_process(42, rows.append)
        self.assertTrue(any(row.get("fd") == 3 and row.get("tid") == 42 for row in errors))
        self.assertTrue(any(row["kind"] == "descriptor" and row["tid"] == 43 for row in rows))
        self.assertEqual(sum(row["kind"] == "shared_mapping" for row in rows), 2)

    def test_fdinfo_race_is_flagged(self):
        proc, rows = self.synthetic_proc(), []
        original = c.fdinfo
        changed = False
        def replace_info(path):
            nonlocal changed
            result = original(path)
            if path.name == "3" and not changed:
                changed = True
                path.write_text("pos:\t99\nflags:\t0100002\nmnt_id:\t13\nino:\t9\n")
            return result
        with mock.patch.object(c, "PROC_ROOT", proc), mock.patch.object(c, "fdinfo", side_effect=replace_info):
            errors = c.snapshot_process(42, rows.append)
        self.assertTrue(any("fdinfo changed" in row["detail"] for row in errors))
        self.assertFalse(any(row["kind"] == "descriptor" and row["fd"] == 3 for row in rows))

    def test_executable_change_is_flagged_without_discarding_other_metadata(self):
        proc, rows = self.synthetic_proc(), []
        replacement = self.root / "exe-new"
        replacement.touch()
        def emit(row):
            rows.append(row)
            if row["kind"] == "task" and row["tid"] == 42:
                link = proc / "42/task/42/exe"
                link.unlink()
                link.symlink_to(replacement)
        with mock.patch.object(c, "PROC_ROOT", proc):
            errors = c.snapshot_process(42, emit)
        self.assertTrue(any("executable identity changed" in row["detail"] for row in errors))
        self.assertEqual(sum(row["kind"] == "descriptor" for row in rows), 2)

    def test_interrupted_snapshots_preserve_accumulated_errors_and_phase(self):
        proc = self.synthetic_proc()
        (proc / "42/task/42/fdinfo/3").unlink()
        for failure in (TimeoutError, c.OutputLimit):
            with self.subTest(failure=failure):
                state = {"errors": []}
                class Sink:
                    def write(self, name, row):
                        pass
                def pump():
                    if state["errors"]:
                        raise failure("simulated snapshot interruption")
                with mock.patch.object(c, "PROC_ROOT", proc), self.assertRaises(failure):
                    c.snapshot_all(Sink(), "before", self.nss, pump=pump, state=state)
                self.assertTrue(state["started"])
                self.assertTrue(state["partial"])
                self.assertFalse(state["completed"])
                self.assertGreaterEqual(state["error_count"], 2)
                self.assertTrue(any(error.get("fd") == 3 for error in state["errors"]))

    def test_nss_root_replaced_by_symlink_is_not_descended(self):
        proc = self.synthetic_proc()
        destination = self.root / "unrelated-tree"
        destination.mkdir()
        (destination / "must-not-inventory").touch()
        self.nss.rmdir()
        self.nss.symlink_to(destination, target_is_directory=True)
        rows, state = [], {}
        class Sink:
            def write(self, name, row):
                rows.append(row)
        with mock.patch.object(c, "PROC_ROOT", proc):
            errors = c.snapshot_all(Sink(), "after", self.nss, state=state)
        self.assertTrue(any("symlink or non-directory" in row["detail"] for row in errors))
        self.assertFalse(any("must-not-inventory" in row.get("path", "") for row in rows))
        self.assertTrue(state["completed"])


class AccountingTests(unittest.TestCase):
    def test_profile_uses_only_exact_owned_rows(self):
        result = c.profile_accounting("unrelated 900 900\nr_vfs_write_bcc_12 5 0\n", ["r_vfs_write_bcc_12"])
        self.assertTrue(result["complete"])
        self.assertEqual(set(result["owned_rows"]), {"r_vfs_write_bcc_12"})

    def test_missing_missed_duplicate_and_malformed_profile_are_unknown(self):
        for text in ("", "r_test 10 1", "r_test 10 0\nr_test 10 0", "r_test unknown 0"):
            with self.subTest(text=text):
                self.assertFalse(c.profile_accounting(text, ["r_test"])["complete"])

    def test_every_loss_and_missing_counter_invalidates(self):
        counters = dict.fromkeys(c.COUNTERS, 0)
        profile = {"complete": True}
        self.assertEqual(c.loss_reasons(counters, 0, 0, profile), [])
        for key in ("submission_failures", "context_map_failures", "context_overwrites", "unmatched_returns",
                    "kernel_read_errors", "path_truncations", "deadline_unfinished_pairs", "identity_errors"):
            changed = dict(counters)
            changed[key] = 1
            self.assertTrue(c.loss_reasons(changed, 0, 0, profile))
            del changed[key]
            self.assertTrue(c.loss_reasons(changed, 0, 0, profile))
        for lost, unfinished, account in ((1, 0, profile), (0, 1, profile), (0, 0, {"complete": False})):
            self.assertTrue(c.loss_reasons(counters, lost, unfinished, account))


class FakeBCC:
    """Simulation only: no compilation, syscall probe or kernel event production."""
    instances = []
    fail_symbol = None
    mismatch_submissions = False
    scenario = "none"

    def __init__(self, text):
        self.log = ["fake-construct"]
        self.kprobe_fds = {}
        self.maps = {"enabled": {}, "pairs": {}, "counters": {n: ctypes.c_uint64(1 if n == 0 and self.mismatch_submissions else 0) for n in range(len(c.COUNTERS))}}
        self.instances.append(self)
        self.delivered = False

    def __getitem__(self, name):
        if name == "events":
            return self
        mapping = self.maps[name]
        class Access:
            def __getitem__(self, key):
                return mapping[key.value]
            def __setitem__(self, key, value):
                mapping[key.value] = value
            def items(self):
                return mapping.items()
        return Access()

    def open_perf_buffer(self, callback, **kwargs):
        self.log.append("perf-open")
        self.callback = callback
        self.lost_callback = kwargs["lost_cb"]

    def attach_raw_tracepoint(self, tp, fn_name):
        self.log.append("raw:" + tp)

    def attach_kprobe(self, event, fn_name):
        self.log.append("entry:" + event)
        if event == self.fail_symbol:
            raise RuntimeError("simulated mandatory attach failure: " + event)

    def attach_kretprobe(self, event, fn_name, maxactive):
        self.log.append("return:" + event)
        if maxactive != 1024:
            raise RuntimeError("simulation requires explicit maxactive")
        self.kprobe_fds[("r_" + event).encode()] = object()

    def perf_buffer_poll(self, timeout):
        self.log.append("poll")
        if self.delivered or not self.maps["enabled"].get(0, ctypes.c_uint64(0)).value:
            return
        self.delivered = True
        if self.scenario == "none":
            return
        if self.scenario == "signal":
            c.signal.raise_signal(c.signal.SIGTERM)
            return
        if self.scenario == "lost":
            self.maps["counters"][0].value += 3
            self.lost_callback(0, 3)
            return
        event = EventTests().event()
        event.entry_ns = event.return_ns = time.monotonic_ns()
        event.entry_boot_ns = event.return_boot_ns = time.clock_gettime_ns(time.CLOCK_BOOTTIME)
        if self.scenario == "uncertain":
            event.source.flags = c.PATH_TRUNCATED
        self.maps["counters"][0].value += 1
        self.callback(0, ctypes.byref(event), ctypes.sizeof(event) - (1 if self.scenario == "bad-abi" else 0))

    def cleanup(self):
        self.log.append("collector-cleanup")


class SimulatedCaptureTests(Fixture):
    def run_simulated(self, fail=None, mismatch=False, scenario="none", owner_present=True):
        FakeBCC.fail_symbol = fail
        FakeBCC.mismatch_submissions = mismatch
        FakeBCC.instances.clear()
        FakeBCC.scenario = scenario
        proc = self.synthetic_proc()
        args = types.SimpleNamespace(duration=1, output=self.output, nss_dir=self.nss, max_bytes=131072,
                                     host_namespaces_confirmed=True, owner_pid=123)
        def elapsed(*unused):
            return (self.output / "ready.json").exists()
        fake_module = types.ModuleType("bcc")
        fake_module.BPF = FakeBCC
        fake_module.__version__ = "simulated-bcc-not-kernel-validation"
        with mock.patch.dict(sys.modules, {"bcc": fake_module}), mock.patch.object(c, "PROC_ROOT", proc), mock.patch.object(c, "namespace_check", return_value={}), mock.patch.object(c, "owner_identity", return_value={"pid": 123, "start_ticks": 5}), mock.patch.object(c, "owner_alive", return_value=owner_present), mock.patch.object(c, "dependency_report", return_value={"missing": []}), mock.patch.object(c, "mount_record", return_value={"filesystem": "ext4"}), mock.patch.object(c, "deadline_elapsed", side_effect=elapsed), mock.patch.object(c, "read_profile", return_value={"complete": True, "owned_rows": {}, "errors": []}), contextlib.redirect_stdout(io.StringIO()):
            result = c.capture(args)
        return result, json.loads((self.output / "receipt.json").read_text()), FakeBCC.instances[0]

    def test_simulated_capture_order_cleanup_and_frozen_authority(self):
        result, receipt, bpf = self.run_simulated()
        self.assertEqual(result, 0)
        self.assertEqual(bpf.log[1], "perf-open")
        self.assertTrue(all(item.startswith("raw:") for item in bpf.log[2:5]))
        self.assertEqual(bpf.log[-1], "collector-cleanup")
        self.assertFalse(receipt["complete"])
        self.assertFalse(receipt["global_immutability_proven"])
        self.assertEqual(receipt["frozen"], c.FROZEN)
        self.assertEqual(bpf.maps["enabled"][0].value, 0)
        self.assertTrue(receipt["supported_probe_accounting_complete"])

    def test_simulated_missing_mandatory_probe_refuses_readiness_and_cleans(self):
        result, receipt, bpf = self.run_simulated(fail="notify_change")
        self.assertEqual(result, 2)
        self.assertFalse(receipt["ready"])
        self.assertFalse((self.output / "ready.json").exists())
        self.assertTrue(receipt["collector_only_cleanup_completed"])
        self.assertIn("collector-cleanup", bpf.log)
        self.assertTrue(any("mandatory attach failure" in value for value in receipt["errors"]))

    def test_simulated_unread_event_invalidates_accounting(self):
        result, receipt, bpf = self.run_simulated(mismatch=True)
        self.assertEqual(result, 2)
        self.assertFalse(receipt["supported_probe_accounting_complete"])
        self.assertIn("submission/delivery/loss counts do not reconcile", receipt["incomplete_reasons"])

    def test_simulated_binary_event_persists_and_source_provenance_is_bound(self):
        result, receipt, bpf = self.run_simulated(scenario="valid")
        self.assertEqual(result, 0)
        event = json.loads((self.output / "events.jsonl").read_text())
        self.assertTrue(event["successful_mutation"])
        self.assertEqual(event["process"]["executable_at_event"]["inode"], 300)
        self.assertEqual(receipt["stats"]["events"], 1)
        self.assertEqual(receipt["diagnostic_provenance"]["collector_python_source_sha256"], c.hashlib.sha256((c.HERE / "collector.py").read_bytes()).hexdigest())
        self.assertEqual(receipt["diagnostic_provenance"]["bcc_version"], "simulated-bcc-not-kernel-validation")
        self.assertTrue(receipt["diagnostic_provenance"]["rendered_bpf_sha256"])
        self.assertTrue(receipt["snapshot_phases"]["before"]["completed"])
        before = [json.loads(line) for line in (self.output / "before.jsonl").read_text().splitlines()]
        self.assertEqual(sum(row["kind"] == "descriptor" for row in before), 2)
        self.assertEqual(sum(row["kind"] == "shared_mapping" for row in before), 2)

    def test_simulated_callback_uncertainty_persists_and_refuses_accounting(self):
        result, receipt, bpf = self.run_simulated(scenario="uncertain")
        self.assertEqual(result, 2)
        self.assertFalse(receipt["supported_probe_accounting_complete"])
        self.assertIn("source_before metadata/path incomplete", receipt["incomplete_reasons"])
        event = json.loads((self.output / "events.jsonl").read_text())
        self.assertEqual(event["source_before"]["flags"], c.PATH_TRUNCATED)

    def test_simulated_lost_callback_refuses_accounting(self):
        result, receipt, bpf = self.run_simulated(scenario="lost")
        self.assertEqual(result, 2)
        self.assertEqual(receipt["stats"]["perf_lost"], 3)
        self.assertIn("perf lost events: 3", receipt["incomplete_reasons"])

    def test_simulated_callback_abi_error_refuses_ready_and_cleans(self):
        result, receipt, bpf = self.run_simulated(scenario="bad-abi")
        self.assertEqual(result, 2)
        self.assertFalse(receipt["ready"])
        self.assertIn("event ABI size mismatch", receipt["incomplete_reasons"])
        self.assertTrue(receipt["collector_only_cleanup_completed"])

    def test_simulated_callback_decode_error_is_retained(self):
        with mock.patch.object(c, "decode_event", side_effect=ValueError("simulated decoding failure")):
            result, receipt, bpf = self.run_simulated(scenario="valid")
        self.assertEqual(result, 2)
        self.assertTrue(any("simulated decoding failure" in reason for reason in receipt["incomplete_reasons"]))
        self.assertFalse(receipt["ready"])

    def test_simulated_callback_overflow_closes_gate_and_preserves_partial_phases(self):
        original = c.BoundedOutput.write
        def overflow(sink, name, record, final=False):
            if name == "events.jsonl":
                raise c.OutputLimit("simulated event output overflow")
            return original(sink, name, record, final)
        with mock.patch.object(c.BoundedOutput, "write", new=overflow):
            result, receipt, bpf = self.run_simulated(scenario="valid")
        self.assertEqual(result, 2)
        self.assertTrue(receipt["stop"]["overflow"])
        self.assertTrue(receipt["snapshot_phases"]["before"]["partial"])
        self.assertGreater(receipt["snapshot_error_counts"]["before"], 0)
        self.assertFalse(receipt["snapshot_phases"]["after"]["started"])
        self.assertEqual(bpf.maps["enabled"][0].value, 0)
        self.assertTrue(receipt["collector_only_cleanup_completed"])

    def test_simulated_owner_exit_refuses_ready_and_preserves_partial_snapshot(self):
        result, receipt, bpf = self.run_simulated(owner_present=False)
        self.assertEqual(result, 2)
        self.assertFalse(receipt["ready"])
        self.assertTrue(receipt["snapshot_phases"]["before"]["partial"])
        self.assertTrue(any("caller shell exited" in error for error in receipt["errors"]))

    def test_simulated_signal_refuses_ready_and_requests_only_collector_cleanup(self):
        result, receipt, bpf = self.run_simulated(scenario="signal")
        self.assertEqual(result, 2)
        self.assertFalse(receipt["ready"])
        self.assertEqual(receipt["stop"]["signal"], c.signal.SIGTERM)
        self.assertEqual(bpf.log[-1], "collector-cleanup")


if __name__ == "__main__":
    unittest.main()
