"""Check current offline proposal consistency; never execute or accept a release."""

import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


PACKET = Path(__file__).resolve().parent
WORKSPACE = PACKET.parent.parent
PLATFORM_SOURCE = "references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md"
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
    "src/Hexalith.Folders.Server/Authentication/FoldersGatewayHumanBearerHandler.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadAuthenticationExtensions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/ConfigureWorkloadJwtBearerOptions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtWorkloadAssertionIssuer.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationOptions.cs",
    "Directory.Packages.props",
    "Directory.Build.props",
    "references/Hexalith.Projects/Directory.Packages.props",
    "references/Hexalith.Projects/Directory.Build.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/DelegatedProjectFolderDirectory.cs",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/EventStoreGatewayTokenForwardingHandler.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DelegatedOwnerQueryClient.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/VerifiedQueryCaller.cs",
    "src/Hexalith.Folders.Contracts/FolderOwnerReadQuery.cs",
    "src/Hexalith.Folders.Server/Queries/FolderOwnerReadQueryHandler.cs",
    "src/Hexalith.Folders.Server/Queries/FolderOwnerQueryBinding.cs",
    "src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs",
    "src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs",
    "src/Hexalith.Folders.Server/Content/AuthoritativeWorkspaceFileContextSource.cs",
    "src/Hexalith.Folders.Server/Content/WorkspaceContentErasure.cs",
    "src/Hexalith.Folders.Server/Content/WorkspaceContentRetentionEndpoints.cs",
    "references/Hexalith.Platform/.config/dotnet-tools.json",
    "references/Hexalith.Platform/hexalith-platform.json",
    "references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml",
    "references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/1-13/pass-5/release/accepted-identities.json",
    "_bmad-output/implementation-artifacts/story-1-17-first-install-decision-2026-10-18.md",
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
        "Authentication:WorkloadJwtBearer:Authority", "Authentication:WorkloadJwtBearer:Issuer",
        "Authentication:WorkloadJwtBearer:Audience", "Authentication:WorkloadJwtBearer:ValidAudiences",
        "Authentication:WorkloadJwtBearer:AllowedAlgorithms", "Authentication:WorkloadJwtBearer:SigningKey",
        "Authentication:WorkloadJwtBearer:RequireHttpsMetadata", "Authentication:WorkloadJwtBearer:AllowInsecureSymmetricKey",
        "Authentication:Workload:Audience", "Authentication:Workload:AllowedCallers:0",
        "Authentication:HumanAuthorityReceipt:SigningKey",
        "Authentication:HumanAuthorityReceipt:ExpectedWorkload",
        "EventStore:Gateway:BaseAddress", "APP_API_TOKEN",
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
        "Authentication:WorkloadIssuer:EnableResourceBindingRequests",
        "Authentication:HumanAuthorityReceipt:SigningKey",
        "EventStore:IdempotencyAdmission:Enabled", "EventStore:IdempotencyAdmission:DigestKeySource",
        "EventStore:IdempotencyAdmission:ActiveDigestKeyVersion",
        "EventStore:IdempotencyAdmission:DigestKeySecretStoreName",
        "EventStore:IdempotencyAdmission:DigestKeySecretName",
        "EventStore:IdempotencyAdmission:DigestKeySecretGeneration",
    ),
    "folders-server-and-workers-telemetry": ("OTEL_EXPORTER_OTLP_ENDPOINT",),
    "projects-delegated-owner-query": (
        "Authentication:WorkloadIssuer:Workload", "Authentication:BoundStreamRead:PrivateKey",
        "Authentication:QueryAuthority:GatewayWorkloadId",
    ),
}
MODULE_SOURCES = {
    "folders-server": "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
    "folders-workers": "src/Hexalith.Folders.Workers/FoldersTenantEventSubscriptionOptionsValidator.cs",
    "folders-ui": "src/Hexalith.Folders.UI/CompositionRoot.cs",
    "folders-sdk": "src/Hexalith.Folders.Client/FoldersClientOptions.cs",
    "eventstore-inputs": "deploy/dapr/production/sidecar-config-bindings.yaml",
    "folders-server-and-workers-telemetry": "src/Hexalith.Folders.ServiceDefaults/Extensions.cs",
    "projects-delegated-owner-query": "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DelegatedOwnerQueryClient.cs",
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
    "moduleConfiguration", "authenticationRequirements", "catalogCoherenceObservation",
    "effectiveCatalogSelections", "projectsDelegatedReadOperations",
)
REQUIREMENTS_SHA256 = "c2a8562fb7d3706145bf159185fe0ec815a13ffab9333f20af56b53836f63e4a"
STAGING_SHA256 = "8605f0f626c56f1be196bcc8ab9e9e0b85e2874e8faef4a7458c655b54009885"
FIXED_VALUES = {
    ("folders-server", "Folders:ApiRouting:Mode"): "V1Only",
    ("folders-server", "Folders:Authentication:RequireHttpsMetadata"): True,
    ("folders-server", "Authentication:Workload:Audience"): "folders",
    ("folders-server", "Authentication:Workload:AllowedCallers:0"): "eventstore",
    ("folders-server", "Authentication:HumanAuthorityReceipt:ExpectedWorkload"): "eventstore",
    ("folders-server", "EventStore:Gateway:BaseAddress"): "http://eventstore",
    ("folders-server", "Authentication:WorkloadJwtBearer:RequireHttpsMetadata"): True,
    ("folders-server", "Authentication:WorkloadJwtBearer:AllowInsecureSymmetricKey"): False,
    ("folders-workers", "Folders:Workers:PubSubName"): "folders-workers-pubsub",
    ("folders-ui", "Folders:Authentication:Mode"): "oidc",
    ("folders-ui", "Folders:Authentication:RequireHttpsMetadata"): True,
    ("eventstore-inputs", "EventStore:Publisher:TopicOverrides:folders"): "folders.events",
    ("eventstore-inputs", "EventStore:IdempotencyAdmission:Enabled"): True,
    ("eventstore-inputs", "EventStore:IdempotencyAdmission:DigestKeySource"): "DaprSecret",
    ("eventstore-inputs", "Authentication:WorkloadIssuer:EnableResourceBindingRequests"): False,
    ("projects-delegated-owner-query", "Authentication:QueryAuthority:GatewayWorkloadId"): "eventstore",
}
BINDINGS = (
    "release-and-hosting", "profile", "namespaces-and-network", "read-model", "broker",
    "realm-and-workload-auth", "projects-client-delegation", "ui-confidential-client",
    "provider-tenancy-and-egress", "secret-contract", "telemetry", "attempt-controls", "recovery",
)
DEPENDENCIES = {
    "eventstore": ("eventstore", "3.127.0"),
    "tenants": ("tenants", "5.10.1"),
    "memories": ("memories", "2.29.9"),
    "folders-server": ("folders", "1.3.1"),
    "folders-workers": ("folders-workers", "1.3.1"),
    "folders-ui": ("folders-ui", "1.3.1"),
    "projects-server": ("projects", "1.2.0"),
    "frontcomposer": (None, "4.7.0"),
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
    "Folders": "67630d3be75a4f5f9b1a5598809efebca4d82c47",
    "Projects": "94f6ad1f6854df62c8cd15e47d65dc72985f4e79",
    "Platform": "31b9cdbac61e691ab7d76bfbf3758569be542355",
    "EventStore": "245ded36b78b960b57728f845f9a8aae62275908",
    "Builds": "142b06e6d05c94b55ac35e587450c35fcac87590",
    "Tenants": "14133e96d2c5efb13270580f920133079707398b",
    "AI.Tools": "3f194e17174994d308ec84af9ee2b5aa68674d0d",
    "FrontComposer": "80782afc7fc2fd33ee365d4601cb6b5e91d215fa",
    "Memories": "78657969f4d7d6ccd82c99766296b99ee55bd895",
    "Commons": "1663028fb6d9f46a0bf17919edeb21bb99be1587",
    "PolymorphicSerializations": "221ac4592ba908ae7661ba1fa9e2d1fcc23baee0",
    "McpCli": "8962e2f2570459dbea2976f789b468aa5c6353c6",
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
    """Accept only the inspected scalar-property and Include/Version catalog forms."""
    catalog = xml_source(body, "Builds catalog")
    require(catalog.tag == "Project" and not catalog.attrib
            and all(group.tag in ("PropertyGroup", "ItemGroup") and not group.attrib for group in catalog),
            "Builds catalog: unsupported or conditional catalog group")
    for group in catalog:
        if group.tag == "PropertyGroup":
            require(all(not list(node) and set(node.attrib) <= {"Condition"} for node in group),
                    "Builds catalog: unsupported property construct")
        else:
            require(all(node.tag == "PackageVersion" and not list(node)
                        and set(node.attrib) <= {"Include", "Version", "Condition"}
                        and {"Include", "Version"} <= set(node.attrib) for node in group),
                    "Builds catalog: unsupported package construct")
    sentinel = catalog.findall("./PropertyGroup/HexalithVersionsLoaded")
    require(len(sentinel) == 1 and not sentinel[0].attrib and sentinel[0].text == "true",
            "Builds catalog: unconditional HexalithVersionsLoaded=true required")
    for name, (prop, package) in CATALOG_BINDINGS.items():
        properties = catalog.findall(f"./PropertyGroup/{prop}")
        require(len(properties) == 1, f"dependency {name}: unique catalog property required")
        exact(properties[0].attrib, {"Condition": f"'$({prop})' == ''"},
              f"dependency {name} catalog default condition")
        exact(properties[0].text, dependencies[name]["catalogInputPin"],
              f"dependency {name} observed catalog selection")
        entries = [row for row in catalog.findall("./ItemGroup/PackageVersion")
                   if row.get("Include") == package]
        require(len(entries) == 1, f"dependency {name}: unique catalog package required")
        exact(entries[0].get("Version"), f"$({prop})", f"dependency {name} catalog package binding")
        exact(entries[0].attrib, {"Include": package, "Version": f"$({prop})"},
              f"dependency {name} unconditional catalog package binding")
    client = [row for row in catalog.findall("./ItemGroup/PackageVersion")
              if row.get("Include") == "Hexalith.Folders.Client"]
    require(len(client) == 1, "Builds catalog: unique Folders Client binding required")
    exact(client[0].attrib, {"Include": "Hexalith.Folders.Client", "Version": "$(HexalithFoldersVersion)"},
          "Builds catalog Folders Client exact package binding")


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
    exact(runtime["source"], PLATFORM_SOURCE, "runtimeConstraints.source")
    nonempty(runtime["owner"], "runtimeConstraints.owner")
    exact(runtime["adoptionEvidence"], None, "runtimeConstraints.adoptionEvidence")
    exact(runtime["canonicalHosting"], ["Hexalith.EventStore.Aspire",
          "Hexalith.EventStore.ServiceDefaults", "CommunityToolkit.Aspire.Hosting.Dapr"],
          "runtimeConstraints.canonicalHosting")
    exact(runtime["supportingPrimitives"], ["Commons Aspire", "Commons ServiceDefaults"],
          "runtimeConstraints.supportingPrimitives")
    for key in RUNTIME_TEXT:
        nonempty(runtime[key], f"runtimeConstraints.{key}")


PROJECTS_FOLDERS = "references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/"
OWNER_CATALOGS = {
    "Folders": ("Directory.Packages.props", {"HexalithEventStoreVersion": "3.127.0"}),
    "Projects": ("references/Hexalith.Projects/Directory.Packages.props", {}),
}
NEARER_CATALOGS = (
    "references/Hexalith.Projects/src/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.AppHost/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Aspire/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Cli/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Client/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Client/Generation/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Client/Generation/Shared/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Contracts/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Infrastructure/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Mcp/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Server/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.ServiceDefaults/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Testing/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.UI/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects.Workers/Directory.Packages.props",
    "references/Hexalith.Projects/src/Hexalith.Projects/Directory.Packages.props",
    "src/Directory.Packages.props",
    "src/Hexalith.Folders.AppHost/Directory.Packages.props",
    "src/Hexalith.Folders.Aspire/Directory.Packages.props",
    "src/Hexalith.Folders.Cli/Directory.Packages.props",
    "src/Hexalith.Folders.Client/Directory.Packages.props",
    "src/Hexalith.Folders.Client/Generation/Directory.Packages.props",
    "src/Hexalith.Folders.Client/Generation/Shared/Directory.Packages.props",
    "src/Hexalith.Folders.Contracts/Directory.Packages.props",
    "src/Hexalith.Folders.EventStore/Directory.Packages.props",
    "src/Hexalith.Folders.Mcp/Directory.Packages.props",
    "src/Hexalith.Folders.Server/Directory.Packages.props",
    "src/Hexalith.Folders.ServiceDefaults/Directory.Packages.props",
    "src/Hexalith.Folders.Testing/Directory.Packages.props",
    "src/Hexalith.Folders.UI/Directory.Packages.props",
    "src/Hexalith.Folders.Workers/Directory.Packages.props",
    "src/Hexalith.Folders/Directory.Packages.props",
)
BUILD_PROPS = ("Directory.Build.props", "references/Hexalith.Projects/Directory.Build.props")
CATALOG_SOURCE = "references/Hexalith.Builds/Props/Directory.Packages.props"
IMPORTS = [
    {"Project": "$(Hexalith1BuildPackageProps)", "Condition": "Exists('$(Hexalith1BuildPackageProps)') And '$(HexalithVersionsLoaded)' != 'true'"},
    {"Project": "$(Hexalith2BuildPackageProps)", "Condition": "Exists('$(Hexalith2BuildPackageProps)') And '$(HexalithVersionsLoaded)' != 'true'"},
    {"Project": "$(Hexalith3BuildPackageProps)", "Condition": "'$(HexalithVersionsLoaded)' != 'true'"},
]
IMPORT_PATHS = {
    "Hexalith1BuildPackageProps": "$(MSBuildThisFileDirectory)references/Hexalith.Builds/Props/Directory.Packages.props",
    "Hexalith2BuildPackageProps": "$(MSBuildThisFileDirectory)../Hexalith.Builds/Props/Directory.Packages.props",
    "Hexalith3BuildPackageProps": "$(MSBuildThisFileDirectory)../../Hexalith.Builds/Props/Directory.Packages.props",
}
TOOL_MANIFEST = "references/Hexalith.Platform/.config/dotnet-tools.json"
TOOL_RANGE = "references/Hexalith.Platform/hexalith-platform.json"
TOOL_RECEIPT = "references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/1-13/pass-5/release/accepted-identities.json"
TOOL_PACKAGE_HASHES = {
    "Hexalith.Builds.Evidence.Cli": {
        "publishedSignedArchiveSha256": "3659978298499a6bc67335c32c70aef15217f9f23d410f31dcc175aa14b7c708",
        "qualifiedUnsignedArchiveSha256": "3cb7f879305e0da9d0edb6d4b66911b16c290814b62a8b76a47c5390356defaa"
    },
    "Hexalith.Builds.Module.Cli": {
        "publishedSignedArchiveSha256": "637bebd547f45b5f2012c2f43af79b9b01772f1e81af02809babe62f4aa7cf1d",
        "qualifiedUnsignedArchiveSha256": "69d94250acd8790997692bc66884e6f7be35e714487a4dfe04a13138b3459328"
    }
}
PLATFORM_TRACKER = "references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml"
DELEGATED_OPERATIONS = [
    {
        "operation": "RefreshFolderReferenceAsync",
        "queryType": "Hexalith.Folders.Queries.AuthorizeFolderRead.v1",
        "method": "POST",
        "gatewayPath": "api/v1/queries/owner-authority/workload",
        "ownerPath": "/query",
        "ownerDomain": "folders",
        "mutates": False,
        "source": "references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/DelegatedProjectFolderDirectory.cs",
        "transportSource": "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DelegatedOwnerQueryClient.cs",
        "queryContractSource": "src/Hexalith.Folders.Contracts/FolderOwnerReadQuery.cs",
        "ownerHandlerSource": "src/Hexalith.Folders.Server/Queries/FolderOwnerReadQueryHandler.cs",
        "selection": "VerifiedQueryCaller present; otherwise direct SDK refresh. ValidateSetProjectFolderAsync remains direct.",
        "outcomes": "safe-denial -> Denied; Allowed -> Accepted; Stale -> Stale; other/failed/malformed/unknown -> Unavailable; caller cancellation propagates.",
        "hostedQualification": None,
    },
]


def xml_source(body, label):
    try:
        return ET.fromstring(body)
    except ET.ParseError:
        raise PacketError(f"{label}: invalid XML") from None


def validate_effective_inputs(config, sources, source_root):
    """Check the reviewed default import/override forms; never evaluate MSBuild."""
    selections = config["effectiveCatalogSelections"]
    keys(selections, OWNER_CATALOGS, "effectiveCatalogSelections")
    catalog = xml_source(sources[CATALOG_SOURCE], "Builds catalog")
    for relative in NEARER_CATALOGS:
        candidate = source_root / relative
        require(not candidate.exists() and not candidate.is_symlink() and candidate.resolve() == candidate,
                f"nearer consumer catalog substitution refused: {relative}")
    version_properties = {prop for prop, _ in CATALOG_BINDINGS.values()} | {"HexalithVersionsLoaded"}
    for relative in BUILD_PROPS:
        build = xml_source(sources[relative], "consumer build props")
        require(build.tag == "Project" and not build.attrib,
                "consumer build props: unsupported root")
        require(not any(node.tag in version_properties | {"Import", "Choose", "VersionOverride"} for node in build.iter()),
                "consumer build props: catalog override or unsupported indirection refused")
        require(not any(node.tag in ("PackageVersion", "PackageReference")
                        for group in build.iter("ItemGroup") for node in group),
                "consumer build props: package override refused")
    for owner, (relative, overrides) in OWNER_CATALOGS.items():
        row = selections[owner]
        keys(row, ("owningCatalog", "importedCatalog", "scope", "propertyOverrides", "packageOverrides",
                   "foldersClientContractsSelection", "boundary"), f"effective catalog {owner}")
        exact(row["owningCatalog"], relative, f"effective catalog {owner} owningCatalog")
        exact(row["importedCatalog"], CATALOG_SOURCE, f"effective catalog {owner} importedCatalog")
        exact(row["scope"], "inspected-default-Release-package-inputs", f"effective catalog {owner} scope")
        exact(row["propertyOverrides"], overrides, f"effective catalog {owner} propertyOverrides")
        exact(row["packageOverrides"], {}, f"effective catalog {owner} packageOverrides")
        exact(row["foldersClientContractsSelection"], "1.3.1", f"effective catalog {owner} foldersClientContractsSelection")
        nonempty(row["boundary"], f"effective catalog {owner} boundary")
        local = xml_source(sources[relative], f"effective catalog {owner}")
        exact(local.tag, "Project", f"effective catalog {owner} root")
        exact(local.attrib, {}, f"effective catalog {owner} root attributes")
        require(all(not node.attrib for node in local.findall("./PropertyGroup")),
                f"effective catalog {owner}: conditional property group refused")
        exact([node.attrib for node in local.findall("./Import")], IMPORTS,
              f"effective catalog {owner} guarded imports")
        require(all(node.tag in ("PropertyGroup", "Import") for node in local),
                f"effective catalog {owner}: unsupported local catalog construct")
        properties = list(local.findall("./PropertyGroup/*"))
        expected_properties = dict(IMPORT_PATHS, ManagePackageVersionsCentrally="true",
                                   CentralPackageTransitivePinningEnabled="true", **overrides)
        require(len(properties) == len(expected_properties) and all(not node.attrib and len(node) == 0 for node in properties),
                f"effective catalog {owner}: unsupported or duplicate local property")
        exact({node.tag: node.text for node in properties}, expected_properties,
              f"effective catalog {owner} reviewed local properties")
        # Projects' earlier nested fallback is currently absent. If it appears,
        # the same owning props would select another catalog before direct Builds.
        # Probe only this fixed candidate's metadata and never read its contents.
        if owner == "Projects":
            alternate = source_root / "references/Hexalith.Projects/references/Hexalith.Builds/Props/Directory.Packages.props"
            require(not alternate.exists() and not alternate.is_symlink() and alternate.resolve() == alternate,
                    "effective catalog Projects: earlier nested catalog substitution refused")
        observed = {}
        for node in properties:
            if node.tag.startswith("Hexalith") and node.tag.endswith("Version"):
                require(node.tag not in observed and not node.attrib,
                        f"effective catalog {owner}: unconditional unique override required")
                observed[node.tag] = node.text
        exact(observed, overrides, f"effective catalog {owner} observed overrides")
        # Every owning declaration, including import paths, must precede imports.
        first_import = next(index for index, node in enumerate(local) if node.tag == "Import")
        require(all(index < first_import for index, node in enumerate(local)
                    if node.tag == "PropertyGroup" and list(node)),
                f"effective catalog {owner}: owning declaration order changed")
        for key, value in IMPORT_PATHS.items():
            matches = [node for node in properties if node.tag == key]
            require(len(matches) == 1 and not matches[0].attrib and matches[0].text == value,
                    f"effective catalog {owner}: import path changed")
        for package in ("Hexalith.Folders.Client", "Hexalith.Folders.Contracts"):
            rows = [node for node in catalog.findall("./ItemGroup/PackageVersion") if node.get("Include") == package]
            require(len(rows) == 1 and rows[0].get("Version") == "$(HexalithFoldersVersion)"
                    and rows[0].get("Condition") is None,
                    f"effective catalog {owner}: Client/Contracts pair binding changed")
    try:
        documents = {relative: json.loads(sources[relative].decode("utf-8"),
                         object_pairs_hook=unique_object, parse_constant=reject_constant)
                     for relative in (TOOL_MANIFEST, TOOL_RANGE, TOOL_RECEIPT)}
        manifest = documents[TOOL_MANIFEST]["tools"]["hexalith.builds.module.cli"]
        exact(manifest["version"], "4.35.2", "recorded tool pin")
        exact(manifest["rollForward"], False, "recorded tool rollForward")
        exact(manifest["commands"], ["hexalith-module"], "recorded tool command binding")
        exact(documents[TOOL_MANIFEST]["version"], 1, "recorded tool manifest version")
        exact(documents[TOOL_MANIFEST]["isRoot"], True, "recorded tool manifest root")
        exact(documents[TOOL_RANGE]["toolVersionRange"], "[4.35.2, 5.0.0-0)", "recorded tool range")
        receipt = documents[TOOL_RECEIPT]
        for key, value in dict(version="4.35.2", rollForward=False,
                               supportedToolRange="[4.35.2, 5.0.0-0)",
                               buildsSourceCommit="4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
                               buildsReleaseTag="v4.35.2",
                               platformRecordedBuildsGitlink="4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
                               platformToolManifestSha256=hashlib.sha256(sources[TOOL_MANIFEST]).hexdigest(),
                               restoredVersion="4.35.2+4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
                               ciRunId=39736790198, ciConclusion="success",
                               releaseRunId=39741376261, releaseConclusion="success").items():
            exact(receipt[key], value, f"recorded tool acceptance {key}")
        for package, hashes in TOOL_PACKAGE_HASHES.items():
            exact(receipt["packageHashes"][package], hashes, "recorded tool acceptance package hashes")
        fresh = receipt["freshNuGetOnlyAcceptance"]
        for key, value in dict(emptyCliHomeAndPackageCache=True, commands=4, unchanged=True,
                               restoredPackageSha256=TOOL_PACKAGE_HASHES["Hexalith.Builds.Module.Cli"]["publishedSignedArchiveSha256"]).items():
            exact(fresh[key], value, f"recorded tool acceptance fresh restore {key}")
        exact(receipt["platformKitTests"]["class"], "Hexalith.Platform.Composition.Tests.DomainEnrollmentKitTests",
              "recorded tool acceptance kit class")
        exact(receipt["platformKitTests"]["total"], 5, "recorded tool acceptance kit total")
        exact(receipt["platformKitTests"]["failed"], 0, "recorded tool acceptance kit failures")
    except (UnicodeError, json.JSONDecodeError, KeyError, TypeError, RecursionError):
        raise PacketError("recorded tool acceptance: malformed source") from None
    tracker = sources[PLATFORM_TRACKER].decode("utf-8")
    for story in ("1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagno",
                  "1-9-ratify-and-pin-the-first-platform-accepted-tool-version"):
        require(re.findall(r"(?m)^  " + re.escape(story) + r": (\S+)\s*$", tracker) == ["done"],
                "recorded Platform story status changed")
    # These are lexical declaration anchors, backed by reviewed exact source
    # bytes. Comments can contain them; they do not prove C# control flow.
    delegated = DELEGATED_OPERATIONS[0]
    expected_tokens = {
        delegated["source"]: ("VerifiedQueryCaller.Get", "FolderOwnerReadQuery.QueryType", "client().InvokeAsync"),
        delegated["transportSource"]: (delegated["gatewayPath"], "BoundOwnerQueryProof.Issue", "OriginatingAssertion"),
        delegated["queryContractSource"]: ('"' + delegated["queryType"] + '"',),
        delegated["ownerHandlerSource"]: ("AuthorizeCurrentMetadataReadAsync", '"delegated-owner-query"'),
    }
    for relative, tokens in expected_tokens.items():
        require(all(token.encode("utf-8") in sources[relative] for token in tokens),
                f"Projects delegated source declaration changed: {relative}")


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
        "authenticationRequirements", "catalogCoherenceObservation", "effectiveCatalogSelections",
        "projectsDelegatedReadOperations",
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
    keys(snapshots, SNAPSHOTS, "sourceSnapshots")
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
        extra = ("consumingHost", "environmentBindings", "secretBindings") if name == "projects-delegated-owner-query" else ()
        keys(row, ("owner", "source", "values", "boundary", *extra), f"module {name}")
        if extra:
            exact(row["consumingHost"], "projects-server", "Projects delegated consuming host")
            exact(row["environmentBindings"], None, "Projects delegated environment bindings")
            exact(row["secretBindings"], None, "Projects delegated secret bindings")
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
    validate_effective_inputs(config, bound_sources, source_root)
    operations = config["projectsReadOnlyOperations"]
    require(isinstance(operations, list) and len(operations) == len(OPERATIONS), "required Projects read families differ")
    for row, (operation, method, path) in zip(operations, OPERATIONS):
        keys(row, ("operation", "method", "path", "mutates", "transport", "source"), "Projects operation")
        exact(row["transport"], "direct-sdk", f"Projects operation {operation}.transport")
        direct_source = PROJECTS_FOLDERS + ("FoldersProjectFileReferenceDirectory.cs"
            if operation == "GetFolderFileMetadataAsync" else "FoldersProjectFolderDirectory.cs")
        exact(row["source"], direct_source, f"Projects operation {operation}.source")
        for key, value in dict(operation=operation, method=method, path=path, mutates=False).items():
            exact(row[key], value, f"Projects operation {operation}.{key}")

    exact(config["projectsDelegatedReadOperations"], DELEGATED_OPERATIONS, "Projects delegated operation")

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
