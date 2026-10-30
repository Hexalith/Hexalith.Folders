#!/usr/bin/python3
"""Qualification-only interception; incoming production controls remain distinct."""
import json
import os
from pathlib import Path
import sys
import subprocess

# The generated launchers supply an absolute immutable config as argv[1].
config_path, kind, *arguments = sys.argv[1:]
sys.path.insert(0, str(Path(config_path).parent / "harness"))
from qualify import GateFailure, controlled_child, require, sha  # noqa: E402

config = json.loads(Path(config_path).read_text())
incoming = dict(os.environ)
try:
    environment, added, argv = controlled_child(config, kind, arguments, incoming)
    target = config[kind]
    require(sha(target) == config[kind + "Sha256"], "Selected executable bytes changed: " + kind)
    row = dict(kind=kind, target=target, incomingArguments=arguments, addedArguments=argv[len(arguments):],
               incomingEnvironment=incoming, addedEnvironment=added, effectiveEnvironment=environment,
               effectiveArguments=argv, cwd=os.getcwd(), pid=os.getpid(), sdkExecuted=False)
    if arguments == ["--hexalith-controlled-interception-probe"]:
        row["probe"] = True
        with open(config["childLog"], "a") as stream:
            stream.write(json.dumps(row) + "\n")
        print("intercepted|certificate=false|serial=1")
        raise SystemExit(0)
    row["sdkExecuted"] = True
    row["log"] = str(Path(config["work"]) / "logs" / (kind + "-child-" + str(os.getpid()) + ".log"))
    with open(config["childLog"], "a") as stream:
        stream.write(json.dumps(row) + "\n")
    # Keep complete child diagnostics even when the production API bounds its own capture.
    with open(row["log"], "wb") as log:
        child = subprocess.Popen([target, *argv], env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        while chunk := child.stdout.read(65536):
            log.write(chunk)
            log.flush()
            sys.stdout.buffer.write(chunk)
            sys.stdout.buffer.flush()
        raise SystemExit(child.wait())
except GateFailure as error:
    with open(config["childLog"], "a") as stream:
        stream.write(json.dumps(dict(kind=kind, incomingArguments=arguments, incomingEnvironment=incoming,
                                    sdkExecuted=False, refusal=str(error))) + "\n")
    print("Qualification SDK child refused: " + str(error), file=sys.stderr)
    raise SystemExit(78)
