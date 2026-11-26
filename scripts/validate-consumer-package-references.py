#!/usr/bin/env python3
"""Build isolated package-only consumers for the Folders release inventory."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as element_tree
import zipfile

from release_package_contract import ROOT, validate_packages

EXTENSION_PACKAGE = "Hexalith.Folders.EventStoreExtensions"
EXTENSION_CONTRACTS_PACKAGE = "Hexalith.Folders.Contracts"
EXTENSION_LOADER_PROBE = ROOT / "tests" / "tools" / "EventStoreExtensionConsumer" / "Program.cs"
EXTENSION_CONSUMER_PACKAGES = {
    name.casefold()
    for name in (
        EXTENSION_PACKAGE,
        EXTENSION_CONTRACTS_PACKAGE,
        "Hexalith.EventStore.Contracts",
        "Hexalith.McpCli.Abstractions",
        "Hexalith.Commons.UniqueIds",
        "ByteAether.Ulid",
    )
}


def write_consumer(
    root: pathlib.Path, name: str, packages: dict[str, str], program: pathlib.Path | None = None
) -> pathlib.Path:
    """Write an isolated package-only consumer project."""

    directory = root / name
    directory.mkdir()
    project = directory / f"{name}.csproj"
    root_element = element_tree.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
    properties = element_tree.SubElement(root_element, "PropertyGroup")
    element_tree.SubElement(properties, "TargetFramework").text = "net11.0"
    element_tree.SubElement(properties, "Nullable").text = "enable"
    element_tree.SubElement(properties, "ImplicitUsings").text = "enable"
    if program is not None:
        element_tree.SubElement(properties, "OutputType").text = "Exe"
    references = element_tree.SubElement(root_element, "ItemGroup")
    element_tree.SubElement(references, "FrameworkReference", {"Include": "Microsoft.AspNetCore.App"})
    for package_id, version in packages.items():
        element_tree.SubElement(references, "PackageReference", {"Include": package_id, "Version": version})
    element_tree.ElementTree(root_element).write(project, encoding="utf-8", xml_declaration=False)
    if program is None:
        (directory / "Consumer.cs").write_text("public sealed class Consumer { }\n", encoding="utf-8")
    else:
        (directory / "Program.cs").write_bytes(program.read_bytes())
    return project


def eventstore_server_version(extension_project: str) -> str:
    """Resolve the same EventStore SDK pin used to build the packed extension."""
    version = subprocess.run(
        [
            "dotnet", "msbuild", extension_project, "-nologo", "-getProperty:HexalithEventStoreVersion",
            "-p:Configuration=Release", "-p:UseNuGetDeps=true", "-p:UseHexalithProjectReferences=false",
        ],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
        text=True,
    ).stdout.strip()
    if not version:
        raise ValueError("Could not resolve the release EventStore SDK version.")
    return version


def validate_extension_consumer(project: pathlib.Path, package_directory: pathlib.Path, version: str) -> None:
    """Check the published package and a consumer's isolated restore graph."""
    assets = json.loads((project.parent / "obj" / "project.assets.json").read_text(encoding="utf-8"))
    dependencies = set()
    for library in assets["libraries"]:
        dependency = library.split("/", 1)[0]
        dependency_key = dependency.casefold()
        if dependency_key not in EXTENSION_CONSUMER_PACKAGES:
            raise ValueError(f"{EXTENSION_PACKAGE} brought unexpected dependency {dependency} into its consumer.")
        dependencies.add(dependency_key)
    if EXTENSION_CONTRACTS_PACKAGE.casefold() not in dependencies:
        raise ValueError(f"{EXTENSION_PACKAGE} did not restore required dependency {EXTENSION_CONTRACTS_PACKAGE}.")

    with zipfile.ZipFile(package_directory / f"{EXTENSION_PACKAGE}.{version}.nupkg") as package:
        manifest_path = "build/hexalith.module-manifest.v2.json"
        source_manifest = ROOT / "module" / "hexalith.module-manifest.v2.json"
        if package.read(manifest_path) != source_manifest.read_bytes():
            raise ValueError(f"{EXTENSION_PACKAGE} packaged a different manifest from {source_manifest.name}.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=pathlib.Path)
    args = parser.parse_args()
    package_directory = args.package_directory.resolve()
    manifest, version = validate_packages(package_directory)
    package_ids = [package.package_id for package in manifest]
    extension_project = next(package.project for package in manifest if package.package_id == EXTENSION_PACKAGE)
    server_version = eventstore_server_version(extension_project)

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
        extension_consumer = write_consumer(root, "EventStoreExtensionConsumer", {EXTENSION_PACKAGE: version})
        loader_consumer = write_consumer(
            root,
            "EventStoreExtensionLoaderConsumer",
            {EXTENSION_PACKAGE: version, "Hexalith.EventStore.Server": server_version},
            EXTENSION_LOADER_PROBE,
        )
        projects = [
            write_consumer(root, "PrimaryPackagesConsumer", dict.fromkeys(package_ids[:split], version)),
            write_consumer(root, "ExtendedPackagesConsumer", dict.fromkeys(package_ids[split:], version)),
            extension_consumer,
            loader_consumer,
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
        validate_extension_consumer(extension_consumer, package_directory, version)

        for project in projects:
            subprocess.run(
                ["dotnet", "build", str(project), "--no-restore", "--configuration", "Release", "-warnaserror"],
                cwd=project.parent,
                env=environment,
                check=True,
            )

        subprocess.run(
            ["dotnet", "run", "--project", str(loader_consumer), "--configuration", "Release", "--no-build"],
            cwd=loader_consumer.parent,
            env=environment,
            check=True,
        )

    print(f"Validated four package-only consumers for all {len(package_ids)} packages at {version}.")
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
