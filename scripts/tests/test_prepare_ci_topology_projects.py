from __future__ import annotations

import json
import os
import pathlib
import shutil
import stat
import subprocess
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/prepare-ci-topology-projects.sh"
MODULE = "references/Hexalith.Memories"
PLATFORM_MODULE = "references/Hexalith.Platform"
TENANTS_MODULE = "references/Hexalith.Tenants"
MCPCLI_MODULE = "references/Hexalith.McpCli"
MCPCLI_INPUTS = (
    "_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md",
    "src/Hexalith.McpCli.Core/Serialization/McpCliJson.cs",
)
RECORDED_SHA = "0123456789abcdef0123456789abcdef01234567"
UPDATE = [
    "-c", "submodule.recurse=false", "-c", "fetch.recurseSubmodules=false",
    "submodule", "update", "--init", "--checkout", "--", MODULE,
]
PLATFORM_UPDATE = [
    "-c", "submodule.recurse=false", "-c", "fetch.recurseSubmodules=false",
    "submodule", "update", "--init", "--checkout", "--", PLATFORM_MODULE,
]
TENANTS_UPDATE = [
    "-c", "submodule.recurse=false", "-c", "fetch.recurseSubmodules=false",
    "submodule", "update", "--init", "--checkout", "--", TENANTS_MODULE,
]
MCPCLI_UPDATE = [
    "-c", "submodule.recurse=false", "-c", "fetch.recurseSubmodules=false",
    "submodule", "update", "--init", "--checkout", "--", MCPCLI_MODULE,
]


class PrepareCiTopologyProjectsTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory(prefix="folders-ci-topology-")
        self.addCleanup(temporary.cleanup)
        self.root = pathlib.Path(temporary.name)
        self.workspace = self.root / "workspace"
        self.workspace.mkdir()
        self.real_git = shutil.which("git")
        self.environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        self.environment.update({"GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": os.devnull, "GIT_TERMINAL_PROMPT": "0"})
        self.git("init", "--initial-branch=main")
        self.git("remote", "add", "origin", "https://github.com/Hexalith/Hexalith.Folders.git")
        (self.workspace / "Hexalith.Folders.slnx").write_text("<Solution />\n", encoding="utf-8")
        (self.workspace / "scripts").mkdir()
        self.script = self.workspace / "scripts" / SCRIPT.name
        shutil.copyfile(SCRIPT, self.script)
        (self.workspace / "scripts/install-dapr-cli.sh").write_text(
            '#!/usr/bin/env bash\nset -euo pipefail\nprintf "%s\\n" fixture-dapr-cli >> "$GITHUB_PATH"\n',
            encoding="utf-8",
        )
        self.declaration = self.workspace / ".gitmodules"
        self.declaration.write_text(
            '[submodule "Hexalith.Memories"]\n'
            f'\tpath = {MODULE}\n'
            '\turl = https://github.com/Hexalith/Hexalith.Memories.git\n'
            '\tupdate = none\n'
            '[submodule "Hexalith.Platform"]\n'
            f'\tpath = {PLATFORM_MODULE}\n'
            '\turl = https://github.com/Hexalith/Hexalith.Platform.git\n'
            '\tupdate = none\n'
            '[submodule "Hexalith.Tenants"]\n'
            f'\tpath = {TENANTS_MODULE}\n'
            '\turl = https://github.com/Hexalith/Hexalith.Tenants.git\n'
            '\tupdate = none\n'
            '[submodule "Hexalith.McpCli"]\n'
            f'\tpath = {MCPCLI_MODULE}\n'
            '\turl = https://github.com/Hexalith/Hexalith.McpCli.git\n'
            '\tupdate = none\n', encoding="utf-8",
        )
        self.git("add", ".gitmodules", "Hexalith.Folders.slnx", "scripts")
        self.git("update-index", "--add", "--cacheinfo", f"160000,{RECORDED_SHA},{MODULE}")
        self.git("update-index", "--add", "--cacheinfo", f"160000,{RECORDED_SHA},{PLATFORM_MODULE}")
        self.git("update-index", "--add", "--cacheinfo", f"160000,{RECORDED_SHA},{TENANTS_MODULE}")
        self.git("update-index", "--add", "--cacheinfo", f"160000,{RECORDED_SHA},{MCPCLI_MODULE}")
        self.git("commit", "-m", "test: declare standalone Folders fixture")

        # Read-only Git probes use the isolated real fixture. The only update
        # command is intercepted: no network or submodule initialization occurs.
        bin_directory = self.root / "bin"
        bin_directory.mkdir()
        shim = bin_directory / "git"
        shim.write_text(
            """#!/usr/bin/env python3
import json, os, pathlib, sys
arguments = sys.argv[1:]
if 'submodule' in arguments:
    with open(os.environ['TEST_UPDATE_LOG'], 'a', encoding='utf-8') as log:
        log.write(json.dumps(arguments) + '\\n')
    if os.environ.get('TEST_UPDATE_EXIT'):
        raise SystemExit(int(os.environ['TEST_UPDATE_EXIT']))
    project = pathlib.Path(os.environ['GITHUB_WORKSPACE']) / 'references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj'
    project.parent.mkdir(parents=True, exist_ok=True)
    project.write_text('<Project />\\n', encoding='utf-8')
    platform = pathlib.Path(os.environ['GITHUB_WORKSPACE']) / 'references/Hexalith.Platform'
    platform.mkdir(parents=True, exist_ok=True)
    for name in ('DaprSelfHostedMtls.cs', 'DaprSelfHostedMtlsSubscriber.cs'):
        (platform / name).write_text('// fixture\\n', encoding='utf-8')
    tenant_project = pathlib.Path(os.environ['GITHUB_WORKSPACE']) / 'references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj'
    tenant_project.parent.mkdir(parents=True, exist_ok=True)
    tenant_project.write_text('<Project />\\n', encoding='utf-8')
    if arguments[-1] == 'references/Hexalith.McpCli':
        for relative in ('_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md',
                         'src/Hexalith.McpCli.Core/Serialization/McpCliJson.cs'):
            if relative == os.environ.get('TEST_MCPCLI_MISSING_INPUT'):
                continue
            path = pathlib.Path(os.environ['GITHUB_WORKSPACE']) / arguments[-1] / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('fixture\\n', encoding='utf-8')
    raise SystemExit(0)
if arguments == ['-C', 'references/Hexalith.McpCli', 'rev-parse', 'HEAD']:
    print(os.environ.get('TEST_MCPCLI_CHECKOUT_SHA', os.environ['TEST_RECORDED_SHA']))
    raise SystemExit(0)
if arguments == ['-C', 'references/Hexalith.McpCli', 'status', '--porcelain', '--untracked-files=all']:
    if os.environ.get('TEST_MCPCLI_STATUS_EXIT'):
        raise SystemExit(int(os.environ['TEST_MCPCLI_STATUS_EXIT']))
    print(os.environ.get('TEST_MCPCLI_STATUS', ''), end='')
    raise SystemExit(0)
if arguments in (['-C', 'references/Hexalith.Memories', 'rev-parse', 'HEAD'],
                 ['-C', 'references/Hexalith.Platform', 'rev-parse', 'HEAD'],
                 ['-C', 'references/Hexalith.Tenants', 'rev-parse', 'HEAD']):
    print(os.environ.get('TEST_CHECKOUT_SHA', os.environ['TEST_RECORDED_SHA']))
    raise SystemExit(0)
if arguments in (['-C', 'references/Hexalith.Memories', 'status', '--porcelain', '--untracked-files=all'],
                 ['-C', 'references/Hexalith.Platform', 'status', '--porcelain', '--untracked-files=all']):
    if os.environ.get('TEST_CHECKOUT_STATUS_EXIT'):
        raise SystemExit(int(os.environ['TEST_CHECKOUT_STATUS_EXIT']))
    print(os.environ.get('TEST_CHECKOUT_STATUS', ''), end='')
    raise SystemExit(0)
if arguments == ['rev-parse', '--show-toplevel'] and os.environ.get('TEST_TOPLEVEL'):
    print(os.environ['TEST_TOPLEVEL'])
    raise SystemExit(0)
if arguments == ['rev-parse', '--show-superproject-working-tree']:
    if os.environ.get('TEST_SUPERPROJECT_EXIT'):
        raise SystemExit(int(os.environ['TEST_SUPERPROJECT_EXIT']))
    if os.environ.get('TEST_SUPERPROJECT'):
        print(os.environ['TEST_SUPERPROJECT'])
        raise SystemExit(0)
os.execv(os.environ['TEST_REAL_GIT'], [os.environ['TEST_REAL_GIT'], *arguments])
""", encoding="utf-8",
        )
        shim.chmod(shim.stat().st_mode | stat.S_IXUSR)
        self.log = self.root / "updates.jsonl"
        self.environment.update({
            "PATH": f"{bin_directory}{os.pathsep}{self.environment['PATH']}",
            "GITHUB_ACTIONS": "true", "GITHUB_SERVER_URL": "https://github.com",
            "GITHUB_REPOSITORY": "Hexalith/Hexalith.Folders", "GITHUB_WORKSPACE": str(self.workspace),
            "TEST_REAL_GIT": self.real_git, "TEST_UPDATE_LOG": str(self.log), "TEST_RECORDED_SHA": RECORDED_SHA,
            "RUNNER_TEMP": str(self.root), "GITHUB_PATH": str(self.root / "github-path"),
        })

    def git(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [self.real_git, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", *arguments],
            cwd=self.workspace, env=self.environment, text=True, capture_output=True, check=True, timeout=15,
        )

    def prepare(self, *, overrides: dict[str, str] | None = None, cwd: pathlib.Path | None = None,
                script: pathlib.Path | None = None) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["bash", str(script or self.script)], cwd=cwd or self.workspace,
            env=self.environment | (overrides or {}), text=True, capture_output=True, check=False, timeout=15,
        )

    def assert_refused(self, **arguments) -> None:
        result = self.prepare(**arguments)
        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertFalse(self.log.exists(), "No update may run before all root guards pass.")

    def commit_declaration(self, content: str) -> None:
        self.declaration.write_text(content, encoding="utf-8")
        self.git("add", ".gitmodules")
        self.git("commit", "-m", "test: alter fixture declaration")

    def test_standalone_dispatch_initializes_missing_recorded_mcpcli_and_topology_roots(self) -> None:
        self.assertFalse((self.workspace / MCPCLI_MODULE).exists())
        result = self.prepare()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual([UPDATE, MCPCLI_UPDATE, TENANTS_UPDATE, PLATFORM_UPDATE], [json.loads(line) for line in self.log.read_text().splitlines()])
        self.assertIn("fixture-dapr-cli", (self.root / "github-path").read_text())
        self.assertIn(RECORDED_SHA, result.stdout)
        self.assertFalse((self.workspace / MODULE / ".git").exists())
        self.assertFalse((self.workspace / PLATFORM_MODULE / ".git").exists())
        for relative in MCPCLI_INPUTS:
            self.assertTrue((self.workspace / MCPCLI_MODULE / relative).is_file())
        self.assertFalse((self.workspace / MCPCLI_MODULE / "references").exists())

    def test_mcpcli_contract_inputs_must_exist_after_initialization(self) -> None:
        for relative in MCPCLI_INPUTS:
            with self.subTest(relative=relative):
                shutil.rmtree(self.workspace / MCPCLI_MODULE, ignore_errors=True)
                result = self.prepare(overrides={"TEST_MCPCLI_MISSING_INPUT": relative})
                self.assertNotEqual(0, result.returncode)
                self.assertIn("inventory or serializer source is unavailable", result.stderr)
                self.assertNotIn(f"Prepared {MCPCLI_MODULE}", result.stdout)

    def test_mcpcli_checkout_must_match_the_recorded_revision(self) -> None:
        result = self.prepare(overrides={"TEST_MCPCLI_CHECKOUT_SHA": "a" * 40})
        self.assertNotEqual(0, result.returncode)
        self.assertIn("McpCli checkout differs from the recorded gitlink", result.stderr)

    def test_mcpcli_checkout_must_be_clean_and_inspectable(self) -> None:
        for overrides, diagnostic in (
            ({"TEST_MCPCLI_STATUS": "?? untracked-metadata"}, "McpCli checkout must be clean"),
            ({"TEST_MCPCLI_STATUS_EXIT": "128"}, "Cannot inspect the McpCli checkout worktree"),
        ):
            with self.subTest(overrides=overrides):
                result = self.prepare(overrides=overrides)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(diagnostic, result.stderr)
                self.assertNotIn(f"Prepared {MCPCLI_MODULE}", result.stdout)

    def test_rejects_mcpcli_url_override_before_its_checkout(self) -> None:
        self.git("config", "submodule.Hexalith.McpCli.url", "file:///outside")
        result = self.prepare()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("local McpCli URL differs", result.stderr)
        self.assertEqual([UPDATE], [json.loads(line) for line in self.log.read_text().splitlines()])

    def test_rejects_a_staged_mcpcli_gitlink_before_its_checkout(self) -> None:
        self.git("update-index", "--cacheinfo", f"160000,{'a' * 40},{MCPCLI_MODULE}")
        result = self.prepare()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("McpCli gitlink has staged changes", result.stderr)
        self.assertEqual([UPDATE], [json.loads(line) for line in self.log.read_text().splitlines()])

    def test_workflow_selects_the_existing_package_prepare_hook(self) -> None:
        self.assertIn("      package-prepare-script: scripts/prepare-ci-topology-projects.sh\n",
                      (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8"))
        workflow = (ROOT / ".github/workflows/contract-spine.yml").read_text(encoding="utf-8")
        root_build_paths = (
            "references/Hexalith.AI.Tools", "references/Hexalith.Builds", "references/Hexalith.Commons",
            "references/Hexalith.EventStore", "references/Hexalith.FrontComposer", "references/Hexalith.Memories",
            "references/Hexalith.McpCli", "references/Hexalith.PolymorphicSerializations", "references/Hexalith.Tenants",
        )
        build_step = f"        run: git submodule update --init --checkout {' '.join(root_build_paths)}\n"
        prepare_step = "      - name: Prepare CI topology projects\n        run: bash scripts/prepare-ci-topology-projects.sh\n"
        self.assertIn(build_step, workflow)
        self.assertIn(prepare_step, workflow)
        self.assertLess(workflow.index(build_step), workflow.index(prepare_step))
        self.assertLess(workflow.index(prepare_step), workflow.index("      - name: Restore\n"))

    def test_rejects_nonmatching_github_context(self) -> None:
        for key, value in (("GITHUB_ACTIONS", ""), ("GITHUB_ACTIONS", "false"),
                           ("GITHUB_REPOSITORY", ""), ("GITHUB_REPOSITORY", "Hexalith/Hexalith.Projects"),
                           ("GITHUB_SERVER_URL", "https://example.invalid")):
            with self.subTest(key=key, value=value):
                self.assert_refused(overrides={key: value})

    def test_rejects_wrong_working_directory(self) -> None:
        self.assert_refused(cwd=self.workspace / "scripts")

    def test_rejects_a_git_root_different_from_the_workspace(self) -> None:
        self.assert_refused(overrides={"TEST_TOPLEVEL": str(self.root)})

    def test_rejects_wrong_or_missing_workspace(self) -> None:
        for workspace in ("", "relative", str(self.root), str(self.root / "absent")):
            with self.subTest(workspace=workspace):
                self.assert_refused(overrides={"GITHUB_WORKSPACE": workspace})

    def test_rejects_a_script_from_another_checkout(self) -> None:
        other = self.root / "other/scripts"
        other.mkdir(parents=True)
        shutil.copyfile(self.script, other / SCRIPT.name)
        self.assert_refused(script=other / SCRIPT.name)

    def test_rejects_a_submodule_gitfile(self) -> None:
        (self.workspace / ".git").rename(self.root / "database")
        (self.workspace / ".git").write_text(f"gitdir: {self.root / 'database'}\n", encoding="utf-8")
        self.assert_refused()

    def test_rejects_an_enclosing_repository_even_without_submodule_registration(self) -> None:
        (self.root / ".git").mkdir()
        self.assert_refused()

    def test_rejects_reported_superproject_or_a_failed_probe(self) -> None:
        for overrides in ({"TEST_SUPERPROJECT": str(self.root)}, {"TEST_SUPERPROJECT_EXIT": "128"}):
            with self.subTest(overrides=overrides):
                self.assert_refused(overrides=overrides)

    def test_rejects_git_directory_or_index_overrides(self) -> None:
        for key in ("GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE"):
            with self.subTest(key=key):
                self.assert_refused(overrides={key: str(self.root)})

    def test_rejects_nonmatching_origin(self) -> None:
        self.git("remote", "set-url", "origin", "https://github.com/Hexalith/Hexalith.Projects.git")
        self.assert_refused()

    def test_rejects_a_missing_root_declaration(self) -> None:
        self.declaration.unlink()
        self.assert_refused()

    def test_rejects_an_untracked_root_declaration(self) -> None:
        self.git("rm", "--cached", ".gitmodules")
        self.git("commit", "-m", "test: untrack fixture declaration")
        self.assert_refused()

    def test_rejects_uncommitted_declaration_changes(self) -> None:
        self.declaration.write_text(self.declaration.read_text() + "# changed\n", encoding="utf-8")
        self.assert_refused()
        self.git("add", ".gitmodules")
        self.assert_refused()

    def test_rejects_a_changed_recorded_module_path(self) -> None:
        self.commit_declaration(self.declaration.read_text().replace(MODULE, "references/Other"))
        self.assert_refused()

    def test_rejects_a_changed_recorded_module_url(self) -> None:
        self.commit_declaration(self.declaration.read_text().replace("Hexalith.Memories.git", "Other.git"))
        self.assert_refused()

    def test_rejects_a_duplicate_path_declaration(self) -> None:
        self.commit_declaration(self.declaration.read_text() + f'[submodule "Other"]\n\tpath = {MODULE}\n')
        self.assert_refused()

    def test_rejects_a_local_module_url_override(self) -> None:
        self.git("config", "submodule.Hexalith.Memories.url", "file:///outside")
        self.assert_refused()

    def test_rejects_a_missing_recorded_gitlink(self) -> None:
        self.git("update-index", "--force-remove", MODULE)
        self.git("commit", "-m", "test: remove fixture gitlink")
        self.assert_refused()

    def test_rejects_a_staged_gitlink_change(self) -> None:
        self.git("update-index", "--cacheinfo", f"160000,{'a' * 40},{MODULE}")
        self.assert_refused()

    def test_rejects_a_symlinked_dependency_path(self) -> None:
        (self.workspace / "references").symlink_to(self.root, target_is_directory=True)
        self.assert_refused()

    def test_propagates_update_failure(self) -> None:
        result = self.prepare(overrides={"TEST_UPDATE_EXIT": "17"})
        self.assertEqual(17, result.returncode, result.stdout + result.stderr)
        self.assertEqual([UPDATE], [json.loads(line) for line in self.log.read_text().splitlines()])

    def test_rejects_checkout_at_a_different_commit(self) -> None:
        result = self.prepare(overrides={"TEST_CHECKOUT_SHA": "a" * 40})
        self.assertNotEqual(0, result.returncode)
        self.assertIn("differs from the recorded gitlink", result.stderr)

    def test_rejects_dirty_selected_worktree_at_the_recorded_commit(self) -> None:
        for status in (" M src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
                       "M  src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
                       "?? src/Hexalith.Memories.Server/Properties/launchSettings.json"):
            with self.subTest(status=status):
                result = self.prepare(overrides={"TEST_CHECKOUT_STATUS": status})
                self.assertNotEqual(0, result.returncode)
                self.assertIn("checkout must be clean", result.stderr)
                self.assertNotIn("[ci-topology] Prepared", result.stdout)

    def test_rejects_a_failed_selected_worktree_status_probe(self) -> None:
        result = self.prepare(overrides={"TEST_CHECKOUT_STATUS_EXIT": "128"})
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Cannot inspect the Memories checkout worktree", result.stderr)
        self.assertNotIn("[ci-topology] Prepared", result.stdout)


if __name__ == "__main__":
    unittest.main()
