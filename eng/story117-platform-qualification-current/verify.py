"""Check current offline proposal consistency; never execute or accept a release."""

import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


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
MODULE_SOURCES = {
    "folders-server": "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
    "folders-workers": "src/Hexalith.Folders.Workers/FoldersTenantEventSubscriptionOptionsValidator.cs",
    "folders-ui": "src/Hexalith.Folders.UI/CompositionRoot.cs",
    "folders-sdk": "src/Hexalith.Folders.Client/FoldersClientOptions.cs",
    "eventstore-inputs": "deploy/dapr/production/sidecar-config-bindings.yaml",
    "folders-server-and-workers-telemetry": "src/Hexalith.Folders.ServiceDefaults/Extensions.cs",
}
SECRET_MAPPINGS = {
    "folders-app-channel": (
        "hexalith-folders-server-app-token", "token", ["folders"],
        ["APP_API_TOKEN", "dapr.io/app-token-secret"],
    ),
    "workers-app-channel": (
        "hexalith-folders-workers-app-token", "token", ["folders-workers"],
        ["APP_API_TOKEN", "dapr.io/app-token-secret"],
    ),
    "tenants-app-channel": (
        "hexalith-tenants-app-token", "token", ["tenants"],
        ["APP_API_TOKEN", "dapr.io/app-token-secret"],
    ),
    "human-receipt": (
        "hexalith-human-authority-receipt-signing-key", "signing-key", ["eventstore", "folders"],
        ["Authentication:HumanAuthorityReceipt:SigningKey"],
    ),
    "workload-client-id": (
        "hexalith-eventstore-workload-client", "client-id", ["eventstore"],
        ["Authentication:WorkloadIssuer:ClientId"],
    ),
    "workload-client-secret": (
        "hexalith-eventstore-workload-client", "client-secret", ["eventstore"],
        ["Authentication:WorkloadIssuer:ClientSecret"],
    ),
    "idempotency-key-ring": (
        "hexalith-eventstore-idempotency-keys", "generation, active-version and retained version entries",
        ["eventstore"], ["EventStore:IdempotencyAdmission:DigestKeySecretGeneration",
                         "EventStore:IdempotencyAdmission:ActiveDigestKeyVersion",
                         "EventStore:IdempotencyAdmission:DigestKeySecretName"],
    ),
}
# Bind reviewed requirement prose and staging safeguards independently of source
# fingerprints, which disposable mutation fixtures deliberately replace.
REQUIREMENT_FIELDS = (
    "installationProposal", "acceptedEnvelope", "runtimeConstraints",
    "admissionPrerequisites", "platformBindings",
)
REQUIREMENTS_SHA256 = "86ebbb6a3a532c91f3bbda64779d428201f56492e1cd24f850a797d46758702c"
STAGING_SHA256 = "8a8c40abbbd234ef64dbd2e714bbd170fb374bebd62c04acbfb8f8856088408c"
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
    "eventstore": ("eventstore", "3.124.0"), "tenants": ("tenants", "5.8.0"),
    "memories": ("memories", "2.27.1"), "folders-server": ("folders", "1.2.1"),
    "folders-workers": ("folders-workers", "1.2.1"), "folders-ui": ("folders-ui", "1.2.1"),
    "projects-server": ("projects", "1.0.0"), "frontcomposer": (None, "4.6.0"),
}
CATALOG_BINDINGS = {
    "eventstore": ("HexalithEventStoreVersion", "Hexalith.EventStore.Server"),
    "tenants": ("HexalithTenantsVersion", "Hexalith.Tenants.Contracts"),
    "memories": ("HexalithMemoriesVersion", "Hexalith.Memories.Contracts"),
    "folders-server": ("HexalithFoldersVersion", "Hexalith.Folders.Contracts"),
    "folders-workers": ("HexalithFoldersVersion", "Hexalith.Folders.Contracts"),
    "folders-ui": ("HexalithFoldersVersion", "Hexalith.Folders.Contracts"),
    "projects-server": ("HexalithProjectsVersion", "Hexalith.Projects.Contracts"),
    "frontcomposer": ("HexalithFrontComposerVersion", "Hexalith.FrontComposer.Contracts"),
}
SNAPSHOTS = {
    "Folders": "d06799dd70334366dddaee6ace6b9a472f78c93e",
    "Projects": "a8059addf27261c0f2bddd23d2b266ebcfa00660",
    "Platform": "b54dc5f2e8fe070db9a0ca3a72ad495b380d2b5e",
    "EventStore": "77d9af18a54d92506e310fe5a45021a395e66f60",
    "Builds": "4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77",
}
PUBLISHED_BUILDS = {
    "receiptPath": "_bmad-output/implementation-artifacts/evidence/story-1-17-builds-publication-2026-10-16.json",
    "receiptSha256": "deeb0cb92fe9356d6f47ab6a996445459df506f398fed95ea2088cdc89c64678",
    "version": "4.32.0",
    "sourceRevision": "abdfca89ed82f388b54664825a8a79fed73e7ccf",
    "signedPackageSha256": {
        "Hexalith.Builds.Evidence.Cli": "93b34284cdfd1995d8c892a79b58eade34e47392470726ab870e6a72a0d98bf2",
        "Hexalith.Builds.Module.Cli": "9e9a9daf825b0ec3a7d6dba2f3e4f473c375181509ed870b6f81e795553fc6d5",
    },
    "sourceCatalogObservation": {
        "path": "Props/Directory.Packages.props",
        "revision": "abdfca89ed82f388b54664825a8a79fed73e7ccf",
        "sha256": "a0e75c1387d7bf08fb9264fd2de9ef9b16ade4b7643907e8d4c2df7ec3a4135b",
        "eventStorePin": "3.123.0",
        "scope": "retained-local-git-object-observation",
    },
}
PREREQUISITES = (
    "platform-startup-story-1-8", "platform-tool-story-1-9", "module-intake",
    "platform-release-and-hosting", "operational-entry-and-scope",
)
RUNTIME_TEXT = (
    "versionAuthority", "logicalBindings", "subscriptions", "localSecurity", "boundary",
)
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
    require(type(actual) is type(expected), f"{label}: incorrect type")
    if isinstance(expected, dict):
        keys(actual, expected, label)
        for key, value in expected.items():
            exact(actual[key], value, f"{label}.{key}")
    elif isinstance(expected, list):
        require(len(actual) == len(expected), f"{label}: required items differ")
        for item, value in zip(actual, expected):
            exact(item, value, label)
    else:
        require(actual == expected, f"{label}: expected {expected!r}")


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
        require(key not in result, "duplicate JSON key")
        result[key] = value
    return result


