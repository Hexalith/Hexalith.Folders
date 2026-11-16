"""Independently rehash the pending inventory and compare two private generations."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
SCRATCH = Path(os.environ["TMPDIR"]).resolve()
RELATIVE = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"


def require(value, message):
    if not value:
        raise RuntimeError(message)


def main():
    inventory = ROOT / RELATIVE
    rows = re.findall(r'  - path: "([^"\n]+)"\n    kind: file\n    bytes: (\d+)\n    sha256: ([a-f0-9]{64})', inventory.read_text())
    require(len(rows) == 1635 and len({row[0] for row in rows}) == 1635, "Wrong candidate denominator")
    for name, size, expected in rows:
        path = ROOT / name
        require(path.is_file() and path.stat().st_size == int(size) and
                hashlib.sha256(path.read_bytes()).hexdigest() == expected, "Candidate drift: " + name)
    commands = []
    for ordinal in (1, 2):
        target = SCRATCH / ("candidate-" + str(ordinal) + ".yaml")
        arguments = ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(target)]
        result = subprocess.run(arguments, cwd=ROOT, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True,
                                env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1"))
        name = "candidate-generation-" + str(ordinal)
        (OUT / (name + ".log")).write_text(result.stdout.replace(str(SCRATCH), "{private-temp}"))
        commands.append({"name": name, "argv": [arg.replace(str(SCRATCH), "{private-temp}") for arg in arguments],
                         "cwd": "{workspace}", "exit_code": result.returncode, "log": name + ".log"})
        require(result.returncode == 0 and target.read_bytes() == inventory.read_bytes(), "Generation differs")
    (OUT / "candidate-commands.json").write_text(json.dumps(commands, indent=2) + "\n")
    (OUT / "candidate-verification.json").write_text(json.dumps({
        "recorded_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "path": RELATIVE, "sha256": hashlib.sha256(inventory.read_bytes()).hexdigest(),
        "entries_rehashed": len(rows), "two_generations_equal_current": True}, indent=2) + "\n")
    print("PASS: 1635 independently rehashed entries; two private generations equal the untouched pending inventory.")


if __name__ == "__main__":
    main()
