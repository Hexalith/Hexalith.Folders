"""Check read-only compiler diagnostic parsing with quoted and relative inputs."""
from pathlib import Path
import tempfile
import unittest

import capture_compiler


class CompilerCaptureTests(unittest.TestCase):
    def test_command_parser_retains_quoted_paths(self):
        row = capture_compiler.compiler_command('  "/sdk folder/csc" /noconfig /out:"/artifacts/project/out.dll" /resource:"data file.json",Resource (TaskId:3)')
        self.assertEqual(row[0], "/sdk folder/csc")
        self.assertIn("/resource:data file.json,Resource", row[1])

    def test_non_compiler_diagnostic_is_not_a_command(self):
        self.assertIsNone(capture_compiler.compiler_command("Task Parameter:Message=Do not execute /noconfig"))
        self.assertIsNone(capture_compiler.compiler_command("/unrelated/tool /noconfig /out:/out.dll"))

    def test_response_vectors_refuse_including_hidden_or_nested_options(self):
        for line in ('/sdk/csc /noconfig @outer.rsp', '/sdk/csc @outer.rsp', '/sdk/dotnet exec /sdk/csc.dll /noconfig @nested.rsp'):
            with self.subTest(line=line), self.assertRaisesRegex(ValueError, "response-file"):
                capture_compiler.compiler_command(line)
        with self.assertRaisesRegex(ValueError, "response-file"):
            capture_compiler.auxiliary_inputs('/sdk/csc', ['@outer.rsp'], Path('/src/Project.csproj'))

    def test_resource_and_analyzer_inputs_resolve_against_the_restored_owner(self):
        rows = capture_compiler.auxiliary_inputs("/sdk/csc", ["/resource:Resources/data.json,Logical.Name", "/analyzer:/packages/a.dll,/packages/b.dll", "/additionalfile:src/Component.razor"], Path("/repo/project/Project.csproj"))
        self.assertEqual(rows[1], {"kind": "embedded-resource", "path": "/repo/project/Resources/data.json"})
        self.assertEqual(rows[2]["path"], "/packages/a.dll")
        self.assertEqual(rows[3]["path"], "/packages/b.dll")
        self.assertEqual(rows[4]["path"], "/repo/project/src/Component.razor")

    def test_unowned_compiler_output_refuses(self):
        with self.assertRaisesRegex(ValueError, "No restored project"):
            capture_compiler.project_for_output(["/out:/outside/Obj.dll"], [(Path("/artifacts/obj/Project"), Path("/repo/Project.csproj"))])

    def test_most_specific_restored_owner_wins(self):
        project, _ = capture_compiler.project_for_output(["/out:/artifacts/obj/Project/debug/Project.dll"], [(Path("/artifacts"), Path("/wrong.csproj")), (Path("/artifacts/obj/Project"), Path("/repo/Project.csproj"))])
        self.assertEqual(project, Path("/repo/Project.csproj"))

    def test_missing_or_linked_compiler_input_refuses(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaisesRegex(ValueError, "Unavailable compiler input"):
                capture_compiler.binding(root / "missing.dll")
            source = root / "actual.dll"
            source.write_bytes(b"compiler input")
            link = root / "link.dll"
            link.symlink_to(source)
            with self.assertRaisesRegex(ValueError, "Unavailable compiler input"):
                capture_compiler.binding(link)


if __name__ == "__main__":
    unittest.main()
