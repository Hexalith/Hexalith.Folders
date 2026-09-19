#!/usr/bin/env python3
"""Generate the non-routed PD10 v2 candidate from the historical v1 Spine."""

from __future__ import annotations

import argparse
import copy
import re
from pathlib import Path
from typing import Any

import yaml


HTTP_METHODS = {"delete", "get", "head", "options", "patch", "post", "put", "trace"}
FORBIDDEN_PROTECTED_CATEGORIES = {
    "not_found",
    "cross_tenant_access_denied",
    "audit_access_denied",
}
EVALUATION_ORDER = [
    "authentication",
    "authority_evidence_availability",
    "tenant_access",
    "principal_and_delegation_intersection",
    "folder_acl_allow",
    "family_grant",
    "resource_scope_binding",
    "freshness_revalidation",
    "observation",
]
ACCESS_STATES = [
    "tenant-administrator",
    "tenant-member",
    "delegated-service-agent",
    "tenant-scoped-operator",
    "audit-reviewer",
    "incident-administrator",
    "wrong-tenant",
    "revoked",
    "stale",
    "disabled",
    "unknown",
    "hidden-resource",
    "absent-resource",
    "insufficient-scope",
]
VISIBILITY_VALUES = ["redacted", "metadata_only", "unavailable", "absent", "withheld"]


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--matrix", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def read_matrix(path: Path) -> dict[str, dict[str, Any]]:
    text = path.read_text(encoding="utf-8")
    section = text.split("## Operation Mapping", 1)[1].split("### Scope Dimension Rules", 1)[0]
    operations: dict[str, dict[str, Any]] = {}
    row_pattern = re.compile(
        r"^\| `(?P<operation>[^`]+)` \| (?P<method>[A-Z]+) \| `(?P<path>[^`]+)` "
        r"\| `(?P<family>[^`]+)` \| (?P<applicable>.*?) \| (?P<not_applicable>.*?) \|$"
    )
    for line in section.splitlines():
        match = row_pattern.match(line)
        if match is None:
            continue
        applicable = re.findall(r"`([^`]+)`", match.group("applicable"))
        not_applicable = re.findall(r"`([^`]+)`", match.group("not_applicable"))
        if match.group("not_applicable").strip() == "none":
            not_applicable = []
        operations[match.group("operation")] = {
            "method": match.group("method").lower(),
            "path": match.group("path"),
            "family": match.group("family"),
            "applicable": applicable,
            "not_applicable": not_applicable,
        }
    if len(operations) != 49:
        raise ValueError(f"Expected 49 operation rows in {path}, found {len(operations)}.")
    return operations


def replace_request_schema_versions(node: Any) -> None:
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "requestSchemaVersion" and value == "v1":
                node[key] = "v2"
            elif key == "requestSchemaVersion" and isinstance(value, dict):
                if value.get("const") == "v1":
                    value["const"] = "v2"
                if value.get("enum") == ["v1"]:
                    value["enum"] = ["v2"]
                replace_request_schema_versions(value)
            else:
                replace_request_schema_versions(value)
    elif isinstance(node, list):
        for item in node:
            replace_request_schema_versions(item)


def remove_forbidden_enum_values(node: Any) -> None:
    if isinstance(node, dict):
        enum_values = node.get("enum")
        if isinstance(enum_values, list):
            node["enum"] = [value for value in enum_values if value not in FORBIDDEN_PROTECTED_CATEGORIES]
        for value in node.values():
            remove_forbidden_enum_values(value)
    elif isinstance(node, list):
        for item in node:
            remove_forbidden_enum_values(item)


def ensure_parameter(operation: dict[str, Any], reference: str, *, prepend: bool = False) -> None:
    parameters = operation.setdefault("parameters", [])
    if not any(isinstance(item, dict) and item.get("$ref") == reference for item in parameters):
        if prepend:
            parameters.insert(0, {"$ref": reference})
        else:
            parameters.append({"$ref": reference})


def requirement_for(operation_id: str, current: str) -> str:
    overrides = {
        "ListFolderAclEntries": "fresh-tenant-and-folder-administer-before-acl-observation",
        "GetEffectivePermissions": "fresh-tenant-and-folder-read-self-inspection-with-optional-task-context",
        "ValidateProviderReadiness": "fresh-tenant-folder-create-authority-before-provider-egress",
        "GetTaskStatus": "fresh-tenant-and-folder-read-before-task-lookup-and-folder-binding-proof",
        "GetReadinessDiagnostics": "fresh-tenant-folder-read-and-operator-scope-before-diagnostic-lookup",
        "GetProjectionFreshness": "fresh-tenant-folder-read-and-operator-scope-before-diagnostic-lookup",
    }
    return overrides.get(operation_id, current)


