#!/usr/bin/env python3
"""Opt-in prospective Linux 6.6 metadata collector; offline modes use stdlib only.

No workload launcher exists here. Help, check and render never import BCC. Kernel
attachment and BCC compilation have not been validated on the current host.
"""

from __future__ import annotations

import argparse
import ctypes
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import re
import signal
import stat
import sys
import time


HERE = Path(__file__).resolve().parent
PROC_ROOT = Path("/proc")
MAX_DURATION = 900
MAX_BYTES = 256 * 1024 * 1024
RESERVE_BYTES = 64 * 1024
EXT4_MAGIC = 0xEF53
PATH_PARTS = 12
PATH_PART_LEN = 48
PATH_READ_ERROR = 1
PATH_TRUNCATED = 2
PATH_MOUNT_RELATIVE = 4
EVENT_SKIPPED = 8
IDENTITY_ERROR = 16
OUTPUT_ANCESTRY_UNCERTAIN = 32
IDENTITY_NO_MM = 64
IDENTITY_KTHREAD = 128
IDENTITY_EXIT_EXE_UNAVAILABLE = 256

# symbol, entry program, return program, operation number. Every listed hook is
# mandatory: an absent/static/inlined/unprobeable symbol refuses readiness.
HOOKS = (
    ("vfs_write", "enter_vfs_write", "return_vfs_write", 1),
    ("__kernel_write", "enter_kernel_write", "return_kernel_write", 2),
    ("do_iter_write", "enter_iter_write", "return_iter_write", 3),
    ("vfs_iter_write", "enter_vfs_iter_write", "return_vfs_iter_write", 4),
    ("notify_change", "enter_notify_change", "return_notify_change", 5),
    ("vfs_rename", "enter_rename", "return_rename", 6),
    ("vfs_link", "enter_link", "return_link", 7),
    ("vfs_unlink", "enter_unlink", "return_unlink", 8),
    ("vfs_create", "enter_create", "return_create", 9),
    ("vfs_mkdir", "enter_mkdir", "return_mkdir", 10),
    ("vfs_rmdir", "enter_rmdir", "return_rmdir", 11),
    ("vfs_fallocate", "enter_fallocate", "return_fallocate", 12),
    ("vfs_copy_file_range", "enter_copy", "return_copy", 13),
    ("do_splice_direct", "enter_splice", "return_splice", 14),
    ("iter_file_splice_write", "enter_splice_file", "return_splice_file", 15),
    ("vfs_mknod", "enter_mknod", "return_mknod", 16),
    ("vfs_tmpfile", "enter_tmpfile", "return_tmpfile", 17),
    ("filemap_page_mkwrite", "enter_generic_mkwrite", "return_generic_mkwrite", 201),
    ("ext4_page_mkwrite", "enter_ext4_mkwrite", "return_ext4_mkwrite", 202),
)
OP_NAMES = {row[3]: row[0] for row in HOOKS}
OP_NAMES.update({101: "sched_process_fork", 102: "sched_process_exec", 103: "sched_process_exit"})
BYTE_RESULT_OPS = frozenset((1, 2, 3, 4, 13, 14, 15))
ZERO_RESULT_OPS = frozenset((5, 6, 7, 8, 9, 10, 11, 12, 16, 17))
COUNTERS = (
    "submissions", "submission_failures", "context_map_failures", "context_overwrites",
    "unmatched_returns", "kernel_read_errors", "path_truncations", "deadline_unfinished_pairs",
    "excluded_entries", "paired_entries", "paired_returns", "identity_errors",
)
UNSUPPORTED = (
    "Already-writable MAP_SHARED stores can mutate bytes without another page-mkwrite event.",
    "Page-mkwrite events are evidence only, never successful content mutations.",
    "Asynchronous completion, io_uring and direct filesystem routes can bypass these VFS probes.",
    "DAX, non-ext4 filesystem semantics and filesystem/kernel-specific bypasses are unsupported.",
    "External VM, hypervisor or host writers are outside this Linux kernel observation.",
    "Dentry paths are mount-relative; mount aliases, namespace aliases and disconnected dentries remain uncertain.",
    "Bounded path components and snapshot races can make identities incomplete.",
    "Nested VFS hooks can report the same mutation more than once; records are not independent write counts.",
    "All-process snapshots are racy observations, not atomic process/file/mapping inventories.",
    "Metadata timestamps, open descriptors and prevention settings alone provide no writer proof.",
    "UTC calibration samples cannot rule out clock steps that occur and reverse between samples.",
    "PMU entry dispatch/recursion misses are not directly profiled; unmatched returns detect some, but paired omissions cannot be ruled out by counters.",
)
FROZEN = {
    "qualification": "incomplete", "parent": "in-progress", "tracker": "backlog",
    "productionControl": "V1Only", "T0": None, "activationAuthority": None, "staging": "0/14",
    "historicalWriters": {"2026-10-30T16:39:27Z": "unresolved", "2026-10-30T16:54:04Z": "unresolved"},
}


class PolicyError(ValueError):
    """A request is outside the collector's bounded opt-in policy."""


class OutputLimit(RuntimeError):
    """The configured metadata budget has been reached."""


class PathMeta(ctypes.Structure):
    _fields_ = [("ino", ctypes.c_uint64), ("parent_ino", ctypes.c_uint64),
                ("size", ctypes.c_uint64), ("fs_magic", ctypes.c_uint64),
                ("dev", ctypes.c_uint32), ("mode", ctypes.c_uint32),
                ("flags", ctypes.c_uint32), ("count", ctypes.c_uint32),
                ("parts", (ctypes.c_char * PATH_PART_LEN) * PATH_PARTS)]


class Identity(ctypes.Structure):
    _fields_ = [("start_boottime", ctypes.c_uint64), ("parent_start_boottime", ctypes.c_uint64),
                ("pid", ctypes.c_uint32), ("tid", ctypes.c_uint32), ("ppid", ctypes.c_uint32),
                ("uid", ctypes.c_uint32), ("gid", ctypes.c_uint32), ("flags", ctypes.c_uint32),
                ("comm", ctypes.c_char * 16), ("exe", PathMeta)]


class Event(ctypes.Structure):
    _fields_ = [("entry_ns", ctypes.c_uint64), ("return_ns", ctypes.c_uint64),
                ("entry_boot_ns", ctypes.c_uint64), ("return_boot_ns", ctypes.c_uint64),
                ("result", ctypes.c_int64), ("detail", ctypes.c_uint64), ("requested_size", ctypes.c_uint64),
                ("op", ctypes.c_uint32), ("flags", ctypes.c_uint32),
                ("old_pid", ctypes.c_uint32), ("reserved", ctypes.c_uint32),
                ("process", Identity), ("fork_creator", Identity),
                ("source", PathMeta), ("destination", PathMeta), ("target_after", PathMeta)]


