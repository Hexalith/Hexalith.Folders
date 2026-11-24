from pathlib import Path
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj"


class DependencyModeTests(unittest.TestCase):
    def test_contradictory_flags_fail_before_the_requested_target(self):
        for value in ("true", "false"):
            with self.subTest(value=value):
                result = self.probe(
                    f"-p:UseHexalithProjectReferences={value}",
                    f"-p:UseNuGetDeps={value}",
                )
                self.assertNotEqual(0, result.returncode)
                self.assertIn("FOLDERS001", result.stdout)

    def test_single_flags_and_consistent_pairs_resolve_the_same_modes(self):
        for project_refs in ("true", "false"):
            packages = "false" if project_refs == "true" else "true"
            source_flag = f"-p:UseHexalithProjectReferences={project_refs}"
            package_flag = f"-p:UseNuGetDeps={packages}"
            for flags in ((source_flag,), (package_flag,), (source_flag, package_flag)):
                with self.subTest(flags=flags):
                    result = self.probe(*flags)
                    self.assertEqual(0, result.returncode, result.stdout)
                    self.assertIn(f'"UseHexalithProjectReferences": "{project_refs}"', result.stdout)
                    self.assertIn(f'"UseNuGetDeps": "{packages}"', result.stdout)

    @staticmethod
    def probe(*flags: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["dotnet", "msbuild", str(PROJECT), "-nologo", "-t:GetTargetFrameworks",
             "-getProperty:UseHexalithProjectReferences,UseNuGetDeps", *flags],
            cwd=ROOT,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=60,
            check=False,
        )
