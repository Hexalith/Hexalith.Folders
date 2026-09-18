#!/usr/bin/env python3
"""Pack the exact Folders NuGet release inventory."""

from __future__ import annotations

import argparse
import pathlib
import subprocess
import sys

from release_package_contract import ROOT, load_manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output_directory", type=pathlib.Path)
    parser.add_argument("version")
    parser.add_argument("--source-revision")
    args = parser.parse_args()

    source_revision = args.source_revision
    if source_revision is None:
        source_revision = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()
    if len(source_revision) != 40 or any(character not in "0123456789abcdef" for character in source_revision):
        raise ValueError("source revision must be an exact lowercase commit SHA")

    output = args.output_directory.resolve()
    expected_output = (ROOT / "nupkgs").resolve()
    if output != expected_output or args.output_directory.is_symlink():
        raise ValueError(f"output directory must be the repository-owned package directory: {expected_output}")
    output.mkdir(parents=True, exist_ok=True)
    for pattern in ("*.nupkg", "*.snupkg"):
        for package in output.glob(pattern):
            package.unlink()

    for package in load_manifest():
        common_properties = [
            "-p:UseNuGetDeps=true",
            "-p:UseHexalithProjectReferences=false",
        ]
        subprocess.run(
            [
                "dotnet",
                "restore",
                package.project,
                "--force",
                "-m:1",
                *common_properties,
            ],
            cwd=ROOT,
            check=True,
        )
        subprocess.run(
            [
                "dotnet",
                "build",
                package.project,
                "--configuration",
                "Release",
                "--no-restore",
                "-warnaserror",
                "-m:1",
                *common_properties,
            ],
            cwd=ROOT,
            check=True,
        )
        subprocess.run(
            [
                "dotnet",
                "pack",
                package.project,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                str(output),
                "-m:1",
                *common_properties,
                f"-p:PackageVersion={args.version}",
                f"-p:Version={args.version}",
                f"-p:RepositoryCommit={source_revision}",
                f"-p:SourceRevisionId={source_revision}",
                "-p:ContinuousIntegrationBuild=true",
                "-p:IncludeSymbols=true",
                "-p:SymbolPackageFormat=snupkg",
                "-p:GeneratePackageOnBuild=false",
            ],
            cwd=ROOT,
            check=True,
        )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - command-line gate reports a concise failure.
        print(f"Package packing failed: {error}", file=sys.stderr)
        raise SystemExit(1)
