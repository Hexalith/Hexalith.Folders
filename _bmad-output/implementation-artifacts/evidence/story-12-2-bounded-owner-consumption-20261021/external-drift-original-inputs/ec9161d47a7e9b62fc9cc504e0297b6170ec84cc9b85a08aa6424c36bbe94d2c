"""Offline real selected sequence with controlled UTC/monotonic, HTTP and SQL; no live credit."""
from contextlib import redirect_stdout
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import threading
import types
import unittest
from unittest.mock import patch
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as xml

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools"))
import access_telemetry_functional_retention as f
from access_telemetry_producer_common import _qualification_record_ids


class SelectedActiveSequenceTests(unittest.TestCase):
    def run_sequence(self, variant="pass"):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            root = base / "run"
            candidate = base / "platform" / "references" / "Hexalith.Memories"
            for path in (root / "native-tests", root / "source-mapping", root / "platform-components" / "retention", candidate / "tools", candidate / "module", candidate / "bin"):
                path.mkdir(parents=True, mode=0o700)
            root.chmod(0o700)
            for path in root.rglob("*"):
                if path.is_dir(): path.chmod(0o700)
            clock = types.SimpleNamespace(seconds=1792144800.0, monotonic=100.0)
            def now():
                return datetime.fromtimestamp(clock.seconds, timezone.utc)
            class Utc(datetime):
                @classmethod
                def now(cls, tz=None):
                    return datetime.fromtimestamp(clock.seconds, tz)
            def write(path, value):
                path.write_text(json.dumps(value, separators=(",", ":")))
                path.chmod(0o600)
            entry = candidate / "tools/verify-access-telemetry-lifecycle.py"
            entry.write_text("# inert fixture entry\n")
            manifest = candidate / "module/hexalith.module-manifest.v2.json"
            manifest.write_text("{}")
            mapping = {"Mode": "source", "Root": str(candidate.parent.parent), "ActiveModule": "Hexalith.Platform", "Entries": [{"Identity": "Hexalith.Memories", "Origin": "source", "Path": str(candidate)}], "HasGit": False, "PlatformIdentity": None, "IdentityFindings": []}
            mapping_hash = f._sha(json.dumps(mapping, separators=(",", ":")).encode())
            write(root / "source-mapping/mapping.json", mapping)
            pins = {item["id"]: item["reference"] for item in f.artifact_catalog()["images"]}
            profile = {"purpose": "local-functional-retention", "runId": "offline-run", "sourceMappingHash": mapping_hash, "componentType": "state.postgresql/v2", "runtimeImage": pins["daprd"], "postgresqlImage": pins["postgresql"], "sslMode": "verify-full", "retentionSeconds": 3600, "qualificationCredit": False}
            raw_profile = json.dumps(profile, separators=(",", ":"))
            loaded = {}
            names = {"memories": "Hexalith.Memories.Server.dll", "memories-access-telemetry": "Hexalith.Memories.AccessTelemetry.dll", "memories-access-telemetry-clock": "Hexalith.Memories.AccessTelemetry.Clock.dll"}
            for resource, assembly in names.items():
                path = candidate / "bin" / assembly
                path.write_bytes(b"offline mapped fixture bytes")
                loaded[resource + "/bin/" + assembly] = f._sha(path.read_bytes())
            selection = {"moduleId": "memories", "name": "functional-retention", "entry": "tools/verify-access-telemetry-lifecycle.py", "mode": "source", "daprRuntimeVersion": "1.18.1", "executionTimeoutSeconds": 14400, "stopTimeoutSeconds": 900, "executionDeadlineUtc": (now() + timedelta(hours=4)).isoformat(), "manifestHash": f._sha(manifest.read_bytes()).upper(), "entryHash": f._sha(entry.read_bytes()).upper()}
            plan = {"schema": "hexalith.g4-run-plan.v1", "runId": "offline-run", "selectedTest": selection, "sourceMapping": {"root": str(candidate.parent.parent)}, "sourceMappingHash": mapping_hash}
            gate_path = root / "platform-components/retention/gate.json"
            access_path = gate_path.with_name("access.json")
            observation = {"isComplete": True, "runId": "offline-run", "moduleId": "memories", "runtimeArtifactVerified": True, "profileJson": raw_profile, "profileHash": f._sha(raw_profile.encode()), "observedBackendIdentity": pins["postgresql"] + "/180006|t|TLSv1.3|telemetry_observer|on/sha256:" + "f" * 64, "observedCandidateHash": f._sha(json.dumps([{"Key": k, "Value": v} for k, v in sorted(loaded.items())], separators=(",", ":")).encode()), "observedRuntimeHash": "c" * 64, "loadedArtifactHashes": loaded, "serviceRetentionSeconds": {name: 3600 for name in names}, "observedAtUtc": now().isoformat(), "accessPath": str(access_path), "gatePath": str(gate_path), "postgresqlContainerId": "d" * 64, "endpoints": {name + suffix: "http://127.0.0.1:12345" for name in names for suffix in ("", "/dapr")}}
            readiness = {"schema": "hexalith.g4-run-readiness.v1", "runId": "offline-run", "selectedTest": selection, "resources": [{"state": "Healthy"}], "testObservation": observation}
            write(root / "plan.json", plan)
            write(root / "readiness.json", readiness)
            write(access_path, {name: {"APP_API_TOKEN": "fixture-app-secret", "DAPR_API_TOKEN": "fixture-dapr-secret"} for name in names})
            def gate():
                write(gate_path, {"schemaVersion": 1, "state": "disabled" if variant == "withdrawal" and clock.monotonic > 1000 else "enabled", "profileSha256": observation["profileHash"], "expiresUtcMs": int(min(clock.seconds + 600, datetime.fromisoformat(selection["executionDeadlineUtc"]).timestamp()) * 1000)})
            gate()
            def sleep(seconds):
                clock.seconds += seconds
                clock.monotonic += seconds
                if variant == "deadline" and clock.monotonic > 1000:
                    clock.seconds += 14400
                    clock.monotonic += 14400
                gate()
            records, emitted = {}, {}
            record_keys = {segment: f.fixed_record_keys("offline-run", segment) for segment in ("cohort-a", "cohort-b")}
            def iso(milliseconds):
                return datetime.fromtimestamp(milliseconds / 1000, timezone.utc).isoformat()
            calls = []
            def http(_, request, **kwargs):
                calls.append(request.full_url)
                headers = {key.lower(): value for key, value in request.headers.items()}
                self.assertIn(headers["dapr-api-token"], {"fixture-app-secret", "fixture-dapr-secret"})
                path = unquote(urlsplit(request.full_url).path)
                if path.endswith("/fixed-workload"):
                    segment = headers["x-hexalith-qualification-segment"]
                    start = int(headers["x-hexalith-qualification-emitted-utc-ms"])
                    emitted[segment] = start
                    ids = _qualification_record_ids("offline-run", segment)
                    for ordinal, (key, identity) in enumerate(zip(record_keys[segment], ids)):
                        records[key] = {"recordId": identity, "envelopeHash": "e" * 64, "emittedAtUtc": iso(start + ordinal), "expiresAtUtc": iso(start + ordinal + 3600000), "acceptedAtUtc": iso(start), "payload": {"message": "original"}}
                    value = {"runId": "offline-run", "segmentId": segment, "writer": "writer-1", "recordIds": ids, "startedUtcMs": start, "finishedUtcMs": start, "emitFinishedUtcMs": start, "acknowledgedUtcMs": start, "resultCount": 1, **{key: 125 for key in ("attempted", "enqueued", "acknowledged", "persisted", "transactionAcknowledgements")}, **{key: 0 for key in ("conflicted", "rejected", "dropped")}}
                elif "/actors/" in path:
                    purged = "cohort-a" in emitted and clock.seconds * 1000 >= emitted["cohort-a"] + 3600124
                    value = {"ReminderSequence": 2 if purged else 1, "LastPurgeUnixMilliseconds": int(clock.seconds * 1000) if purged else 0}
                else:
                    key = path.split("/access-telemetry-store/", 1)[1]
                    value = records.get(key)
                    a_expired = "cohort-a" in emitted and clock.seconds * 1000 >= emitted["cohort-a"] + 3600124
                    if key in record_keys["cohort-a"] and a_expired:
                        value = None
                    if key in record_keys["cohort-b"] and a_expired:
                        if variant == "b-loss": value = None
                        if variant == "b-mutation": value = {**value, "payload": {"message": "mutated"}}
                return io.BytesIO(json.dumps(value).encode())
            sql_calls = []
            def sql(argv, **kwargs):
                sql_calls.append(argv)
                self.assertEqual("127.0.0.1", argv[argv.index("-h") + 1])
                self.assertIn("PGSSLMODE=verify-full", argv)
                self.assertEqual(f.fixed_physical_bound_query("offline-run"), argv[-1])
                if variant == "sql-latency":
                    clock.seconds += 10
                    clock.monotonic += 10
                rows = []
                # Normal cleanup already removed physical tuples before the first post-expiry poll.
                if "cohort-a" in emitted and clock.seconds * 1000 < emitted["cohort-a"] + 3600000:
                    for ordinal, key in enumerate(record_keys["cohort-a"]):
                        row = {"recordId": records[key]["recordId"], "record": records[key], "expiresUtcMs": emitted["cohort-a"] + ordinal + 3600000 + 1000}
                        rows.append(json.loads(json.dumps(row)))
                    if variant == "missing-ttl": rows.pop()
                    if variant == "early-ttl": rows[0]["expiresUtcMs"] -= 2000
                    if variant == "late-ttl": rows[0]["expiresUtcMs"] += 900000
                    if variant == "row-drift": rows[0]["record"]["payload"]["message"] = "foreign"
                result = {"serverVersionNum": "180006", "ssl": variant != "non-tls", "tlsVersion": "TLSv1.3", "role": "telemetry_observer", "readOnly": "on", "observedUtcMs": int(clock.seconds * 1000), "rows": rows}
                return types.SimpleNamespace(stdout=json.dumps(result).encode(), stderr=b"")
            report = root / "native-tests/native.trx"
            if variant == "persist-failure": write(report.with_name("functional-observations.json"), {"occupied": True})
            outside = base / "outside"
            if variant == "symlink-output":
                outside.mkdir(mode=0o700)
                (root / "native-tests").rmdir()
                (root / "native-tests").symlink_to(outside, target_is_directory=True)
            env = {"HEXALITH_G4_TEST_NAME": "functional-retention", "HEXALITH_G4_TEST_OWNER": "memories", "HEXALITH_G4_TEST_MODE": "source", "HEXALITH_G4_RUN_ID": "offline-run", "HEXALITH_G4_PLAN_PATH": str(root / "plan.json"), "HEXALITH_G4_READINESS_PATH": str(root / "readiness.json"), "HEXALITH_G4_TEST_REPORT_PATH": str(report), "HEXALITH_G4_TEST_MANIFEST_HASH": selection["manifestHash"], "HEXALITH_G4_TEST_ENTRY_HASH": selection["entryHash"], "HEXALITH_SOURCE_MAPPING_HASH": mapping_hash}
            with patch.dict(os.environ, env, clear=True), patch.object(f, "__file__", str(candidate / "tools/access_telemetry_functional_retention.py")), patch.object(f, "datetime", Utc), patch.object(f.time, "time", lambda: clock.seconds), patch.object(f.time, "monotonic", lambda: clock.monotonic), patch.object(f.time, "sleep", sleep), patch("urllib.request.OpenerDirector.open", http), patch.object(f.subprocess, "run", sql), redirect_stdout(io.StringIO()) as output:
                result = f.run_selected_platform_retention()
            self.assertNotIn("fixture-app-secret", output.getvalue())
            self.assertNotIn("fixture-dapr-secret", output.getvalue())
            status = json.loads(output.getvalue())
            if variant in {"pass", "sql-latency"}:
                self.assertEqual(0, result, status)
                self.assertEqual("functional-observations-passed", status["status"])
                detail = json.loads(report.with_name("functional-observations.json").read_text())
                self.assertEqual(125, len(detail["ttlEvidence"]["preExpiry"]["rows"]))
                self.assertEqual([], detail["ttlEvidence"]["lastObservation"]["rows"])
                self.assertEqual([], detail["purge"]["actorDeletedRecordIds"])
                self.assertEqual(125, len(detail["purge"]["ttlAbsentRecordIds"]))
                self.assertGreaterEqual(detail["cohortClocks"]["cohort-b"]["monotonicSeconds"] - detail["cohortClocks"]["cohort-a"]["monotonicSeconds"], 1800)
                self.assertTrue(calls and len(sql_calls) >= 3)
                self.assertEqual([], detail["ttlEvidence"]["preflight"]["rows"])
                first_expiry = min(row["record"]["expiresAtUtc"] for row in detail["ttlEvidence"]["preExpiry"]["rows"])
                self.assertLess(detail["ttlEvidence"]["preExpiry"]["observedUtcMs"], int(datetime.fromisoformat(first_expiry).timestamp() * 1000))
            else:
                self.assertEqual(1, result, status)
                self.assertEqual("rejected", status["status"])
                reasons = {"b-loss": "functional-selected-record-missing", "b-mutation": "functional-selected-canonical-cohort-changed", "missing-ttl": "functional-selected-ttl-bound-missing", "early-ttl": "functional-selected-ttl-bound-drift", "late-ttl": "functional-selected-ttl-bound-drift", "row-drift": "functional-selected-ttl-bound-drift", "non-tls": "functional-selected-postgres-session", "deadline": "functional-selected-expiry-or-clock-step", "withdrawal": "functional-selected-gate-withdrawn-or-expired", "persist-failure": "functional-selected-input-refused", "symlink-output": "functional-selected-native-output-refused"}
                self.assertEqual(reasons[variant], status["reason"])
            if variant == "symlink-output": self.assertEqual([], list(outside.iterdir()))
            else:
                counters = xml.parse(report).find("{*}ResultSummary/{*}Counters")
                self.assertEqual("1" if variant in {"pass", "sql-latency"} else "0", counters.attrib["passed"])
            return status

    def test_real_selected_sequence_reconciles_cleanup_before_first_poll(self):
        self.run_sequence()

    def test_real_selected_sequence_captures_ttl_before_sql_latency_crosses_last_checkpoint(self):
        self.run_sequence("sql-latency")

    def test_active_sql_uses_pinned_postgresql_v2_column_and_schema_contract(self):
        # Dapr v1.18.1/go.mod pins components-contrib v1.18.0. Its
        # state/postgresql/v2/postgresql.go CREATE TABLE defines value bytea
        # and expires_at timestamptz; metadata.go TableName appends "state".
        # https://raw.githubusercontent.com/dapr/components-contrib/v1.18.0/state/postgresql/v2/postgresql.go
        # https://raw.githubusercontent.com/dapr/components-contrib/v1.18.0/state/postgresql/v2/metadata.go
        query = f.fixed_physical_bound_query("offline-run")
        self.assertIn("FROM access_telemetry.lifecycle_state WHERE key IN (", query)
        self.assertIn("convert_from(value,'UTF8')::jsonb", query)
        self.assertIn("extract(epoch FROM expires_at)", query)
        self.assertNotIn("expiredate", query)
        self.assertIn("expiredate", f.fixed_expired_physical_query("offline-run", "cohort-a"))

    def test_strong_reads_keep_fixed_sixteen_concurrency_and_key_order(self):
        consumer = object.__new__(f._PlatformRetentionConsumer)
        consumer.segments = {"cohort-a": 0}
        consumer.check = lambda: None
        barrier = threading.Barrier(16)
        lock = threading.Lock()
        active = maximum = admitted = 0
        keys = f.fixed_record_keys("offline-run", "cohort-a")
        def request(resource, path, **kwargs):
            nonlocal active, maximum, admitted
            self.assertEqual("memories-access-telemetry/dapr", resource)
            self.assertTrue(path.endswith("?consistency=strong&metadata.partitionKey=access-telemetry"))
            key = unquote(urlsplit(path).path).split("/access-telemetry-store/", 1)[1]
            self.assertIn(key, keys)
            with lock:
                active += 1
                admitted += 1
                ordinal = admitted
                maximum = max(maximum, active)
            if ordinal <= 16:
                barrier.wait(timeout=5)
            with lock:
                active -= 1
            return key
        consumer.request = request
        observed = consumer.states("offline-run", "cohort-a")
        self.assertEqual(16, maximum)
        self.assertEqual(125, admitted)
        self.assertEqual(list(keys), [item["key"] for item in observed])
        self.assertEqual(list(keys), [item["value"] for item in observed])

    def test_strong_read_withdrawal_admits_no_later_batch(self):
        consumer = object.__new__(f._PlatformRetentionConsumer)
        consumer.segments = {"cohort-a": 0}
        withdrawn = threading.Event()
        barrier = threading.Barrier(16)
        admitted = []
        def check():
            f._require(not withdrawn.is_set(), "selected-gate-withdrawn-or-expired")
        consumer.check = check
        def request(resource, path, **kwargs):
            admitted.append(path)
            barrier.wait(timeout=5)
            withdrawn.set()
            return None
        consumer.request = request
        with self.assertRaisesRegex(f.FunctionalRetentionError, "functional-selected-gate-withdrawn-or-expired"):
            consumer.states("offline-run", "cohort-a")
        self.assertEqual(16, len(admitted))

    def test_real_selected_sequence_refuses_behavior_clock_and_custody_failures(self):
        for variant in ("b-loss", "b-mutation", "missing-ttl", "early-ttl", "late-ttl", "row-drift", "non-tls", "deadline", "withdrawal", "persist-failure", "symlink-output"):
            with self.subTest(variant=variant):
                self.run_sequence(variant)


if __name__ == "__main__":
    unittest.main()
