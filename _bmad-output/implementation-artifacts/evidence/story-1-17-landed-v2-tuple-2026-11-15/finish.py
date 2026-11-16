"""Finish independent checks after correcting a missing mandatory command argument."""
import importlib.util
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("landed_tuple_checks", ROOT / "run.py")
checks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checks)
checks.COMMANDS = json.loads((ROOT / "commands.json").read_text())
psh = ["pwsh", "-NoLogo", "-NoProfile", "-File"]
builds = checks.ROOT / "references/Hexalith.Builds"
checks.run("catalog-exceptions-corrected", psh + ["Tools/validate-package-version-exceptions.ps1",
           "-InventoryPath", "Tools/package-version-exceptions.json", "-CatalogPath",
           "Props/Directory.Packages.props"], builds)
checks.run("current-audit-refusal", psh + ["Tools/validate-package-version-audit.ps1"], builds, expected=1)
checks.require("no matching accepted audit selection" in (ROOT / "current-audit-refusal.log").read_text(),
               "Unexpected audit blocker")
checks.run("current-source-proposal-refusal", ["python3", "eng/story117-platform-qualification-current/verify.py"],
           expected=1)
after = checks.snapshot()
checks.require(json.loads((ROOT / "before.json").read_text()) == after,
               "Owner revisions, indices, status or protected inputs changed during checks")
checks.write("checks-after.json", after)
print("Independent local checks complete; no remote action or owning repository mutation occurred.")
