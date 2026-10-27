"""Read-only capsule verifier: no extraction, subprocess, SDK or archived code execution."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import tarfile


def require(condition, detail):
    if not condition:
        raise ValueError(detail)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def safe_name(name):
    path = PurePosixPath(name)
    return bool(name) and not path.is_absolute() and ".." not in path.parts and "\\" not in name and str(path) == name


def verify(directory, receipt_name, expected_receipt):
    require(safe_name(receipt_name) and "/" not in receipt_name, "Unsafe receipt path")
    receipt_path = directory / receipt_name
    require(not receipt_path.is_symlink() and digest(receipt_path) == expected_receipt, "Receipt hash mismatch")
    receipt = json.loads(receipt_path.read_text())
    for key in ("archive", "memberManifest"):
        row = receipt[key]
        require(safe_name(row["path"]) and "/" not in row["path"], "Unsafe artifact path")
        path = directory / row["path"]
        require(not path.is_symlink() and path.stat().st_size == row["bytes"] and digest(path) == row["sha256"], "Artifact drift: " + row["path"])
    manifest = json.loads((directory / receipt["memberManifest"]["path"]).read_text())
    indexed = {row["path"]: row for row in manifest}
    require(len(indexed) == len(manifest) == receipt["memberCount"], "Duplicate or missing manifest member")
    require(all(safe_name(name) for name in indexed), "Unsafe manifest member")
    seen = set()
    with tarfile.open(directory / receipt["archive"]["path"], "r:gz") as archive:
        for member in archive:
            require(safe_name(member.name) and member.isfile() and member.name not in seen, "Unsafe, special or duplicate member")
            require(member.name in indexed, "Unexpected member")
            row = indexed[member.name]
            require(member.size == row["bytes"], "Member size mismatch")
            with archive.extractfile(member) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest() == row["sha256"], "Member hash mismatch")
            seen.add(member.name)
    require(seen == set(indexed), "Missing member")
    require(receipt["outcome"]["controls"]["stagingChecksExecuted"] == 0, "Unexpected acceptance state")
    return dict(verified=True, members=len(seen), receiptSha256=expected_receipt,
                limitation="Packet integrity only; no renewed execution authority or compatibility acceptance.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--receipt", default="story-1-17-current-builds-host-2026-10-26.json")
    parser.add_argument("--receipt-sha256", required=True, help="The fixed receipt SHA-256 from the result record")
    args = parser.parse_args()
    print(json.dumps(verify(args.directory, args.receipt, args.receipt_sha256), sort_keys=True))


if __name__ == "__main__":
    main()
