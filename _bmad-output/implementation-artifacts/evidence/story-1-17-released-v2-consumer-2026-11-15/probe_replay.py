"""Prove optimized replay refuses corruption and rebound unsafe controls."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

source = Path(__file__).resolve().parent
private = Path(os.environ["TMPDIR"]) / "evidence"
shutil.copytree(source, private)
results = []


def run(name, expected_text):
    process = subprocess.run([sys.executable, "-O", str(private / "verify.py")],
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if process.returncode == 0 or expected_text not in process.stdout:
        raise RuntimeError("Optimized replay accepted " + name)
    results.append({"name": name, "verifier_exit_code": process.returncode,
                    "expected_refusal": expected_text})


package = private / "final/packages/Hexalith.Folders.Client.2.0.0.nupkg"
original = package.read_bytes()
package.write_bytes(original[:-1] + bytes([original[-1] ^ 1]))
run("modified package archive", "Retained file changed")
package.write_bytes(original)
receipt = json.loads((private / "receipt.json").read_text())
receipt["controls"]["activation_authority"] = "fabricated"
(private / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
manifest = json.loads((private / "artifact-manifest.json").read_text())
manifest["receipt.json"] = hashlib.sha256((private / "receipt.json").read_bytes()).hexdigest()
(private / "artifact-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
run("rebound unsafe activation authority", "Unsafe operational controls")
(source / "replay-probes.json").write_text(json.dumps(results, indent=2) + "\n")
print("PASS: optimized replay refused both independent probes")