def reject_constant(value):
    raise PacketError(f"non-standard JSON constant: {value}")


def read_json(directory, filename):
    path = directory / filename
    require(not path.is_symlink(), f"{filename}: packet symlinks are not accepted")
    try:
        require(path.is_file(), f"{filename}: regular packet file required")
        return json.loads(path.read_bytes().decode("utf-8"),
                          object_pairs_hook=unique_object, parse_constant=reject_constant)
    except OSError:
        raise PacketError(f"{filename}: packet unavailable") from None
    except (UnicodeError, json.JSONDecodeError, RecursionError):
        raise PacketError(f"{filename}: invalid UTF-8 JSON") from None


def source_bytes(root, relative):
    """Read only a trusted allowlisted name and keep OS details out of diagnostics."""
    try:
        requested = root / relative
        path = requested.resolve()
        require(path.is_relative_to(root), f"source escapes workspace: {relative}")
        require(path == requested, f"source redirected: {relative}")
        require(path.exists(), f"source unavailable: {relative}")
        require(path.is_file(), f"source not a regular file: {relative}")
        return path.read_bytes()
    except (OSError, RuntimeError):
        raise PacketError(f"source unavailable: {relative}") from None


def reviewed_content(value, expected_digest, label):
    """Bind semantic JSON content without depending on formatting or source fixtures."""
    body = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    require(hashlib.sha256(body).hexdigest() == expected_digest,
            f"{label}: reviewed content changed")


