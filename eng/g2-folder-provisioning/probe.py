"""Run the native Folder provisioning phases against a private, retained Dapr actor store."""

import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import uuid
import xml.etree.ElementTree as ET


DAPR = "daprio/daprd@sha256:ed471547b67db50c831d498b8ee861822fb037d9d594c38ac299546b425f575a"
CONTROL = "daprio/dapr@sha256:16ca01236a97825e30b19ef9c89c7c9006fabdd9036a6d7e8947662371418851"
REDIS = "redis@sha256:7f7d30b7fdd09467bac58198a1f166dd69789d348d4fe859cccb20e0cf287fbe"
DOTNET = "mcr.microsoft.com/dotnet/sdk@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea"
FIXTURE_CLASS = "Hexalith.Folders.Server.Tests.Provisioning.ProvisioningQualificationTests"
APP_ID = "g2-folders-fixture"
ACTOR_STORE = """apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
  - name: actorStateStore
    value: "true"
"""


def docker(*args, check=True, timeout=45, environment=None):
    result = subprocess.run(["docker", *args], check=False, timeout=timeout, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            env=os.environ | (environment or {}))
    if check and result.returncode:
        raise RuntimeError(result.stdout.strip())
    return result.stdout.strip()


def run(assembly, results):
    name = f"folders-g2-{uuid.uuid4().hex[:12]}"
    containers = []
    observations = {}
    token = uuid.uuid4().hex
    with tempfile.TemporaryDirectory(prefix="g2-dapr-") as scratch:
        components = Path(scratch)
        components.chmod(0o755)
        (components / "statestore.yaml").write_text(ACTOR_STORE, encoding="utf-8")
        docker("network", "create", "--internal", name)

        def container(role, *args, environment=None):
            instance = f"{name}-{role}"
            containers.append(instance)
            env_flags = [argument for key in (environment or {}) for argument in ("--env", key)]
            image_id = docker("run", "--pull", "never", "-d", "--name", instance,
                              *env_flags, *args, environment=environment)
            return instance, image_id

        try:
            redis, redis_id = container("redis", "--network", name, "--network-alias", "redis",
                                        REDIS, "redis-server", "--appendonly", "yes", "--appendfsync", "always")
            # The Redis container is retained across phases so its append-only files survive.
            placement, _ = container("placement", "--network", name, "--network-alias", "placement",
                                     CONTROL, "./placement")
            scheduler, _ = container("scheduler", "--network", name, "--network-alias", "scheduler",
                                     CONTROL, "./scheduler", "--etcd-data-dir=/var/lock/dapr/scheduler")

            def run_phase(phase, method):
                runner, runner_id = container(f"runner-{phase}", "--network", name, "--mount",
                                             f"type=bind,src={assembly.parent},dst=/tests,readonly", "--mount",
                                             f"type=bind,src={results},dst=/results", DOTNET,
                                             "sleep", "infinity")
                sidecar, sidecar_id = container(f"sidecar-{phase}", "--network", f"container:{runner}",
                                               "--mount",
                                               f"type=bind,src={components},dst=/components,readonly", DAPR,
                                               "/daprd", "--app-id", APP_ID, "--app-port", "8080",
                                               "--enable-mtls=false", "--dapr-http-port", "3500",
                                               "--dapr-grpc-port", "50001", "--placement-host-address", "placement:50005",
                                               "--scheduler-host-address", "scheduler:50006", "--resources-path", "/components",
                                               environment={"APP_API_TOKEN": token})
                process = subprocess.run(
                    ["docker", "exec", "--user", f"{os.getuid()}:{os.getgid()}",
                                "--env", f"G2_QUALIFICATION_PHASE={phase}", "--env", "DOTNET_CLI_HOME=/tmp",
                                "--env", "DAPR_HTTP_ENDPOINT=http://127.0.0.1:3500",
                                "--env", "DAPR_GRPC_ENDPOINT=http://127.0.0.1:50001",
                                "--env", "G2_QUALIFICATION_CHANNEL", runner, "dotnet",
                                f"/tests/{assembly.name}", "-method", f"{FIXTURE_CLASS}.{method}", "-failSkips",
                    "-result-xml", f"/results/{phase}.xml"],
                    capture_output=True, text=True, check=False, timeout=100,
                    env=os.environ | {"G2_QUALIFICATION_CHANNEL": token})
                output = process.stdout + process.stderr
                print(output, flush=True)
                (results / f"{phase}.log").write_text(output + "\n", encoding="utf-8")
                (results / f"{phase}-sidecar.log").write_text(docker("logs", sidecar, check=False) + "\n", encoding="utf-8")
                if process.returncode != 0:
                    raise RuntimeError(f"{phase} native test returned {process.returncode}")
                docker("rm", "-f", sidecar)
                docker("rm", "-f", runner)
                # Inspect both exact identities after removal. A previous application or sidecar must no longer serve.
                for identity in (runner_id, sidecar_id):
                    inspection = subprocess.run(["docker", "inspect", identity], capture_output=True, check=False, timeout=15)
                    if inspection.returncode == 0:
                        raise RuntimeError(f"Prior {phase} runtime is still present")
                receipt = results / f"{phase}.json"
                if not receipt.is_file():
                    raise RuntimeError(f"{phase} failed to write a passing receipt")
                assemblies = ET.parse(results / f"{phase}.xml").getroot().findall("assembly")
                if len(assemblies) != 1 or any(assemblies[0].get(key) != value for key, value in {
                    "total": "1", "passed": "1", "failed": "0", "skipped": "0", "errors": "0",
                }.items()):
                    raise RuntimeError(f"{phase} lacks exactly one passing native test")
                observations[phase] = {"runnerId": runner_id, "sidecarId": sidecar_id, "runtimeRemoved": True}

            run_phase("before-restart", "CommitAndLoseReply")
            previous = docker("inspect", "--format", "{{.State.StartedAt}}", redis)
            docker("restart", redis)
            current = docker("inspect", "--format", "{{.State.StartedAt}}", redis)
            if previous == current:
                raise RuntimeError("Redis did not restart")
            for _ in range(30):
                if docker("exec", redis, "redis-cli", "ping", check=False) == "PONG":
                    break
                time.sleep(1)
            else:
                raise RuntimeError("Redis did not return from restart")
            run_phase("after-restart", "RecoverWithMutationsFrozen")
            observations["redis"] = {"containerId": redis_id, "startedBefore": previous, "startedAfter": current}
            observations["images"] = {"daprd": DAPR, "control": CONTROL, "redis": REDIS, "dotnet": DOTNET}
            (results / "topology.json").write_text(json.dumps(observations, indent=2) + "\n", encoding="utf-8")
        finally:
            for instance in reversed(containers):
                docker("rm", "-fv", instance, check=False)
            docker("network", "rm", name, check=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assembly", type=Path)
    parser.add_argument("results", type=Path)
    args = parser.parse_args()
    args.results.mkdir(parents=True, exist_ok=False)
    run(args.assembly.resolve(strict=True), args.results.resolve(strict=True))
