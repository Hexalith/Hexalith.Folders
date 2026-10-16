"""Check an inert Story 1.17 proposal; never execute or qualify a deployment."""

import hashlib
import json
from pathlib import Path
import re
import sys


PACKET = Path(__file__).resolve().parent
WORKSPACE = PACKET.parent.parent
SOURCES = (
    "src/Hexalith.Folders.Server/FoldersApiRouting.cs",
    "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
    "src/Hexalith.Folders.Server/Authentication/FoldersOidcOptions.cs",
    "src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs",
    "src/Hexalith.Folders.Server/Authentication/HumanAuthorityReceiptVerifier.cs",
    "src/Hexalith.Folders.Server/FoldersApiRouteTelemetry.cs",
    "src/Hexalith.Folders.Workers/FoldersWorkersHostComposition.cs",
    "src/Hexalith.Folders.Workers/FoldersTenantEventSubscriptionOptionsValidator.cs",
    "src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs",
    "src/Hexalith.Folders.UI/CompositionRoot.cs",
    "src/Hexalith.Folders.Client/FoldersClientOptions.cs",
    "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
    "src/Hexalith.Folders.ServiceDefaults/Extensions.cs",
    "src/Hexalith.Folders/Projections/TenantAccess/EventStoreFolderTenantAccessProjectionStore.cs",
    "src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml",
    "deploy/dapr/production/sidecar-config-bindings.yaml",
    "docs/operations/container-images-and-dapr-app-ids.md",
    "docs/runbooks/rollback.md",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/ProjectsServerServiceCollectionExtensions.cs",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/FoldersProjectFolderDirectory.cs",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/FoldersProjectFileReferenceDirectory.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/WorkloadAuthenticationOptions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/WorkloadAssertionIssuerOptions.cs",
    "references/Hexalith.Builds/Props/Directory.Packages.props",
    "references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md",
)
CHECKS = (
    "artifact-provenance", "first-install-baseline-selection",
    "platform-profile-and-isolation", "attempt-controls", "readiness",
    "authentication-and-authorization-negatives", "durable-end-state",
    "projects-lifecycle", "projects-permissions", "projects-file-metadata",
    "consumer-attribution", "route-restart-and-reversal",
    "retained-data-recovery", "verification-and-records",
)
MODULE_KEYS = {
    "folders-server": (
        "Folders:ApiRouting:Mode", "Folders:Authentication:Authority",
        "Folders:Authentication:MetadataAddress", "Folders:Authentication:Audience",
        "Folders:Authentication:ValidIssuer", "Folders:Authentication:RequireHttpsMetadata",
        "Authentication:JwtBearer:Authority", "Authentication:JwtBearer:Issuer",
        "Authentication:Workload:Audience", "Authentication:Workload:AllowedCallers:0",
        "Authentication:HumanAuthorityReceipt:SigningKey", "APP_API_TOKEN",
    ),
    "folders-workers": ("Folders:Workers:PubSubName", "APP_API_TOKEN"),
    "folders-ui": (
        "Folders:Authentication:Mode", "Folders:Authentication:Authority",
        "Folders:Authentication:ClientId", "Folders:Authentication:RequireHttpsMetadata",
        "Folders:Client:BaseAddress",
    ),
    "folders-sdk": ("Folders:BaseAddress",),
    "eventstore-inputs": (
        "EventStore:Publisher:TopicOverrides:folders",
        "Authentication:WorkloadIssuer:ClientId", "Authentication:WorkloadIssuer:ClientSecret",
        "Authentication:HumanAuthorityReceipt:SigningKey",
        "EventStore:IdempotencyAdmission:Enabled", "EventStore:IdempotencyAdmission:DigestKeySource",
        "EventStore:IdempotencyAdmission:ActiveDigestKeyVersion",
        "EventStore:IdempotencyAdmission:DigestKeySecretStoreName",
        "EventStore:IdempotencyAdmission:DigestKeySecretName",
        "EventStore:IdempotencyAdmission:DigestKeySecretGeneration",
    ),
    "folders-server-and-workers-telemetry": ("OTEL_EXPORTER_OTLP_ENDPOINT",),
}
FIXED_VALUES = {
    ("folders-server", "Folders:ApiRouting:Mode"): "V1Only",
    ("folders-server", "Folders:Authentication:RequireHttpsMetadata"): True,
    ("folders-server", "Authentication:Workload:Audience"): "folders",
    ("folders-server", "Authentication:Workload:AllowedCallers:0"): "eventstore",
    ("folders-workers", "Folders:Workers:PubSubName"): "folders-workers-pubsub",
    ("folders-ui", "Folders:Authentication:Mode"): "oidc",
    ("folders-ui", "Folders:Authentication:RequireHttpsMetadata"): True,
    ("eventstore-inputs", "EventStore:Publisher:TopicOverrides:folders"): "folders.events",
    ("eventstore-inputs", "EventStore:IdempotencyAdmission:Enabled"): True,
    ("eventstore-inputs", "EventStore:IdempotencyAdmission:DigestKeySource"): "DaprSecret",
}
BINDINGS = (
    "release-and-hosting", "profile", "namespaces-and-network", "read-model", "broker",
    "realm-and-workload-auth", "projects-client-delegation", "ui-confidential-client",
    "provider-tenancy-and-egress", "secret-contract", "telemetry", "attempt-controls", "recovery",
)
DEPENDENCIES = {
    "eventstore": ("eventstore", "3.121.0"), "tenants": ("tenants", "5.8.0"),
    "memories": ("memories", "2.27.1"), "folders-server": ("folders", "1.2.1"),
    "folders-workers": ("folders-workers", "1.2.1"), "folders-ui": ("folders-ui", "1.2.1"),
    "projects-server": ("projects", "1.0.0"), "frontcomposer": (None, "4.6.0"),
}
OPERATIONS = (
    ("GetFolderLifecycleStatusAsync", "GET", "/api/v2/folders/{folderId}/lifecycle-status"),
    ("GetEffectivePermissionsAsync", "GET", "/api/v2/folders/{folderId}/effective-permissions"),
    ("GetFolderFileMetadataAsync", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/metadata"),
)


class PacketError(ValueError):
    """A proposal is inconsistent or claims authority/evidence it cannot carry."""


def require(condition, message):
    if not condition:
        raise PacketError(message)


def exact(actual, expected, label):
    require(type(actual) is type(expected) and actual == expected, f"{label}: expected {expected!r}")


def keys(value, expected, label):
    require(isinstance(value, dict) and set(value) == set(expected), f"{label}: required keys differ")


def nonempty(value, label):
    require(isinstance(value, str) and bool(value.strip()), f"{label}: non-empty text required")


def texts(value, label):
    require(isinstance(value, list) and len(value) > 0, f"{label}: evidence/procedure list required")
    for item in value:
        nonempty(item, label)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result


def reject_constant(value):
    raise PacketError(f"non-standard JSON constant: {value}")


def read_json(directory, filename):
    path = directory / filename
    require(not path.is_symlink(), f"{filename}: packet symlinks are not accepted")
    with path.open(encoding="utf-8") as stream:
        return json.load(stream, object_pairs_hook=unique_object, parse_constant=reject_constant)


def validate_packet(packet_dir=PACKET, source_root=WORKSPACE):
    """Read the two packet JSON files and only the fixed source allowlist."""
    config = read_json(Path(packet_dir), "configuration.proposed.json")
    plan = read_json(Path(packet_dir), "staging-checks.json")
    keys(config, (
        "schema", "status", "qualification", "productionControl", "installationProposal",
        "acceptedEnvelope", "sourceSnapshots", "sourceInputs", "releaseIdentity",
        "moduleConfiguration", "platformBindings", "secretReferences", "dependencies",
        "projectsReadOnlyOperations",
    ), "configuration")
    keys(plan, ("schema", "status", "qualification", "productionControl", "executionScope", "evidencePolicy", "checks"), "staging")
    exact(config["schema"], "story117-platform-qualification.configuration.v1", "configuration schema")
    for doc, name in ((config, "configuration"), (plan, "staging")):
        exact(doc.get("status"), "proposed-inert", f"{name} status")
        exact(doc.get("qualification"), "HOLD", f"{name} qualification")
    keys(config["productionControl"], (
        "routeMode", "T0", "activationAuthority", "deploymentAuthorized",
        "publicationAuthorized", "trackerTransitionAuthorized",
    ), "productionControl")
    for key, value in config["productionControl"].items():
        expected = "V1Only" if key == "routeMode" else None if key in ("T0", "activationAuthority") else False
        exact(value, expected, f"productionControl.{key}")

    proposal = config["installationProposal"]
    keys(proposal, ("branch", "confirmed", "productionWorkingBaseline", "retainedApplicationData",
                    "owner", "observationScope", "branchRule"), "installationProposal")
    exact(proposal["branch"], "first-install-in-observed-target", "installation branch")
    exact(proposal["confirmed"], False, "installation confirmed")
    for key in ("productionWorkingBaseline", "retainedApplicationData"):
        exact(proposal[key], None, f"installationProposal.{key}")
    for key in ("owner", "observationScope", "branchRule"):
        nonempty(proposal[key], f"installationProposal.{key}")
    envelope = config["acceptedEnvelope"]
    expected_envelope = dict(topology="single-node", rpoSeconds=3600, rtoSeconds=14400,
                             backupIntervalSeconds=1800, frequentRetentionDays=7, dailyRetentionDays=30)
    keys(envelope, (*expected_envelope, "rtoScope"), "acceptedEnvelope")
    for key, value in expected_envelope.items():
        exact(envelope[key], value, f"acceptedEnvelope.{key}")
    nonempty(envelope["rtoScope"], "rtoScope")

    snapshots = config["sourceSnapshots"]
    keys(snapshots, ("Folders", "Projects", "Platform", "EventStore", "Builds"), "sourceSnapshots")
    for name, row in snapshots.items():
        keys(row, ("revision", "scope", "state", "dirtyInputs"), f"snapshot {name}")
        require(isinstance(row["revision"], str) and re.fullmatch(r"[0-9a-f]{40}", row["revision"]),
                f"snapshot {name}: full canonical revision required")
        exact(row["scope"], "local-checkout", f"snapshot {name} scope")
        exact(row["state"], "dirty" if name in ("Folders", "Builds") else "not-attested", f"snapshot {name} state")
        if name in ("Folders", "Builds"):
            texts(row["dirtyInputs"], f"snapshot {name} dirtyInputs")
        else:
            exact(row["dirtyInputs"], [], f"snapshot {name} dirtyInputs")
    require("Props/Directory.Packages.props" in snapshots["Builds"]["dirtyInputs"], "Builds dirty source input missing")
    inputs = config["sourceInputs"]
    keys(inputs, SOURCES, "sourceInputs")
    source_root = Path(source_root).resolve()
    for relative in SOURCES:
        digest = inputs[relative]
        require(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest), f"source digest invalid: {relative}")
        path = (source_root / relative).resolve()
        require(path.is_relative_to(source_root), f"source escapes workspace: {relative}")
        require(hashlib.sha256(path.read_bytes()).hexdigest() == digest, f"source changed: {relative}")

    release = config["releaseIdentity"]
    keys(release, ("owner", "boundary", "sourceProvenance", "exactFoldersClientContractsPair",
                   "moduleImageDigests", "applicationPackageDigest", "composedEventStoreDigest",
                   "releaseRecord", "successfulExactRevisionCI", "priorV1Deployment"), "releaseIdentity")
    for key, value in release.items():
        if key in ("owner", "boundary"):
            nonempty(value, f"releaseIdentity.{key}")
        else:
            exact(value, None, f"releaseIdentity.{key}")
    modules = config["moduleConfiguration"]
    keys(modules, MODULE_KEYS, "moduleConfiguration")
    for name, required in MODULE_KEYS.items():
        row = modules[name]
        keys(row, ("owner", "source", "values", "boundary"), f"module {name}")
        for key in ("owner", "boundary"):
            nonempty(row[key], f"module {name}.{key}")
        require(row["source"] in SOURCES, f"module {name}: source not allowlisted")
        keys(row["values"], required, f"module {name} values")
        for key, value in row["values"].items():
            exact(value, FIXED_VALUES.get((name, key)), f"module {name}.{key}")
    keys(config["platformBindings"], BINDINGS, "platformBindings")
    for name, row in config["platformBindings"].items():
        keys(row, ("value", "owner", "reason", "contract"), f"binding {name}")
        exact(row["value"], None, f"binding {name} unresolved value")
        for key in ("owner", "reason", "contract"):
            nonempty(row[key], f"binding {name}.{key}")

    secrets = config["secretReferences"]
    keys(secrets, ("folders-app-channel", "workers-app-channel", "tenants-app-channel", "human-receipt",
                   "workload-client-id", "workload-client-secret", "idempotency-key-ring"), "secretReferences")
    sidecar_source = (source_root / "deploy/dapr/production/sidecar-config-bindings.yaml").read_text(encoding="utf-8")
    for name, row in secrets.items():
        keys(row, ("consumers", "sourceName", "sourceKey", "targetKeys", "environmentSource", "value", "owner", "boundary"), f"secret {name}")
        for key in ("consumers", "targetKeys"):
            texts(row[key], f"secret {name}.{key}")
        for key in ("sourceName", "sourceKey", "owner", "boundary"):
            nonempty(row[key], f"secret {name}.{key}")
        require(row["sourceName"] in sidecar_source, f"secret {name}: source reference missing")
        for key in ("environmentSource", "value"):
            exact(row[key], None, f"secret {name}.{key}")
    keys(config["dependencies"], DEPENDENCIES, "dependencies")
    for name, (app_id, pin) in DEPENDENCIES.items():
        row = config["dependencies"][name]
        keys(row, ("appId", "catalogInputPin", "qualifiedArtifact", "owner"), f"dependency {name}")
        exact(row["appId"], app_id, f"dependency {name} appId")
        exact(row["catalogInputPin"], pin, f"dependency {name} catalogInputPin")
        exact(row["qualifiedArtifact"], None, f"dependency {name} qualifiedArtifact")
        nonempty(row["owner"], f"dependency {name} owner")
    operations = config["projectsReadOnlyOperations"]
    require(isinstance(operations, list) and len(operations) == len(OPERATIONS), "required Projects read families differ")
    for row, (operation, method, path) in zip(operations, OPERATIONS):
        keys(row, ("operation", "method", "path", "mutates"), "Projects operation")
        for key, value in dict(operation=operation, method=method, path=path, mutates=False).items():
            exact(row[key], value, f"Projects operation {operation}.{key}")

    exact(plan["schema"], "story117-platform-qualification.staging.v1", "staging schema")
    keys(plan["productionControl"], ("routeMode", "T0", "activationAuthority"), "staging productionControl")
    for key, value in plan["productionControl"].items():
        exact(value, config["productionControl"][key], f"staging productionControl.{key}")
    exact(plan["executionScope"], "staging-only-external-execution", "staging executionScope")
    nonempty(plan["evidencePolicy"], "staging evidencePolicy")
    checks = plan["checks"]
    require(isinstance(checks, list) and len(checks) == len(CHECKS), "required staging checks differ")
    for order, (row, check_id) in enumerate(zip(checks, CHECKS), 1):
        keys(row, ("id", "order", "owner", "maxDurationSeconds", "preconditions", "procedure",
                   "requiredEvidence", "status", "result", "observedEvidence", "startedAt", "completedAt"), f"check {check_id}")
        exact(row["id"], check_id, "required staging check order")
        exact(row["order"], order, f"check {check_id} order")
        nonempty(row["owner"], f"check {check_id} owner")
        duration = row["maxDurationSeconds"]
        require(type(duration) is int and 0 < duration <= 14400, f"check {check_id}: finite duration required")
        for key in ("preconditions", "procedure", "requiredEvidence"):
            texts(row[key], f"check {check_id}.{key}")
        exact(row["status"], "unexecuted", f"check {check_id} status")
        for key in ("result", "startedAt", "completedAt"):
            exact(row[key], None, f"check {check_id}.{key}: no executed evidence accepted")
        exact(row["observedEvidence"], [], f"check {check_id}.observedEvidence: no executed evidence accepted")


def main():
    if len(sys.argv) != 1:
        print("FAIL: this offline diagnostic accepts no arguments or execution/approval mode.", file=sys.stderr)
        return 2
    try:
        validate_packet()
    except (OSError, ValueError, TypeError) as error:
        print(f"FAIL: packet consistency: {error}; operational qualification remains HOLD.", file=sys.stderr)
        return 1
    print("PASS: packet consistency only; operational qualification remains HOLD; "
          "staging checks executed: 0; V1Only; T0 unset; no activation authority.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