def transform_operation(
    operation: dict[str, Any],
    operation_id: str,
    matrix: dict[str, Any],
) -> None:
    responses = operation.setdefault("responses", {})
    responses.pop("403", None)
    responses["401"] = {"$ref": "#/components/responses/AuthenticationFailure401"}
    responses["404"] = {"$ref": "#/components/responses/SafeDenial404"}
    responses["503"] = {"$ref": "#/components/responses/AuthorityUnavailable503"}

    categories = [
        category
        for category in operation.get("x-hexalith-canonical-error-categories", [])
        if category not in FORBIDDEN_PROTECTED_CATEGORIES
    ]
    for category in ("authentication_failure", "tenant_access_denied", "read_model_unavailable"):
        if category not in categories:
            categories.append(category)
    if operation.get("x-hexalith-idempotency-key", {}).get("required") is True and "concurrency_conflict" not in categories:
        categories.append("concurrency_conflict")
    operation["x-hexalith-canonical-error-categories"] = categories
    operation["x-hexalith-operation-family"] = matrix["family"]

    authorization = operation.setdefault("x-hexalith-authorization", {})
    authorization["requirement"] = requirement_for(operation_id, str(authorization.get("requirement", "")))
    authorization["tenantAuthority"] = "authentication-context-and-eventstore-envelope"
    authorization["evaluationOrder"] = EVALUATION_ORDER
    authorization["scopeDimensions"] = matrix["applicable"]
    authorization["notApplicableScopeDimensions"] = matrix["not_applicable"]
    authorization["derivedScopeDimensions"] = [
        dimension
        for dimension in matrix["applicable"]
        if dimension in {"provider", "repository", "workspace", "task"}
    ]
    authorization["safeDenial"] = "safe-denial-404"
    authorization["authorityUnavailable"] = "authority-unavailable-503"
    authorization["protectedLookupAfterAuthorization"] = True

    if "{folderId}" in matrix["path"]:
        ensure_parameter(operation, "#/components/parameters/FolderId", prepend=True)
    if operation_id == "GetEffectivePermissions":
        ensure_parameter(operation, "#/components/parameters/TaskId")


def collect_detail_keys(node: Any, keys: set[str]) -> None:
    if isinstance(node, dict):
        details = node.get("details")
        if isinstance(details, dict):
            keys.update(str(key) for key in details)
        for value in node.values():
            collect_detail_keys(value, keys)
    elif isinstance(node, list):
        for item in node:
            collect_detail_keys(item, keys)


def collect_error_codes(node: Any, codes: set[str]) -> None:
    if isinstance(node, dict):
        if isinstance(node.get("category"), str) and isinstance(node.get("code"), str):
            codes.add(node["code"])
        for value in node.values():
            collect_error_codes(value, codes)
    elif isinstance(node, list):
        for item in node:
            collect_error_codes(item, codes)