def decode_text(value):
    """Preserve non-UTF8 names reversibly without reading any file contents."""
    return bytes(value).split(b"\0", 1)[0].decode("utf-8", "surrogateescape")


def device(raw):
    """Decode Linux internal dev_t, distinct from userspace st_dev encoding."""
    return {"major": int(raw) >> 20, "minor": int(raw) & ((1 << 20) - 1)}


def kernel_device(st_dev):
    major, minor = os.major(st_dev), os.minor(st_dev)
    if major >= (1 << 12) or minor >= (1 << 20):
        raise PolicyError("device number does not fit Linux internal dev_t")
    return (major << 20) | minor


def path_record(value):
    count = int(value.count)
    parts = [decode_text(value.parts[n]) for n in range(min(count, PATH_PARTS))]
    return {"device": device(value.dev), "inode": int(value.ino), "parent_inode": int(value.parent_ino),
            "size": int(value.size), "mode": int(value.mode), "fs_magic": int(value.fs_magic),
            "flags": int(value.flags), "component_count": count, "components_leaf_first": parts,
            "mount_relative_path": ("/" if parts and parts[-1] == "/" else "") +
                "/".join(part for part in reversed(parts) if part != "/"),
            "host_absolute_path_proven": False}


def identity_record(value, boot_id):
    return {"boot_id": boot_id, "pid": int(value.pid), "tid": int(value.tid), "ppid": int(value.ppid),
            "group_leader_start_boottime_ns": int(value.start_boottime),
            "parent_group_leader_start_boottime_ns": int(value.parent_start_boottime),
            "uid": int(value.uid), "gid": int(value.gid), "comm": decode_text(value.comm),
            "flags": int(value.flags), "has_user_mm": not bool(value.flags & IDENTITY_NO_MM),
            "is_kernel_thread": bool(value.flags & IDENTITY_KTHREAD),
            "executable_availability": "available" if value.exe.ino else (
                "kernel-thread-no-user-executable" if value.flags & IDENTITY_KTHREAD else
                "exiting-user-unavailable" if value.flags & IDENTITY_EXIT_EXE_UNAVAILABLE else "unavailable"),
            "executable_at_event": path_record(value.exe)}


def normalize_return_value(op, raw):
    if op in ZERO_RESULT_OPS:
        return ctypes.c_int32(raw).value
    if op in (201, 202):
        return ctypes.c_uint32(raw).value
    return ctypes.c_int64(raw).value


def mutation_credit(op, result):
    """Queued (-EIOCBQUEUED), failed and zero-byte writes receive no credit."""
    return (op in BYTE_RESULT_OPS and result > 0) or (op in ZERO_RESULT_OPS and result == 0)