def validate_catalog(body, dependencies):
    """Compare observed selections to the exact bound catalog, without evaluation."""
    try:
        catalog = ET.fromstring(body)
    except ET.ParseError:
        raise PacketError("Builds catalog: invalid XML") from None
    for name, (prop, package) in CATALOG_BINDINGS.items():
        properties = catalog.findall(f"./PropertyGroup/{prop}")
        require(len(properties) == 1, f"dependency {name}: unique catalog property required")
        exact(properties[0].text, dependencies[name]["catalogInputPin"],
              f"dependency {name} observed catalog selection")
        entries = [row for row in catalog.findall("./ItemGroup/PackageVersion")
                   if row.get("Include") == package]
        require(len(entries) == 1, f"dependency {name}: unique catalog package required")
        exact(entries[0].get("Version"), f"$({prop})", f"dependency {name} catalog package binding")


def validate_admission(config):
    """Keep publication observations, local source and owner acceptance separate."""
    published = config["publishedBuildsObservation"]
    keys(published, (*PUBLISHED_BUILDS, "boundary"), "publishedBuildsObservation")
    for key, value in PUBLISHED_BUILDS.items():
        exact(published[key], value, f"publishedBuildsObservation.{key}")
    nonempty(published["boundary"], "publishedBuildsObservation.boundary")
    keys(config["admissionPrerequisites"], PREREQUISITES, "admissionPrerequisites")
    for name, row in config["admissionPrerequisites"].items():
        keys(row, ("owner", "value", "requirement"), f"prerequisite {name}")
        exact(row["value"], None, f"prerequisite {name} unresolved value")
        for key in ("owner", "requirement"):
            nonempty(row[key], f"prerequisite {name}.{key}")
    runtime = config["runtimeConstraints"]
    keys(runtime, ("source", "owner", "adoptionEvidence", "canonicalHosting",
                   "supportingPrimitives", *RUNTIME_TEXT), "runtimeConstraints")
    exact(runtime["source"], SOURCES[-1], "runtimeConstraints.source")
    nonempty(runtime["owner"], "runtimeConstraints.owner")
    exact(runtime["adoptionEvidence"], None, "runtimeConstraints.adoptionEvidence")
    exact(runtime["canonicalHosting"], ["Hexalith.EventStore.Aspire",
          "Hexalith.EventStore.ServiceDefaults", "CommunityToolkit.Aspire.Hosting.Dapr"],
          "runtimeConstraints.canonicalHosting")
    exact(runtime["supportingPrimitives"], ["Commons Aspire", "Commons ServiceDefaults"],
          "runtimeConstraints.supportingPrimitives")
    for key in RUNTIME_TEXT:
        nonempty(runtime[key], f"runtimeConstraints.{key}")


