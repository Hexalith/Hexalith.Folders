#!/usr/bin/env python3
"""Validate Folders release packages against the authoritative inventory."""

from __future__ import annotations

import argparse
import pathlib
import sys

from release_package_contract import validate_packages


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=pathlib.Path)
    parser.add_argument("--version")
    parser.add_argument("--source-revision")
    args = parser.parse_args()
    manifest, version = validate_packages(
        args.package_directory.resolve(),
        args.version,
        args.source_revision,
    )
    print(f"Validated {len(manifest)} sealed package/symbol pairs at version {version}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - command-line gate reports a concise failure.
        print(f"Package validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