def transform(source: dict[str, Any], matrix: dict[str, dict[str, Any]]) -> dict[str, Any]:
    contract = copy.deepcopy(source)
    contract["info"]["version"] = "v2"
    contract["info"]["summary"] = "Non-routed PD10 v2 authorization Contract Spine candidate."
    contract["info"]["description"] = (
        "Digest-bound PD10 v2 candidate for Hexalith.Folders. Authentication and fresh authority are "
        "established before every protected observation. This document is generated for A6b review and is "
        "not selected by the supported production profile."
    )
    contract["servers"] = [{"url": "/api/v2", "description": "Candidate-only surface; not production-routed before A6b, Section 9, and A8."}]

    generated_paths: dict[str, Any] = {}
    observed: set[str] = set()
    for _historical_path, path_item in source["paths"].items():
        for method, operation in path_item.items():
            if method not in HTTP_METHODS or not isinstance(operation, dict) or "operationId" not in operation:
                continue
            operation_id = operation["operationId"]
            if operation_id not in matrix:
                raise ValueError(f"Operation {operation_id} is missing from the matrix.")
            expected = matrix[operation_id]
            if method != expected["method"]:
                raise ValueError(f"Method mismatch for {operation_id}: {method} != {expected['method']}.")
            candidate_operation = copy.deepcopy(operation)
            transform_operation(candidate_operation, operation_id, expected)
            generated_paths.setdefault(expected["path"], {})[method] = candidate_operation
            observed.add(operation_id)
    missing = sorted(set(matrix) - observed)
    if missing:
        raise ValueError(f"Matrix operations missing from historical Spine: {', '.join(missing)}")
    contract["paths"] = generated_paths

    components = contract["components"]
    responses = components["responses"]
    responses.pop("SafeAuthorizationDenial403", None)
    responses["AuthenticationFailure401"] = copy.deepcopy(responses["SafeAuthorizationDenial401"])
    responses["SafeDenial404"] = copy.deepcopy(responses["SafeAuthorizationDenial404"])
    responses["AuthorityUnavailable503"] = {
        "description": "Authority evidence is stale, unavailable, conflicting, or incomplete. No protected lookup has occurred.",
        "content": {
            "application/problem+json": {
                "schema": {"$ref": "#/components/schemas/ProblemDetails"},
                "examples": {"synthetic": {"$ref": "#/components/examples/AuthorityUnavailable503"}},
            }
        },
    }
    components["examples"].pop("SafeDenial403Forbidden", None)
    components["examples"]["AuthorityUnavailable503"] = {
        "summary": "Canonical non-disclosing authority-unavailable response emitted before protected lookup.",
        "value": {
            "type": "about:blank",
            "title": "Authority temporarily unavailable",
            "status": 503,
            "category": "read_model_unavailable",
            "code": "projection_unavailable",
            "message": "Authorization evidence is temporarily unavailable.",
            "correlationId": "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
            "retryable": True,
            "clientAction": "retry",
            "details": {"visibility": "redacted"},
        },
    }

    categories = components["schemas"]["CanonicalErrorCategory"]["enum"]
    if "concurrency_conflict" not in categories:
        categories.insert(categories.index("idempotency_conflict"), "concurrency_conflict")

    mcp_failure_kind_schema = components["schemas"]["McpFailureKind"]["enum"]
    if "concurrency_conflict" not in mcp_failure_kind_schema:
        mcp_failure_kind_schema.insert(mcp_failure_kind_schema.index("idempotency_conflict"), "concurrency_conflict")

    cli_exit_codes = components["schemas"]["CliExitCode"]["enum"]
    if "77" not in cli_exit_codes:
        cli_exit_codes.append("77")

    details_schema = components["schemas"]["ProblemDetails"]["properties"]["details"]
    detail_keys: set[str] = {"visibility"}
    collect_detail_keys(components.get("examples", {}), detail_keys)
    details_schema["required"] = ["visibility"]
    details_schema["propertyNames"] = {"enum": sorted(detail_keys)}
    details_schema["description"] = (
        "Closed metadata-only details. Every error requires visibility; keys outside the generated vocabulary are forbidden."
    )

    release_reason = components["schemas"].get("ReleaseWorkspaceLockRequest", {}).get("properties", {}).get("releaseReasonCode")
    if isinstance(release_reason, dict):
        release_reason["enum"] = ["caller_completed"]

    remove_forbidden_enum_values(contract)
    replace_request_schema_versions(contract)

    error_codes: set[str] = {
        "authentication_required",
        "resource_unavailable",
        "projection_unavailable",
        "concurrency_conflict",
    }
    collect_error_codes(components.get("examples", {}), error_codes)
    post_sdk_mcp_failure_kinds = sorted(
        {
            category
            for path_item in contract["paths"].values()
            for operation in path_item.values()
            if isinstance(operation, dict)
            for category in operation.get("x-hexalith-canonical-error-categories", [])
        }
    )
    contract["x-hexalith-pd10-candidate"] = {
        "version": "2.0.0",
        "lifecycle": "candidate-awaiting-a6b",
        "productionRouted": False,
        "operationCount": 49,
        "accessStates": ACCESS_STATES,
        "protectedOperationFamilies": sorted({entry["family"] for entry in matrix.values()} | {"incident-evidence"}),
        "evaluationOrder": EVALUATION_ORDER,
    }
    contract["x-hexalith-closed-error-vocabulary"] = {
        "categoryRef": "#/components/schemas/CanonicalErrorCategory",
        "codes": sorted(error_codes),
        "clientActions": components["schemas"]["ProblemDetails"]["properties"]["clientAction"]["enum"],
        "visibility": VISIBILITY_VALUES,
        "cliExitCodes": [int(value) for value in cli_exit_codes],
        "mcpFailureKinds": ["usage_error", "credential_missing", *post_sdk_mcp_failure_kinds],
    }
    return contract


def main() -> int:
    arguments = parse_arguments()
    source = yaml.safe_load(arguments.source.read_text(encoding="utf-8"))
    matrix = read_matrix(arguments.matrix)
    candidate = transform(source, matrix)
    serialized = yaml.safe_dump(candidate, sort_keys=False, allow_unicode=False, width=160)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        "# Generated by scripts/generate-pd10-v2-contract.py. Do not edit by hand.\n" + serialized,
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
