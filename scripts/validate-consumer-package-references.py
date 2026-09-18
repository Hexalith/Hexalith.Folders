#!/usr/bin/env python3
"""Build isolated package-only consumers for the Folders release inventory."""

from __future__ import annotations

import argparse
import os
import pathlib
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as element_tree

from release_package_contract import validate_packages


def write_consumer(root: pathlib.Path, name: str, packages: list[str], version: str) -> pathlib.Path:
    """Write one empty package-only consumer project."""

    directory = root / name
    directory.mkdir()
    project = directory / f"{name}.csproj"
    root_element = element_tree.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
    properties = element_tree.SubElement(root_element, "PropertyGroup")
    element_tree.SubElement(properties, "TargetFramework").text = "net10.0"
    element_tree.SubElement(properties, "Nullable").text = "enable"
    element_tree.SubElement(properties, "ImplicitUsings").text = "enable"
    references = element_tree.SubElement(root_element, "ItemGroup")
    element_tree.SubElement(references, "FrameworkReference", {"Include": "Microsoft.AspNetCore.App"})
    for package_id in packages:
        element_tree.SubElement(references, "PackageReference", {"Include": package_id, "Version": version})
    element_tree.ElementTree(root_element).write(project, encoding="utf-8", xml_declaration=False)
    (directory / "Consumer.cs").write_text("public sealed class Consumer { }\n", encoding="utf-8")
    return project


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=pathlib.Path)
    args = parser.parse_args()
    package_directory = args.package_directory.resolve()
    manifest, version = validate_packages(package_directory)
    package_ids = [package.package_id for package in manifest]

    with tempfile.TemporaryDirectory(prefix="hexalith-folders-package-consumers-") as temporary:
        root = pathlib.Path(temporary)
        nuget_config = root / "NuGet.Config"
        nuget_config.write_text(
            """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="folders-release" value="PACKAGE_DIRECTORY" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="folders-release">
      <package pattern="Hexalith.Folders*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
""".replace("PACKAGE_DIRECTORY", str(package_directory)),
            encoding="utf-8",
        )
        split = max(1, (len(package_ids) + 1) // 2)
        projects = [
            write_consumer(root, "PrimaryPackagesConsumer", package_ids[:split], version),
            write_consumer(root, "ExtendedPackagesConsumer", package_ids[split:], version),
        ]
        environment = os.environ.copy()
        environment["NUGET_PACKAGES"] = str(root / ".nuget" / "packages")
        environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
        for project in projects:
            subprocess.run(
                ["dotnet", "restore", str(project), "--configfile", str(nuget_config)],
                cwd=project.parent,
                env=environment,
                check=True,
            )
            subprocess.run(
                ["dotnet", "build", str(project), "--no-restore", "--configuration", "Release", "-warnaserror"],
                cwd=project.parent,
                env=environment,
                check=True,
            )

    print(f"Validated two package-only consumers for all {len(package_ids)} packages at {version}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as error:
        print(f"Consumer validation failed with exit code {error.returncode}.", file=sys.stderr)
        raise SystemExit(error.returncode)
    except Exception as error:  # noqa: BLE001 - command-line gate reports a concise failure.
        print(f"Consumer validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
