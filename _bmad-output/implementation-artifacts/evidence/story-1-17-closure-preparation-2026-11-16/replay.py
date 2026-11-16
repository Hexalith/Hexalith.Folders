"""Read-only packet integrity and optional exact-workspace evidence replay."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(name):
    return json.loads((PACKET / name).read_text())


def git(owner, *arguments, binary=False):
    result = subprocess.check_output(["git", "-C", str(owner), *arguments])
    return result if binary else result.decode().strip()


def packet_integrity():
    rows = (PACKET / "SHA256SUMS").read_text().splitlines()
    names = []
    for row in rows:
        digest, name = row.split("  ", 1)
        require(re.fullmatch(r"[a-f0-9]{64}", digest) is not None, "Invalid packet digest")
        require(Path(name).name == name and name not in (".", ".."), "Unsafe packet path")
        member = PACKET / name
        require(member.is_file() and not member.is_symlink(), "Missing or linked member: " + name)
        require(sha(member) == digest, "Packet member changed: " + name)
        names.append(name)
    require(len(names) == len(set(names)), "Duplicate packet member")
    actual = {p.name for p in PACKET.iterdir() if p.is_file() and p.name != "SHA256SUMS"}
    require(set(names) == actual, "Packet manifest is incomplete")
    commands = read("commands.json")
    expected = {"retained-released-v2": 0, "retained-landed-tuple": 0,
                "current-audit-validator": 0, "current-source-proposal": 1}
    require({c["name"]: c["exit_code"] for c in commands} == expected, "Recorded lane outcomes changed")
    for command in commands:
        require(command["log"] in names, "Missing command log")
    require("317 packages, 157 families, and 1 source" in
            (PACKET / "current-audit-validator.log").read_text(), "Wrong audit outcome")
    return len(names)


def workspace_replay(workspace):
    before = read("before.json")
    appends = read("document-appends.json")
    receipt = read("receipt.json")
    for label in ("new_handoff", "owner_intake", "previous_handoff", "preparation_spec", "byte_attributes"):
        require(sha(workspace / receipt[label]) == receipt[label + "_sha256"], "Linked artifact changed: " + label)
    for relative, owner in before["repositories"].items():
        path = workspace / relative
        require(git(path, "rev-parse", "HEAD") == owner["head"], "Owner revision changed: " + relative)
        require(hashlib.sha256(git(path, "ls-files", "--stage", "-z", binary=True)).hexdigest() ==
                owner["index_sha256"], "Owner index changed: " + relative)
    for relative, expected in before["protected"].items():
        path = workspace / relative
        if relative in appends:
            row = appends[relative]
            data = path.read_bytes()
            require(hashlib.sha256(data[:row["prefix_bytes"]]).hexdigest() == expected and
                    hashlib.sha256(data[row["prefix_bytes"]:]).hexdigest() == row["append_sha256"] and
                    sha(path) == row["final_sha256"], "Historical append changed: " + relative)
        else:
            require(sha(path) == expected, "Protected file changed: " + relative)
    for relative, expected in read("inspected-inputs.json")["files"].items():
        require(sha(workspace / relative) == expected, "Inspected input changed: " + relative)

    comparison = read("source-comparison.json")
    prior_path = workspace / comparison["prior_manifest"]
    require(sha(prior_path) == comparison["prior_manifest_sha256"], "Prior source manifest changed")
    prior = json.loads(prior_path.read_text())
    for relative, expected in comparison["owners"].items():
        owner = workspace / relative
        current = {}
        for name in git(owner, "ls-files", "-z", binary=True).decode().split("\0"):
            if name and not name.startswith(("_bmad-output/implementation-artifacts/evidence/",
                                              "references/", ".agents/", "_bmad/")) and (owner / name).is_file():
                current[name] = sha(owner / name)
        old = prior[relative]["files"]
        changed = [{"path": p, "before_sha256": v, "current_sha256": current[p]}
                   for p, v in old.items() if p in current and current[p] != v]
        added = [{"path": p, "sha256": v} for p, v in current.items() if p not in old]
        require(len(old) == expected["prior_paths"] and changed == expected["changed"] and
                added == expected["added"] and sorted(set(old) - set(current)) == expected["removed"],
                "Source comparison changed: " + relative)

    candidate = read("pending-inventory-rehash.json")
    inventory = workspace / candidate["path"]
    require(sha(inventory) == candidate["sha256"] == candidate["expected_sha256"], "Pending inventory changed")
    rows = re.findall(r'  - path: "([^"\n]+)"\n    kind: file\n    bytes: (\d+)\n    sha256: ([a-f0-9]{64})',
                      inventory.read_text())
    require(len(rows) == len({r[0] for r in rows}) == candidate["entries_rehashed"] == 1635,
            "Pending inventory denominator changed")
    for relative, size, expected in rows:
        path = workspace / relative
        require(path.stat().st_size == int(size) and sha(path) == expected, "Pending row changed: " + relative)

    mapping = read("current-mapping-inputs.json")
    platform = workspace / "references/Hexalith.Platform"
    direct = {line.split("\t")[1]: line.split()[2]
              for line in git(platform, "ls-tree", "HEAD:references").splitlines() if line.startswith("160000 ")}
    require(direct == {r["owner"]: r["platform_direct_revision"] for r in mapping["direct_owner_inventory"]},
            "Platform direct owner inventory changed")
    for row in mapping["rows"]:
        owner = workspace / ("references/Hexalith." + row["owner"])
        data = git(owner, "show", row["platform_direct_revision"] + ":module/hexalith.module-manifest.v2.json", binary=True)
        require(hashlib.sha256(data).hexdigest() == row["platform_direct_declaration"]["sha256"] and
                sha(owner / "module/hexalith.module-manifest.v2.json") == row["root_checkout_declaration"]["sha256"],
                "Declaration comparison changed: " + row["owner"])
    handoff = json.loads((workspace / receipt["new_handoff"]).read_text())
    controls = handoff["controls"]
    require(controls == receipt["controls"] and controls["route_mode"] == "V1Only" and
            controls["migration_t0"] is None and controls["activation_authority"] is None and
            controls["staging_checks_executed"] == 0 and not handoff["story_closed"] and
            not handoff["tracking"]["transition_authorized"] and not handoff["tracking"]["applied_delta"],
            "Unsafe closure or activation controls")
    stages = json.loads((workspace / "eng/story117-platform-qualification-current/staging-checks.json").read_text())
    require(len(stages["checks"]) == 14 and all(c["status"] == "unexecuted" for c in stages["checks"]),
            "Staging controls changed")
    print("PASS: 13 owner revisions/indices, 200 protected paths, 3 historical prefixes, source comparisons, "
          "1635 pending rows, 17 Platform pins, inspected inputs and inert controls independently recomputed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path, help="Recompute against the exact recorded workspace; changed tuples refuse.")
    args = parser.parse_args()
    members = packet_integrity()
    if args.workspace is not None:
        workspace_replay(args.workspace.resolve())
    print(f"PASS: {members} sealed packet members verified; no operational acceptance inferred.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        sys.exit(1)
