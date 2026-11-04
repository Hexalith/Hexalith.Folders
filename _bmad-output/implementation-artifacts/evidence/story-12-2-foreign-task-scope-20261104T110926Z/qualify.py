"""Run exact Story 12.2 commands and retain only metadata in portable evidence."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import time
from datetime import datetime, timezone
import xml.etree.ElementTree as ET

ROOT = Path("/home/administrator/projects/hexalith/folders")
OUT = Path(__file__).resolve().parent
HEX_ROOTS = [ROOT, *sorted((ROOT / "references").glob("Hexalith.*"))]


def now():
    return datetime.now(timezone.utc).isoformat()


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")
    path.chmod(0o600)


def query(argv, cwd=ROOT):
    result = subprocess.run(argv, cwd=cwd, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        return {"exit_code": result.returncode, "stderr": result.stderr[:2000]}
    return result.stdout.strip()


def sources():
    rows = []
    for root in HEX_ROOTS:
        if not (root / ".git").exists():
            continue
        tracked = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root).decode().split("\0")
        entries = {}
        for name in tracked:
            if not name or name.startswith(("references/", "_bmad", ".agents/", ".claude/", ".github/skills/", "docs/evidence/", "eng/package-cohort/qualification/")):
                continue
            path = root / name
            if path.is_file() and path.suffix in {".cs", ".csproj", ".props", ".targets", ".slnx", ".json", ".yaml", ".yml", ".md", ".config"}:
                entries[name] = sha(path)
        dirty = query(["git", "status", "--porcelain=v1", "--untracked-files=normal"], root)
        rows.append({"root": str(root), "head": query(["git", "rev-parse", "HEAD"], root),
                     "branch": query(["git", "branch", "--show-current"], root),
                     "status": dirty, "sources": entries})
    return rows


def containers():
    raw = query(["docker", "ps", "--no-trunc", "--format", "{{json .}}"])
    if not isinstance(raw, str):
        return raw
    return [{key: row.get(key) for key in ("ID", "Names", "Image", "Status")}
            for row in [json.loads(line) for line in raw.splitlines() if line]]


def directories():
    return sorted(str(p) for pattern in ("folders-durability-*", "hexalith-dapr/*", "hexalith-development-catalog-*")
                  for p in Path("/tmp").glob(pattern) if p.is_dir())


def snapshot(label):
    result = {"utc": now(), "repos": sources(), "containers": containers(), "directories": directories()}
    write(OUT / (label + ".json"), result)
    print(json.dumps({"snapshot": label, "repositories": len(result["repos"])}))


def process_tree(root_pid):
    pending = [root_pid]
    seen = set()
    while pending:
        pid = pending.pop()
        if pid in seen:
            continue
        seen.add(pid)
        try:
            pending.extend(int(x) for x in Path(f"/proc/{pid}/task/{pid}/children").read_text().split())
        except (OSError, ValueError):
            pass
    return seen


def stat_identity(pid):
    try:
        content = Path(f"/proc/{pid}/stat").read_text()
        return content[content.rfind(")") + 2:].split()[19]
    except (OSError, IndexError):
        return None


def mapped_modules(pids, seen):
    rows = []
    for pid in pids:
        try:
            lines = Path(f"/proc/{pid}/maps").read_text().splitlines()
            start_id = stat_identity(pid)
            paths = sorted(set(line[line.index("/"):] for line in lines if "/" in line))
            if not any("Hexalith." in p and p.endswith(".dll") for p in paths):
                continue
            for name in paths:
                if not (name.endswith(".dll") or ".so" in Path(name).name):
                    continue
                key = (pid, start_id, name)
                if key in seen:
                    continue
                path = Path(name)
                if not path.is_file():
                    continue
                row = {"pid": pid, "start_id": start_id, "path": name, "sha256": sha(path)}
                pdb = path.with_suffix(".pdb")
                if name.endswith(".dll") and pdb.is_file():
                    row.update(pdb_path=str(pdb), pdb_sha256=sha(pdb))
                seen.add(key)
                rows.append(row)
        except (OSError, PermissionError):
            continue
    return rows


def sanitize_xml(path):
    root = ET.parse(path).getroot()
    result = {"assemblies": [], "tests": []}
    for assembly in root.iter("assembly"):
        result["assemblies"].append({k: v for k, v in assembly.attrib.items()
                                     if k in {"name", "total", "passed", "failed", "skipped", "errors", "time", "run-date", "run-time"}})
    for test in root.iter("test"):
        result["tests"].append({k: v for k, v in test.attrib.items()
                               if k in {"name", "type", "method", "result", "time"}})
    return result


def run(label, argv, port=None):
    os.umask(0o077)
    before = {"utc": now(), "containers": containers(), "directories": directories(), "repos": sources()}
    write(OUT / (label + "-before.json"), before)
    env = dict(os.environ)
    if port is not None:
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", port))
        env["Folders__EventStoreDaprHttpPort"] = str(port)
        env["HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION"] = "true"
    raw = OUT / (label + "-private.log")
    raw.touch(mode=0o600, exist_ok=False)
    xml = OUT / (label + "-private.xml")
    started = now()
    runtime = []
    seen_modules = set()
    seen_pids = {}
    executables = {}
    containers_seen = {}
    dirs_seen = set()
    with raw.open("wb") as log:
        process = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        while process.poll() is None:
            pids = process_tree(process.pid)
            for pid in pids:
                seen_pids.setdefault(str(pid), stat_identity(pid))
                try:
                    executable = Path(f"/proc/{pid}/exe").resolve(strict=True)
                    key = (pid, stat_identity(pid), str(executable))
                    if key not in executables and executable.is_file():
                        executables[key] = {"pid": pid, "start_id": key[1], "path": str(executable), "sha256": sha(executable)}
                except OSError:
                    pass
            runtime.extend(mapped_modules(pids, seen_modules))
            current_containers = containers()
            for row in current_containers if isinstance(current_containers, list) else []:
                if row["ID"] not in containers_seen:
                    row["image_id"] = query(["docker", "inspect", "--format", "{{.Image}}", row["ID"]])
                    containers_seen[row["ID"]] = row
            dirs_seen.update(directories())
            time.sleep(1)
        code = process.returncode
    # A fixture may report modules that appeared between the periodic observations.
    bindings = []
    observations = []
    raw_text = raw.read_text(errors="replace")
    for line in raw_text.splitlines():
        dirs_seen.update(re.findall(r"/tmp/hexalith-dapr/[a-f0-9]{32}", line))
        match = re.search(r"RUNTIME_BINDING (\{.*\})", line)
        if match:
            try:
                bindings.append(json.loads(match.group(1)))
            except json.JSONDecodeError:
                pass
        match = re.search(r"(PASS [^\r\n]*|PERFORMANCE [^\r\n]*|BOOTSTRAP_VERIFIED [^\r\n]*)", line)
        if match:
            observations.append(match.group(1))
    after = {"utc": now(), "repos": sources(), "containers": containers(), "directories": directories()}
    before_by_root = {r["root"]: r for r in before["repos"]}
    drift = []
    status_changes = []
    for repo in after["repos"]:
        old = before_by_root.get(repo["root"])
        if old is None or old["head"] != repo["head"] or old["sources"] != repo["sources"]:
            drift.append(repo["root"])
        if old is not None and old["status"] != repo["status"]:
            status_changes.append(repo["root"])
    write(OUT / (label + "-after.json"), after)
    write(OUT / (label + "-runtime.json"), {"poll_observations": runtime, "fixture_bindings": bindings,
          "executables": list(executables.values()), "containers": list(containers_seen.values())})
    (OUT / (label + "-observations.log")).write_text("\n".join(observations) + "\n")
    remaining_pids = [int(pid) for pid, birth in seen_pids.items() if birth and stat_identity(int(pid)) == birth]
    after_ids = {r["ID"] for r in after["containers"]} if isinstance(after["containers"], list) else set()
    before_ids = {r["ID"] for r in before["containers"]} if isinstance(before["containers"], list) else set()
    new_remaining = [row for key, row in containers_seen.items() if key not in before_ids and key in after_ids]
    remaining_containers = [row for row in new_remaining if row["Names"] in raw_text]
    unattributed_containers = [row for row in new_remaining if row not in remaining_containers]
    after_dirs = set(after["directories"])
    leftover_dirs = sorted((dirs_seen - set(before["directories"])) & after_dirs)
    runtime_drift = []
    for row in runtime:
        path = Path(row["path"])
        if path.is_file() and sha(path) != row["sha256"]:
            runtime_drift.append(row["path"])
    summary = sanitize_xml(xml) if xml.exists() else None
    result = {"argv": argv, "cwd": str(ROOT), "utc_start": started, "utc_end": now(), "exit_code": code,
              "environment_selection": {} if port is None else {"Folders__EventStoreDaprHttpPort": port, "HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION": "true"},
              "private_log": {"path": str(raw), "sha256": sha(raw)},
              "private_xml": {"path": str(xml), "sha256": sha(xml)} if xml.exists() else None,
              "summary": summary, "source_or_head_drift": drift, "working_tree_status_changes": status_changes,
              "runtime_file_drift": sorted(set(runtime_drift)),
              "cleanup": {"remaining_observed_descendants": remaining_pids, "remaining_owned_containers": remaining_containers,
                          "remaining_unattributed_containers": unattributed_containers,
                          "remaining_observed_directories": leftover_dirs},
              "runtime_module_observations": len(runtime), "fixture_module_observations": len(bindings)}
    write(OUT / (label + "-receipt.json"), result)
    print(json.dumps({"label": label, "exit_code": code, "summary": None if summary is None else summary["assemblies"],
                      "runtime_modules": len(runtime), "fixture_modules": len(bindings), "source_drift": drift,
                      "cleanup": result["cleanup"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["snapshot", "run"])
    parser.add_argument("label")
    parser.add_argument("--port", type=int)
    parser.add_argument("argv", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.mode == "snapshot":
        snapshot(args.label)
    else:
        argv = args.argv[1:] if args.argv and args.argv[0] == "--" else args.argv
        run(args.label, argv, args.port)