def utc_ns_text(value):
    seconds, nanoseconds = divmod(int(value), 1_000_000_000)
    return dt.datetime.fromtimestamp(seconds, dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S") + f".{nanoseconds:09d}Z"


def clock_sample():
    before = time.monotonic_ns()
    wall = time.time_ns()
    after = time.monotonic_ns()
    return {"monotonic_ns": (before + after) // 2, "utc_ns": wall,
            "uncertainty_ns": after - before, "utc": utc_ns_text(wall)}


def decode_event(value, boot_id, calibration):
    result = normalize_return_value(int(value.op), int(value.result))
    record = {"operation": OP_NAMES.get(int(value.op), "unknown"), "op": int(value.op),
              "entry_monotonic_ns": int(value.entry_ns), "return_monotonic_ns": int(value.return_ns),
              "entry_boottime_ns": int(value.entry_boot_ns), "return_boottime_ns": int(value.return_boot_ns),
              "return_value": result, "flags": int(value.flags), "detail": int(value.detail),
              "rename_flags": int(value.detail) if value.op == 6 else None,
              "rename_exchange": bool(value.detail & 2) if value.op == 6 else False,
              "requested_size": int(value.requested_size),
              "exec_old_pid": int(value.old_pid), "process": identity_record(value.process, boot_id),
              "fork_creator": identity_record(value.fork_creator, boot_id) if value.op == 101 else None,
              "source_before": path_record(value.source), "destination_before": path_record(value.destination),
              "target_after": path_record(value.target_after),
              "successful_mutation": mutation_credit(int(value.op), result),
              "successful_content_mutation": mutation_credit(int(value.op), result) and
                  (value.op in BYTE_RESULT_OPS or (value.op == 5 and bool(value.detail & 8))),
              "page_mkwrite_evidence_only": int(value.op) in (201, 202)}
    offset = calibration["utc_ns"] - calibration["monotonic_ns"]
    record["entry_utc_estimate"] = utc_ns_text(value.entry_ns + offset)
    record["return_utc_estimate"] = utc_ns_text(value.return_ns + offset)
    record["utc_sample_uncertainty_ns"] = calibration["uncertainty_ns"]
    record["utc_sample_monotonic_ns"] = calibration["monotonic_ns"]
    return record


def event_uncertainties(record):
    reasons = []
    if record["operation"] == "unknown":
        reasons.append("unknown event operation")
    if record["flags"] & ~EVENT_SKIPPED:
        reasons.append("event metadata/ancestry uncertainty")
    identity = record["process"]
    valid_identity_flags = IDENTITY_NO_MM | IDENTITY_KTHREAD | IDENTITY_EXIT_EXE_UNAVAILABLE
    if identity["flags"] & ~valid_identity_flags or not identity["group_leader_start_boottime_ns"]:
        reasons.append("event-time process identity incomplete")
    if record["fork_creator"] and (record["fork_creator"]["flags"] & ~(IDENTITY_NO_MM | IDENTITY_KTHREAD) or not record["fork_creator"]["group_leader_start_boottime_ns"]):
        reasons.append("fork creator identity incomplete")
    for name in ("source_before", "destination_before", "target_after"):
        path = record[name]
        if path["flags"] & (PATH_READ_ERROR | PATH_TRUNCATED) or path["component_count"] > PATH_PARTS:
            reasons.append(name + " metadata/path incomplete")
        if path["fs_magic"] and path["fs_magic"] != EXT4_MAGIC:
            reasons.append(name + " unsupported filesystem")
    exe = identity["executable_at_event"]
    if exe["flags"] & (PATH_READ_ERROR | PATH_TRUNCATED):
        reasons.append("event-time executable path incomplete")
    expected_exit_unavailable = record["op"] == 103 and bool(identity["flags"] & IDENTITY_EXIT_EXE_UNAVAILABLE)
    if not identity["is_kernel_thread"] and not exe["inode"] and not expected_exit_unavailable:
        reasons.append("event-time executable inode missing")
    if identity["flags"] & IDENTITY_EXIT_EXE_UNAVAILABLE and record["op"] != 103:
        reasons.append("exiting-user identity used outside exit event")
    if record["successful_mutation"]:
        op = record["op"]
        target = record["target_after"] if op in (9, 10, 16, 17) else (
            record["destination_before"] if op in (13, 14, 15) else record["source_before"])
        if not target["inode"]:
            reasons.append("successful mutation target inode missing")
    return reasons


def validate_duration(value):
    if isinstance(value, bool) or not isinstance(value, int) or not 1 <= value <= MAX_DURATION:
        raise PolicyError("duration must be an integer in 1..900 seconds, including setup")
    return value


def no_symlink_ancestors(path):
    path = Path(os.path.abspath(path))
    for item in (path, *path.parents):
        if item.is_symlink():
            raise PolicyError(f"symlink path component refused: {item}")
    return path


def validate_output(output, nss_dir):
    output = no_symlink_ancestors(output)
    nss_dir = no_symlink_ancestors(nss_dir)
    if output.exists() or os.path.lexists(output):
        raise PolicyError(f"output must be new; reuse refused: {output}")
    if not output.parent.is_dir():
        raise PolicyError("output parent must already exist")
    parent = nss_dir.parent.resolve(strict=True)
    if output == parent or parent in output.parents:
        raise PolicyError("output must be outside the monitored NSS parent")
    if not nss_dir.is_dir():
        raise PolicyError("NSS directory must exist and be a real directory")
    if output in nss_dir.parents:
        raise PolicyError("output may not contain the NSS tree")
    return output, nss_dir


def render_source(deadline_ns, output_dev=0, output_ino=0, boot_deadline_ns=0, template=None):
    if any(isinstance(v, bool) or not isinstance(v, int) or v < 0 for v in (deadline_ns, output_dev, output_ino, boot_deadline_ns)):
        raise PolicyError("render constants must be nonnegative integers")
    source = (HERE / "trace.bpf.c").read_text(encoding="utf-8") if template is None else template
    for key, value in (("DEADLINE_NS", deadline_ns), ("BOOT_DEADLINE_NS", boot_deadline_ns),
                       ("OUTPUT_DEV", output_dev), ("OUTPUT_INO", output_ino)):
        source = source.replace("@" + key + "@", str(value))
    if re.search(r"@[A-Z_]+@", source):
        raise PolicyError("unresolved BPF template token")
    return source


def dependency_report():
    release = platform.release()
    headers = Path("/lib/modules") / release / "build"
    utsrelease = headers / "include/generated/utsrelease.h"
    autoconf = headers / "include/generated/autoconf.h"
    prepared_release = None
    try:
        match = re.search(r'^#define\s+UTS_RELEASE\s+"([^"\n]+)"', utsrelease.read_text(), re.MULTILINE)
        prepared_release = match[1] if match else None
    except OSError:
        pass
    prepared = headers.is_dir() and prepared_release == release and autoconf.is_file()
    try:
        bcc_found = importlib.util.find_spec("bcc") is not None
    except (ImportError, ValueError):
        bcc_found = False
    missing = []
    if not bcc_found:
        missing.append("Python BCC module bcc (Ubuntu package python3-bpfcc; /usr/bin/python3)")
    if not prepared:
        missing.append(str(headers) + " (matching prepared utsrelease.h/autoconf.h required; BTF is not a substitute)")
    if not release.startswith("6.6."):
        missing.append("Linux 6.6 running kernel required by explicit probe signatures")
    return {"mode": "offline-check", "tracing_imported": False, "attached": False, "kernel_release": release,
            "python": sys.executable, "bcc_module_found_without_import": bcc_found,
            "matching_headers": str(headers), "matching_headers_present": headers.is_dir(),
            "prepared_header_release": prepared_release, "prepared_headers_match": prepared,
            "btf_present": Path("/sys/kernel/btf/vmlinux").exists(), "missing": missing,
            "capture_verified": False, "note": "Presence does not verify compilation, symbol availability or attachment."}


def trusted_parent_fd(path):
    """Open every parent component relative to a no-follow directory anchor.

    Writable shared ancestors are allowed only for root-owned sticky directories
    such as /var/tmp. The final parent must be private and owned by this EUID.
    """
    parent = Path(os.path.abspath(path)).parent
    fd = os.open("/", os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        anchor = os.fstat(fd)
        if anchor.st_uid != 0 or anchor.st_mode & 0o022:
            raise PolicyError("unsafe root directory ownership/write permissions")
        for part in parent.parts[1:]:
            following = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
            os.close(fd)
            fd = following
            info = os.fstat(fd)
            sticky_root = info.st_uid == 0 and bool(info.st_mode & stat.S_ISVTX)
            if info.st_uid not in (0, os.geteuid()) or (info.st_mode & 0o022 and not sticky_root):
                raise PolicyError("unsafe output ancestor ownership/write permissions: " + str(parent))
        info = os.fstat(fd)
        if info.st_uid != os.geteuid() or info.st_mode & 0o077:
            raise PolicyError("output parent must be private (0700) and owned by the collector EUID")
        return fd
    except BaseException:
        os.close(fd)
        raise


class BoundedOutput:
    """O_EXCL files, private directory, total byte budget with a final-receipt reserve."""

    def __init__(self, path, max_bytes):
        if not RESERVE_BYTES * 2 <= max_bytes <= MAX_BYTES:
            raise PolicyError(f"max bytes must be in {RESERVE_BYTES * 2}..{MAX_BYTES}")
        self.path = Path(path)
        self.max_bytes = max_bytes
        self.bytes = 0
        self.files = {}
        self.parent_fd = trusted_parent_fd(self.path)
        self.directory_fd = None
        try:
            old_umask = os.umask(0o077)
            try:
                os.mkdir(self.path.name, 0o700, dir_fd=self.parent_fd)
            finally:
                os.umask(old_umask)
            self.directory_fd = os.open(self.path.name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                                        dir_fd=self.parent_fd)
            info = os.fstat(self.directory_fd)
            if info.st_uid != os.geteuid() or not stat.S_ISDIR(info.st_mode):
                raise PolicyError("new output directory has unexpected ownership/type")
            self.identity = (info.st_dev, info.st_ino)
            self.verify_identity()
            os.fchmod(self.directory_fd, 0o700)
            self.verify_identity()
        except BaseException:
            if self.directory_fd is not None:
                os.close(self.directory_fd)
            os.close(self.parent_fd)
            raise

    def verify_identity(self):
        named = os.stat(self.path.name, dir_fd=self.parent_fd, follow_symlinks=False)
        opened = os.fstat(self.directory_fd)
        if not stat.S_ISDIR(named.st_mode) or (named.st_dev, named.st_ino) != self.identity or (opened.st_dev, opened.st_ino) != self.identity:
            raise PolicyError("output directory was replaced during anchored creation/use")

    def write(self, name, record, final=False):
        if Path(name).name != name:
            raise PolicyError("output filename must be a basename")
        self.verify_identity()
        payload = (json.dumps(record, ensure_ascii=True, separators=(",", ":")) + "\n").encode("utf-8")
        limit = self.max_bytes if final else self.max_bytes - RESERVE_BYTES
        if self.bytes + len(payload) > limit:
            raise OutputLimit("total metadata output budget reached")
        if name not in self.files:
            fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=self.directory_fd)
            try:
                info = os.fstat(fd)
                named = os.stat(name, dir_fd=self.directory_fd, follow_symlinks=False)
                if not stat.S_ISREG(info.st_mode) or info.st_uid != os.geteuid() or (named.st_dev, named.st_ino) != (info.st_dev, info.st_ino):
                    raise PolicyError("new output file identity changed")
                os.fchmod(fd, 0o600)
            except BaseException:
                os.close(fd)
                raise
            self.files[name] = os.fdopen(fd, "wb", buffering=0)
        stream = self.files[name]
        view = memoryview(payload)
        while view:
            size = stream.write(view)
            if not size:
                raise OSError("short output write")
            self.bytes += size
            view = view[size:]

    def close(self):
        for stream in self.files.values():
            stream.close()
        os.close(self.directory_fd)
        os.close(self.parent_fd)


def stat_record(path, follow=False):
    return stat_value_record(os.stat(path, follow_symlinks=follow))


def stat_value_record(info):
    return {"device": {"major": os.major(info.st_dev), "minor": os.minor(info.st_dev)},
            "inode": info.st_ino, "mode": info.st_mode, "uid": info.st_uid, "gid": info.st_gid,
            "size": info.st_size, "mtime_ns": info.st_mtime_ns, "ctime_ns": info.st_ctime_ns,
            "nlink": info.st_nlink}


def proc_stat(text):
    # /proc/PID/stat comm can include spaces and ')' characters. No argv is read.
    close = text.rfind(")")
    if close < 0:
        raise ValueError("malformed proc stat")
    rest = text[close + 2:].split()
    if len(rest) < 20:
        raise ValueError("short proc stat")
    return {"pid": int(text[:text.index("(")].strip()), "comm": text[text.index("(") + 1:close],
            "state": rest[0], "ppid": int(rest[1]), "task_flags": int(rest[6]), "start_ticks": int(rest[19])}


def link_identity(path):
    """Bracket path and stat so a reused proc link cannot silently combine them."""
    first_path = os.readlink(path)
    first = stat_record(path, follow=True)
    second_path = os.readlink(path)
    second = stat_record(path, follow=True)
    if first_path != second_path or (first["device"], first["inode"]) != (second["device"], second["inode"]):
        raise ValueError("proc link identity changed during observation: " + str(path))
    return {"path": first_path, **first}


def fdinfo(path):
    details = {}
    with path.open() as stream:
        for line in stream:
            key, _, value = line.partition(":")
            if key in ("pos", "flags", "mnt_id", "ino"):
                details[key] = value.strip()
    return details


def snapshot_process(pid, emit, errors=None):
    errors = [] if errors is None else errors
    root = PROC_ROOT / str(pid)
    def problem(exc, **context):
        row = {"kind": "snapshot_error", "pid": pid, "error": type(exc).__name__, "detail": str(exc), **context}
        errors.append(row)
        emit(row)
    try:
        before = proc_stat((root / "stat").read_text())
    except (OSError, ValueError) as exc:
        problem(exc)
        return errors
    parent = None
    try:
        if before["ppid"]:
            parent = proc_stat((PROC_ROOT / str(before["ppid"]) / "stat").read_text())
    except (OSError, ValueError) as exc:
        problem(exc, observation="parent")
    emit({"kind": "process", **before, "parent": parent, "clock_ticks_per_second": os.sysconf("SC_CLK_TCK"),
          "start_identity_note": "proc ticks are rounded; kernel events carry exact group-leader boottime ns"})

    def observe_task(task_root, task_before):
        tid = task_before["pid"]
        scope = {"pid": pid, "tid": tid, "group_leader_start_ticks": before["start_ticks"],
                 "task_start_ticks": task_before["start_ticks"]}
        def observe_exe():
            try:
                return link_identity(task_root / "exe")
            except FileNotFoundError:
                if task_before["task_flags"] & 0x00200000 or task_before["state"] in ("Z", "X"):
                    return None
                raise
        executable = None
        try:
            executable = observe_exe()
        except (OSError, ValueError) as exc:
            problem(exc, tid=tid, observation="executable-before")
        emit({"kind": "task", **scope, "comm": task_before["comm"], "executable": executable})
        try:
            # Do not sort/exhaust this iterator: its own FD must remain open
            # until the live self-descriptor has actually been inspected.
            with os.scandir(task_root / "fd") as entries:
                for entry in entries:
                    if not entry.name.isdecimal():
                        continue
                    try:
                        path = Path(entry.path)
                        first = link_identity(path)
                        first_info = fdinfo(task_root / "fdinfo" / entry.name)
                        second = link_identity(path)
                        second_info = fdinfo(task_root / "fdinfo" / entry.name)
                        if (first["path"], first["device"], first["inode"], first_info) != (second["path"], second["device"], second["inode"], second_info):
                            raise ValueError("descriptor path/stat/fdinfo changed during observation")
                        if "ino" in first_info and int(first_info["ino"]) != first["inode"]:
                            raise ValueError("descriptor fdinfo inode disagrees with stat")
                    except (OSError, ValueError) as exc:
                        problem(exc, tid=tid, fd=int(entry.name), observation="descriptor")
                        continue
                    emit({"kind": "descriptor", **scope, "fd": int(entry.name), "path": first.pop("path"),
                          "metadata": first, "fd_metadata": first_info, "identity_rechecked": True})
        except OSError as exc:
            problem(exc, tid=tid, observation="descriptor-table")
        try:
            with (task_root / "maps").open() as stream:
                for line in stream:
                    try:
                        parts = line.split(None, 5)
                        if len(parts) < 5:
                            raise ValueError("malformed proc maps")
                        if len(parts[1]) != 4 or parts[1][3] != "s":
                            continue
                        major, minor = (int(part, 16) for part in parts[3].split(":"))
                        row = {"kind": "shared_mapping", **scope, "range": parts[0], "permissions": parts[1],
                               "offset": parts[2], "device": {"major": major, "minor": minor},
                               "inode": int(parts[4]), "path": parts[5].rstrip("\n") if len(parts) == 6 else ""}
                    except ValueError as exc:
                        problem(exc, tid=tid, observation="shared-mapping")
                        continue
                    emit(row)
        except OSError as exc:
            problem(exc, tid=tid, observation="shared-mappings")
        try:
            final_exe = observe_exe()
            exe_token = lambda item: None if item is None else (item["path"], item["device"], item["inode"])
            if exe_token(executable) != exe_token(final_exe):
                raise ValueError("executable identity changed across task snapshot")
            task_after = proc_stat((task_root / "stat").read_text())
            if (tid, task_before["start_ticks"]) != (task_after["pid"], task_after["start_ticks"]):
                raise ValueError("task reused during snapshot")
        except (OSError, ValueError) as exc:
            problem(exc, tid=tid, observation="task-final-identity")
    try:
        with os.scandir(root / "task") as tasks:
            for task in tasks:
                if not task.name.isdecimal():
                    continue
                try:
                    task_root = Path(task.path)
                    task_before = proc_stat((task_root / "stat").read_text())
                except (OSError, ValueError) as exc:
                    problem(exc, tid=int(task.name), observation="task-identity")
                    continue
                observe_task(task_root, task_before)
        after = proc_stat((root / "stat").read_text())
        if (before["pid"], before["start_ticks"]) != (after["pid"], after["start_ticks"]):
            raise ValueError("process reused during snapshot")
    except (OSError, ValueError) as exc:
        problem(exc, observation="process-final-identity")
    return errors


def snapshot_all(output, phase, nss_dir, deadline_ns=None, pump=None, state=None):
    state = {} if state is None else state
    errors = state.setdefault("errors", [])
    state.update(started=True, completed=False, partial=True)
    try:
        result = snapshot_inventory(output, phase, nss_dir, errors, deadline_ns, pump)
        state.update(completed=True, partial=False)
        return result
    except Exception as exc:
        state["failure"] = type(exc).__name__ + ": " + str(exc)
        errors.append({"kind": "snapshot_error", "phase": phase, "detail": state["failure"]})
        raise
    finally:
        state["error_count"] = len(errors)


def snapshot_inventory(output, phase, nss_dir, errors, deadline_ns=None, pump=None):
    name = phase + ".jsonl"
    def emit(value):
        if deadline_ns is not None and time.monotonic_ns() >= deadline_ns:
            raise TimeoutError("capture deadline elapsed during setup snapshot")
        if pump is not None:
            pump()
        output.write(name, {"observed_monotonic_ns": time.monotonic_ns(), **value})
    emit({"kind": "snapshot_start", "phase": phase})
    pids = sorted(int(item.name) for item in PROC_ROOT.iterdir() if item.name.isdecimal())
    for pid in pids:
        try:
            snapshot_process(pid, emit, errors)
        except (OSError, ValueError) as exc:
            error = {"kind": "snapshot_error", "pid": pid, "error": type(exc).__name__, "detail": str(exc)}
            errors.append(error)
            emit(error)
    nss_safe = True
    nss_identities = {}
    for path in (nss_dir.parent, nss_dir):
        try:
            metadata = stat_record(path)
            nss_identities[path] = (metadata["device"], metadata["inode"])
            emit({"kind": "nss_metadata", "path": str(path), **metadata})
            if not stat.S_ISDIR(metadata["mode"]):
                raise ValueError("NSS root/parent is a symlink or non-directory; traversal refused")
        except (OSError, ValueError) as exc:
            nss_safe = False
            error = {"kind": "snapshot_error", "path": str(path), "detail": str(exc)}
            errors.append(error)
            emit(error)
    def walk_error(exc):
        error = {"kind": "snapshot_error", "detail": str(exc)}
        errors.append(error)
        emit(error)
    if nss_safe:
        parent_fd = root_fd = None
        try:
            parent_fd = os.open(nss_dir.parent, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
            root_fd = os.open(nss_dir.name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent_fd)
            root_identity = os.fstat(root_fd)
            for path, anchor_fd in ((nss_dir.parent, parent_fd), (nss_dir, root_fd)):
                anchored = stat_value_record(os.fstat(anchor_fd))
                if (anchored["device"], anchored["inode"]) != nss_identities[path]:
                    raise ValueError("NSS root/parent identity changed before anchored traversal")
            for directory, dirs, files, directory_fd in os.fwalk(".", dir_fd=root_fd, follow_symlinks=False, onerror=walk_error):
                for leaf in sorted(dirs + files):
                    path = nss_dir / directory / leaf
                    try:
                        info = os.stat(leaf, dir_fd=directory_fd, follow_symlinks=False)
                        entry = {"kind": "nss_metadata", "path": str(path), **stat_value_record(info)}
                        if stat.S_ISLNK(info.st_mode):
                            entry["symlink_target"] = os.readlink(leaf, dir_fd=directory_fd)
                            errors.append({"kind": "snapshot_error", "path": str(path), "detail": "NSS symlink alias unsupported"})
                        emit(entry)
                    except OSError as exc:
                        walk_error(exc)
            named = os.stat(nss_dir.name, dir_fd=parent_fd, follow_symlinks=False)
            if not stat.S_ISDIR(named.st_mode) or (named.st_dev, named.st_ino) != (root_identity.st_dev, root_identity.st_ino):
                raise ValueError("NSS root identity changed during traversal")
            named_parent, opened_parent = os.stat(nss_dir.parent, follow_symlinks=False), os.fstat(parent_fd)
            if not stat.S_ISDIR(named_parent.st_mode) or (named_parent.st_dev, named_parent.st_ino) != (opened_parent.st_dev, opened_parent.st_ino):
                raise ValueError("NSS parent identity changed during traversal")
        except (OSError, ValueError) as exc:
            walk_error(exc)
        finally:
            if root_fd is not None:
                os.close(root_fd)
            if parent_fd is not None:
                os.close(parent_fd)
    emit({"kind": "snapshot_end", "phase": phase, "error_count": len(errors)})
    return errors


def mount_record(nss_dir):
    """Retain only mount metadata; no file contents or mount-option secrets."""
    matches = []
    for line in Path("/proc/self/mountinfo").read_text().splitlines():
        fields = line.split()
        separator = fields.index("-")
        # Kernel escapes whitespace and backslashes in mountinfo path fields.
        decode = lambda s: re.sub(r"\\([0-7]{3})", lambda m: chr(int(m[1], 8)), s)
        mountpoint = Path(decode(fields[4]))
        if nss_dir == mountpoint or mountpoint in nss_dir.parents:
            matches.append({"mount_id": int(fields[0]), "parent_mount_id": int(fields[1]),
                            "device_text": fields[2], "root": decode(fields[3]),
                            "mountpoint": str(mountpoint), "filesystem": fields[separator + 1]})
    if not matches:
        raise PolicyError("cannot identify NSS mount")
    return max(matches, key=lambda row: len(row["mountpoint"]))


def namespace_check(ack):
    if os.geteuid() != 0:
        raise PolicyError("capture requires root in the host PID and mount namespaces")
    if not ack:
        raise PolicyError("capture requires --host-namespaces-confirmed after host namespace verification")
    result = {}
    for name in ("pid", "mnt"):
        own = os.readlink("/proc/self/ns/" + name)
        init = os.readlink("/proc/1/ns/" + name)
        if own != init:
            raise PolicyError("collector namespace differs from host /proc/1: " + name)
        result[name] = own
    return result


def owner_identity(owner_pid):
    if owner_pid <= 1 or owner_pid == os.getpid():
        raise PolicyError("owner PID must identify the existing caller shell, greater than 1")
    owner = proc_stat((Path("/proc") / str(owner_pid) / "stat").read_text())
    cursor = os.getppid()
    for _ in range(64):
        if cursor == owner_pid:
            return {"pid": owner_pid, "start_ticks": owner["start_ticks"], "comm": owner["comm"]}
        if cursor <= 1:
            break
        cursor = proc_stat((Path("/proc") / str(cursor) / "stat").read_text())["ppid"]
    raise PolicyError("owner PID must be an ancestor of this collector")


def owner_alive(owner):
    try:
        current = proc_stat((Path("/proc") / str(owner["pid"]) / "stat").read_text())
        return current["start_ticks"] == owner["start_ticks"] and current["state"] != "Z"
    except OSError:
        return False


def deadline_elapsed(monotonic_deadline, boot_deadline):
    return time.monotonic_ns() >= monotonic_deadline or time.clock_gettime_ns(time.CLOCK_BOOTTIME) >= boot_deadline


def diagnostic_provenance():
    observed = {"collector_python_source_sha256": None, "bpf_template_sha256": None,
                "rendered_bpf_sha256": None, "bcc_version": None, "bcc_version_observation": "not imported",
                "python_version": platform.python_version(), "python_executable": sys.executable,
                "source_observation": "current on-disk diagnostic sources at capture setup; Python may have loaded earlier",
                "errors": []}
    template = None
    for filename, key in (("collector.py", "collector_python_source_sha256"), ("trace.bpf.c", "bpf_template_sha256")):
        try:
            data = (HERE / filename).read_bytes()
            observed[key] = hashlib.sha256(data).hexdigest()
            if filename == "trace.bpf.c":
                template = data.decode("utf-8")
        except (OSError, UnicodeError) as exc:
            observed["errors"].append(filename + ": " + str(exc))
    return observed, template


def owned_return_names(bpf):
    """BCC maxactive>0 forces tracefs kretprobes with process-suffixed aliases."""
    keys = {os.fsdecode(key) for key in bpf.kprobe_fds}
    names = []
    for symbol, _, _, _ in HOOKS:
        key = "r_" + symbol.replace(".", "_").replace("+", "_")
        if key not in keys:
            raise PolicyError("BCC owned return event key unavailable: " + key)
        names.append(f"{key}_bcc_{os.getpid()}")
    return names


def profile_accounting(text, owned_names):
    """Require every owned kretprobe row; missing/ambiguous rows are unknown."""
    rows = {}
    errors = []
    for line in text.splitlines():
        fields = line.split()
        if not fields or fields[0] not in owned_names:
            continue
        if len(fields) != 3 or not fields[1].isdecimal() or not fields[2].isdecimal() or fields[0] in rows:
            errors.append("invalid/duplicate owned profile row: " + fields[0])
            continue
        rows[fields[0]] = {"hits": int(fields[1]), "missed": int(fields[2])}
    for name in owned_names:
        if name not in rows:
            errors.append("missing owned kretprobe profile: " + name)
        elif rows[name]["missed"]:
            errors.append("owned kretprobe missed instances: " + name)
    return {"owned_rows": rows, "errors": errors, "complete": not errors}


def read_profile(owned_names):
    errors = []
    for root in (Path("/sys/kernel/tracing"), Path("/sys/kernel/debug/tracing")):
        try:
            # Read-only: do not mount tracefs or change global tracing state.
            text = (root / "kprobe_profile").read_text()
            return {"path": str(root / "kprobe_profile"), **profile_accounting(text, owned_names)}
        except OSError as exc:
            errors.append(str(exc))
    return {"owned_rows": {}, "complete": False, "errors": errors}


def loss_reasons(counters, perf_lost, unfinished, profile):
    reasons = []
    for key in ("submission_failures", "context_map_failures", "context_overwrites", "unmatched_returns",
                "kernel_read_errors", "path_truncations", "deadline_unfinished_pairs", "identity_errors"):
        if key not in counters:
            reasons.append("missing kernel counter: " + key)
        elif counters[key]:
            reasons.append(f"{key}: {counters[key]}")
    if perf_lost:
        reasons.append(f"perf lost events: {perf_lost}")
    if unfinished:
        reasons.append(f"unfinished entry/return pairs: {unfinished}")
    if not profile.get("complete"):
        reasons.append("owned kretprobe missed accounting incomplete")
    return reasons


def capture(args):
    validate_duration(args.duration)
    output_path, nss_dir = validate_output(args.output, args.nss_dir)
    namespaces = namespace_check(args.host_namespaces_confirmed)
    owner = owner_identity(args.owner_pid)
    report = dependency_report()
    if report["missing"]:
        raise PolicyError("; ".join(report["missing"]))
    # Count setup inside the kernel-enforced window, including output creation,
    # compilation, attachments, and before-snapshot work.
    started_ns = time.monotonic_ns()
    started_boot_ns = time.clock_gettime_ns(time.CLOCK_BOOTTIME)
    deadline_ns = started_ns + args.duration * 1_000_000_000
    boot_deadline_ns = started_boot_ns + args.duration * 1_000_000_000
    output = BoundedOutput(output_path, args.max_bytes)
    bpf = None
    stop = {"requested": False, "signal": None, "overflow": False}
    errors = []
    uncertainties = set()
    snapshot_states = {phase: {"started": False, "completed": False, "partial": False, "errors": [], "error_count": 0}
                       for phase in ("before", "after")}
    before_errors, after_errors = snapshot_states["before"]["errors"], snapshot_states["after"]["errors"]
    counters, profile, unfinished = {}, {"complete": False, "errors": ["not attached"]}, 0
    stats = {"events": 0, "successful_mutation_records": 0, "perf_lost": 0}
    ready = False
    cleanup_ok = False
    calibration = clock_sample()
    initial_calibration = dict(calibration)
    gate_close = None
    boot_id = Path("/proc/sys/kernel/random/boot_id").read_text().strip()
    old_handlers = {}
    provenance, template = diagnostic_provenance()

    def handle_signal(number, frame):
        stop["requested"] = True
        stop["signal"] = number

    def event_callback(cpu, data, size):
        if stop["overflow"]:
            return
        try:
            if size != ctypes.sizeof(Event):
                uncertainties.add("event ABI size mismatch")
                stop["requested"] = True
                return
            event = Event.from_buffer_copy(ctypes.string_at(data, size))
            if not started_ns <= event.entry_ns <= event.return_ns < deadline_ns:
                uncertainties.add("event outside kernel monotonic deadline/order")
                stop["requested"] = True
            if not started_boot_ns <= event.entry_boot_ns <= event.return_boot_ns < boot_deadline_ns:
                uncertainties.add("event outside suspend-aware kernel boottime deadline/order")
                stop["requested"] = True
            record = decode_event(event, boot_id, calibration)
            uncertainties.update(event_uncertainties(record))
            output.write("events.jsonl", {"cpu": cpu, **record})
            stats["events"] += 1
            stats["successful_mutation_records"] += int(record["successful_mutation"])
        except OutputLimit:
            stop.update(requested=True, overflow=True)
        except Exception as exc:
            uncertainties.add("event decode/write failure: " + type(exc).__name__ + ": " + str(exc))
            stop["requested"] = True

    def lost_callback(cpu, lost):
        stats["perf_lost"] += int(lost)

    def sample_clock_if_due():
        nonlocal calibration
        if time.monotonic_ns() - calibration["monotonic_ns"] >= 1_000_000_000:
            sample = clock_sample()
            if abs((sample["utc_ns"] - sample["monotonic_ns"]) -
                   (calibration["utc_ns"] - calibration["monotonic_ns"])) > 1_000_000:
                uncertainties.add("UTC/monotonic clock step or drift exceeded 1ms")
            calibration = sample
            output.write("clock.jsonl", sample)

    try:
        for number in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP, signal.SIGQUIT):
            old_handlers[number] = signal.signal(number, handle_signal)
        metadata = {"schema": "story117-nss-writer-v1", "frozen": FROZEN, "unsupported": UNSUPPORTED,
                    "boot_id": boot_id, "kernel_release": platform.release(), "namespaces": namespaces,
                    "started_monotonic_ns": started_ns, "deadline_monotonic_ns": deadline_ns,
                    "started_boottime_ns": started_boot_ns, "deadline_boottime_ns": boot_deadline_ns,
                    "duration_including_setup_seconds": args.duration, "nss_dir": str(nss_dir),
                    "output_dir": str(output_path), "maximum_output_bytes": args.max_bytes,
                    "caller_shell": owner,
                    "diagnostic_provenance": dict(provenance),
                    "clock_sample": calibration, "nss_mount": mount_record(nss_dir),
                    "hook_contract": [list(row) for row in HOOKS], "event_abi_bytes": ctypes.sizeof(Event)}
        if metadata["nss_mount"]["filesystem"] != "ext4":
            uncertainties.add("NSS filesystem is unsupported: " + metadata["nss_mount"]["filesystem"])
        output.write("metadata.json", metadata)
        out_stat = os.fstat(output.directory_fd)
        if template is None:
            raise PolicyError("observed BPF source unavailable; cannot bind compiled source provenance")
        source = render_source(deadline_ns, kernel_device(out_stat.st_dev), out_stat.st_ino, boot_deadline_ns, template)
        provenance["rendered_bpf_sha256"] = hashlib.sha256(source.encode("utf-8")).hexdigest()
        # The only BCC import is below the explicit capture policy and dependency gates.
        import bcc
        BPF = bcc.BPF
        version = getattr(bcc, "__version__", None)
        provenance["bcc_version"] = version if isinstance(version, str) else None
        provenance["bcc_version_observation"] = "module __version__" if isinstance(version, str) else "unknown; module exposes no string __version__"
        output.write("provenance.json", provenance)
        bpf = BPF(text=source)
        bpf["events"].open_perf_buffer(event_callback, lost_cb=lost_callback, page_cnt=64)
        for event, function in (("sched_process_fork", "raw_fork"), ("sched_process_exec", "raw_exec"),
                                ("sched_process_exit", "raw_exit")):
            bpf.attach_raw_tracepoint(tp=event, fn_name=function)
        for symbol, entry, returned, _ in HOOKS:
            if deadline_elapsed(deadline_ns, boot_deadline_ns) or stop["requested"]:
                raise TimeoutError("setup deadline/signal before mandatory probe attachment completed")
            bpf.attach_kprobe(event=symbol, fn_name=entry)
            bpf.attach_kretprobe(event=symbol, fn_name=returned, maxactive=1024)
        owned = owned_return_names(bpf)
        profile = read_profile(owned)
        if not profile["complete"]:
            raise PolicyError("mandatory owned kretprobe accounting unavailable: " + "; ".join(profile["errors"]))
        trace_started_ns = time.monotonic_ns()
        bpf["enabled"][ctypes.c_int(0)] = ctypes.c_uint64(1)
        output.write("trace-start.json", {"earliest_possible_trace_monotonic_ns": trace_started_ns,
                                         "gate_enabled_by_monotonic_ns": time.monotonic_ns()})
        def pump():
            if deadline_elapsed(deadline_ns, boot_deadline_ns):
                raise TimeoutError("monotonic/boottime deadline elapsed during setup snapshot")
            if stop["requested"]:
                raise InterruptedError("stop requested during setup snapshot")
            if not owner_alive(owner):
                raise InterruptedError("caller shell exited or PID was reused")
            bpf.perf_buffer_poll(timeout=0)
            sample_clock_if_due()
            if stop["overflow"]:
                raise OutputLimit("event stream reached output budget during snapshot")
        snapshot_all(output, "before", nss_dir, deadline_ns, pump, state=snapshot_states["before"])
        if before_errors:
            uncertainties.add("before snapshot incomplete")
        if deadline_elapsed(deadline_ns, boot_deadline_ns) or stop["requested"]:
            raise TimeoutError("setup consumed capture deadline or was signalled; readiness refused")
        output.write("ready.json", {"pid": os.getpid(), "start_ticks": proc_stat(Path("/proc/self/stat").read_text())["start_ticks"],
                                   "caller_shell": owner,
                                   "boot_id": boot_id, "ready_monotonic_ns": time.monotonic_ns(),
                                   "deadline_monotonic_ns": deadline_ns, "mandatory_hooks_attached": True,
                                   "deadline_boottime_ns": boot_deadline_ns,
                                   "before_snapshot_complete": not before_errors,
                                   "qualification": "incomplete", "global_immutability_proven": False})
        ready = True
        while not stop["requested"] and not deadline_elapsed(deadline_ns, boot_deadline_ns):
            if not owner_alive(owner):
                uncertainties.add("caller shell exited or PID was reused")
                break
            remaining_ms = max(1, min(100, (deadline_ns - time.monotonic_ns()) // 1_000_000))
            bpf.perf_buffer_poll(timeout=int(remaining_ms))
            sample_clock_if_due()
        # Kernel deadline prevents new records, independently of Python teardown.
        # For early signals, disable event generation by setting the map gate below
        # before snapshots; this is added to kernel time checks in the C program.
    except OutputLimit as exc:
        stop.update(requested=True, overflow=True)
        errors.append(str(exc))
    except Exception as exc:
        errors.append(type(exc).__name__ + ": " + str(exc))
    finally:
        if bpf is not None:
            try:
                gate_close = {"earliest_monotonic_ns": time.monotonic_ns(),
                              "earliest_boottime_ns": time.clock_gettime_ns(time.CLOCK_BOOTTIME)}
                bpf["enabled"][ctypes.c_int(0)] = ctypes.c_uint64(0)
                gate_close["completed_monotonic_ns"] = time.monotonic_ns()
                gate_close["completed_boottime_ns"] = time.clock_gettime_ns(time.CLOCK_BOOTTIME)
                # Drain queued records before the after snapshot; no new probe
                # entries/lifecycle events are admitted after gate closure.
                for _ in range(3):
                    bpf.perf_buffer_poll(timeout=1)
            except Exception as exc:
                errors.append("stop/drain: " + str(exc))
            try:
                for index, key in enumerate(COUNTERS):
                    counters[key] = int(bpf["counters"][ctypes.c_int(index)].value)
                unfinished = sum(1 for _ in bpf["pairs"].items())
                profile = read_profile(owned_return_names(bpf))
            except Exception as exc:
                errors.append("kernel accounting: " + str(exc))
            try:
                bpf.cleanup()  # only this BCC instance's probes/maps/perf buffers
                cleanup_ok = True
            except Exception as exc:
                errors.append("BCC cleanup: " + str(exc))
        if not stop["overflow"]:
            try:
                snapshot_all(output, "after", nss_dir, state=snapshot_states["after"])
            except Exception as exc:
                errors.append("after snapshot: " + str(exc))
        else:
            errors.append("after snapshot not completed because output budget was reached")
            snapshot_states["after"]["failure"] = "skipped because output budget was reached"
        for number, previous in old_handlers.items():
            signal.signal(number, previous)
        uncertainties.update(loss_reasons(counters, stats["perf_lost"], unfinished, profile))
        if counters.get("submissions") != stats["events"] + stats["perf_lost"] + counters.get("submission_failures", 0):
            uncertainties.add("submission/delivery/loss counts do not reconcile")
        if counters.get("paired_entries") != counters.get("paired_returns", 0) + unfinished:
            uncertainties.add("entry/return/context counts do not reconcile")
        for phase, snapshot in snapshot_states.items():
            if snapshot["errors"] or not snapshot["completed"]:
                uncertainties.add(phase + " snapshot incomplete")
        if provenance["errors"] or not provenance["collector_python_source_sha256"] or not provenance["rendered_bpf_sha256"]:
            uncertainties.add("diagnostic source provenance incomplete")
        if stop["overflow"]:
            uncertainties.add("output overflow; collection stopped")
        if stop["signal"] is not None:
            uncertainties.add("capture stopped by signal")
        if not ready:
            uncertainties.add("readiness not reached")
        if not cleanup_ok:
            uncertainties.add("BCC cleanup not verified")
        if errors:
            uncertainties.add("collector errors")
        final_clock = clock_sample()
        offset_change = (final_clock["utc_ns"] - final_clock["monotonic_ns"]) - (initial_calibration["utc_ns"] - initial_calibration["monotonic_ns"])
        if abs(offset_change) > 1_000_000:
            uncertainties.add("UTC/monotonic calibration drift exceeded 1ms")
        receipt = {"schema": "story117-nss-writer-receipt-v1", "frozen": FROZEN, "ready": ready,
                   "complete": False, "global_immutability_proven": False,
                   "supported_probe_accounting_complete": ready and not uncertainties,
                   "attribution_requires": "Successful target mutation + device/inode/path + event-time executable/start identity + fork/exec correlation + loss accounting + before/after metadata; manual evidence assessment required.",
                   "unsupported": UNSUPPORTED, "incomplete_reasons": [s[:256] for s in sorted(uncertainties)[:64]],
                   "incomplete_reason_count": len(uncertainties), "errors": [s[:256] for s in errors[:32]],
                   "stats": stats, "kernel_counters": counters, "unfinished_pairs": unfinished,
                   "kretprobe_profile_before_detach": profile, "collector_only_cleanup_completed": cleanup_ok,
                   "stop": stop, "final_clock_sample": final_clock, "utc_offset_change_ns": offset_change,
                   "event_gate_close": gate_close,
                   "diagnostic_provenance": provenance,
                   "finished_monotonic_ns": time.monotonic_ns(), "bytes_before_receipt": output.bytes,
                   "snapshot_error_counts": {"before": len(before_errors), "after": len(after_errors)},
                   "snapshot_phases": {phase: {**{key: value[:512] if isinstance(value, str) else value for key, value in snapshot.items() if key != "errors"},
                                                "errors_sample": [{key: str(value)[:256] for key, value in error.items()} for error in snapshot["errors"][:4]]}
                                       for phase, snapshot in snapshot_states.items()}}
        try:
            output.write("receipt.json", receipt, final=True)
        finally:
            output.close()
    print(json.dumps({"output": str(output_path), "ready": ready, "complete": False,
                      "supported_probe_accounting_complete": receipt["supported_probe_accounting_complete"],
                      "qualification": "incomplete"}, sort_keys=True))
    return 0 if receipt["supported_probe_accounting_complete"] else 2


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    sub = result.add_subparsers(dest="mode")
    sub.add_parser("check", help="offline dependency report; never imports/attaches BCC")
    render = sub.add_parser("render", help="print rendered C only; never compile or attach")
    render.add_argument("--deadline-ns", type=int, default=0, help="monotonic constant; 0 disables every event")
    render.add_argument("--boot-deadline-ns", type=int, default=0, help="suspend-aware boottime constant; 0 disables every event")
    capture_parser = sub.add_parser("capture", help="EXPLICIT OPT-IN ONLY: host-wide metadata probes")
    capture_parser.add_argument("--nss-dir", type=Path, required=True)
    capture_parser.add_argument("--output", type=Path, required=True, help="new directory outside NSS parent")
    capture_parser.add_argument("--duration", type=int, default=120, help="1..900 seconds INCLUDING setup")
    capture_parser.add_argument("--max-bytes", type=int, default=64 * 1024 * 1024)
    capture_parser.add_argument("--owner-pid", type=int, required=True, help="existing caller shell PID; collector stops if it exits")
    capture_parser.add_argument("--host-namespaces-confirmed", action="store_true", help="operator confirms actual host PID/mount namespaces")
    return result


def main(argv=None):
    args_parser = parser()
    args = args_parser.parse_args(argv)
    try:
        if args.mode is None:
            args_parser.print_help()
            return 0
        if args.mode == "check":
            report = dependency_report()
            print(json.dumps(report, indent=2, sort_keys=True))
            return 2 if report["missing"] else 0
        if args.mode == "render":
            print(render_source(args.deadline_ns, boot_deadline_ns=args.boot_deadline_ns), end="")
            return 0
        return capture(args)
    except (PolicyError, OSError) as exc:
        print(type(exc).__name__ + ": " + str(exc), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