def validate_packet(packet_dir=PACKET, source_root=WORKSPACE):
    """Read the two packet JSON files and only the fixed source allowlist."""
    config = read_json(Path(packet_dir), "configuration.proposed.json")
    plan = read_json(Path(packet_dir), "staging-checks.json")
    keys(config, (
        "schema", "status", "qualification", "productionControl", "installationProposal",
        "acceptedEnvelope", "sourceSnapshots", "sourceInputs", "releaseIdentity",
        "moduleConfiguration", "platformBindings", "secretReferences", "dependencies",
        "projectsReadOnlyOperations", "publishedBuildsObservation", "admissionPrerequisites",
        "runtimeConstraints",
    ), "configuration")
    keys(plan, ("schema", "status", "qualification", "productionControl", "executionScope", "evidencePolicy", "checks"), "staging")
    exact(config["schema"], "story117-platform-qualification-current.configuration.v1", "configuration schema")
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
        keys(row, ("revision", "scope", "state", "dirtyInputs", "artifactProvenance"), f"snapshot {name}")
        exact(row["revision"], SNAPSHOTS[name], f"snapshot {name} canonical revision")
        exact(row["scope"], "local-checkout", f"snapshot {name} scope")
        exact(row["state"], "dirty" if name == "Folders" else "clean", f"snapshot {name} state")
        exact(row["artifactProvenance"], None, f"snapshot {name} artifactProvenance")
        if name == "Folders":
            texts(row["dirtyInputs"], f"snapshot {name} dirtyInputs")
        else:
            exact(row["dirtyInputs"], [], f"snapshot {name} dirtyInputs")
    inputs = config["sourceInputs"]
    keys(inputs, SOURCES, "sourceInputs")
    source_root = Path(source_root).resolve()
    bound_sources = {}
    for relative in SOURCES:
        digest = inputs[relative]
        require(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest), f"source digest invalid: {relative}")
        body = source_bytes(source_root, relative)
        require(hashlib.sha256(body).hexdigest() == digest, f"source changed: {relative}")
        bound_sources[relative] = body

    validate_admission(config)

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
        exact(row["source"], MODULE_SOURCES[name], f"module {name} reviewed source")
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
    keys(secrets, SECRET_MAPPINGS, "secretReferences")
    try:
        sidecar_source = bound_sources["deploy/dapr/production/sidecar-config-bindings.yaml"].decode("utf-8")
    except UnicodeError:
        raise PacketError("sidecar-config-bindings.yaml: invalid UTF-8 source") from None
    source_names = set(re.findall(
        r"(?m)^\s*(?:name|value|dapr\.io/app-token-secret):\s*([a-z0-9-]+)\s*$", sidecar_source))
    for name, row in secrets.items():
        keys(row, ("consumers", "sourceName", "sourceKey", "targetKeys", "environmentSource", "value", "owner", "boundary"), f"secret {name}")
        for key in ("consumers", "targetKeys"):
            texts(row[key], f"secret {name}.{key}")
        for key in ("sourceName", "sourceKey", "owner", "boundary"):
            nonempty(row[key], f"secret {name}.{key}")
        for key, expected in zip(("sourceName", "sourceKey", "consumers", "targetKeys"), SECRET_MAPPINGS[name]):
            exact(row[key], expected, f"secret {name}.{key}")
        require(row["sourceName"] in source_names, f"secret {name}: exact source reference missing")
        for key in ("environmentSource", "value"):
            exact(row[key], None, f"secret {name}.{key}")
    keys(config["dependencies"], DEPENDENCIES, "dependencies")
    for name, (app_id, pin) in DEPENDENCIES.items():
        row = config["dependencies"][name]
        keys(row, ("appId", "catalogInputPin", "qualifiedArtifact", "owner",
                   "catalogProperty", "catalogPackage"), f"dependency {name}")
        exact(row["appId"], app_id, f"dependency {name} appId")
        exact(row["catalogInputPin"], pin, f"dependency {name} catalogInputPin")
        exact(row["qualifiedArtifact"], None, f"dependency {name} qualifiedArtifact")
        nonempty(row["owner"], f"dependency {name} owner")
        prop, package = CATALOG_BINDINGS[name]
        exact(row["catalogProperty"], prop, f"dependency {name} catalogProperty")
        exact(row["catalogPackage"], package, f"dependency {name} catalogPackage")
    validate_catalog(bound_sources["references/Hexalith.Builds/Props/Directory.Packages.props"],
                     config["dependencies"])
    operations = config["projectsReadOnlyOperations"]
    require(isinstance(operations, list) and len(operations) == len(OPERATIONS), "required Projects read families differ")
    for row, (operation, method, path) in zip(operations, OPERATIONS):
        keys(row, ("operation", "method", "path", "mutates"), "Projects operation")
        for key, value in dict(operation=operation, method=method, path=path, mutates=False).items():
            exact(row[key], value, f"Projects operation {operation}.{key}")

    exact(plan["schema"], "story117-platform-qualification-current.staging.v1", "staging schema")
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
    reviewed_content({key: config[key] for key in REQUIREMENT_FIELDS}, REQUIREMENTS_SHA256,
                     "configuration requirements")
    reviewed_content(plan, STAGING_SHA256, "staging safeguards")


def main():
    if len(sys.argv) != 1:
        print("FAIL: this offline diagnostic accepts no arguments or execution/approval mode.", file=sys.stderr)
        return 2
    try:
        validate_packet()
    except PacketError as error:
        print(f"FAIL: packet consistency: {error}; operational qualification remains HOLD.", file=sys.stderr)
        return 1
    except (OSError, ValueError, TypeError, RecursionError):
        print("FAIL: packet consistency: invalid packet or unavailable source; "
              "operational qualification remains HOLD.", file=sys.stderr)
        return 1
    print("PASS: packet consistency only; operational qualification remains HOLD; "
          "staging checks executed: 0; V1Only; T0 unset; no activation authority.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
