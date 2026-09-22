#!/usr/bin/env python3
"""Generate the non-routed PD10 v2 candidate from the historical v1 Spine."""

from __future__ import annotations

import argparse
import copy
import json
import re
from pathlib import Path
from typing import Any

import yaml
from jsonschema import Draft202012Validator


HTTP_METHODS = {"delete", "get", "head", "options", "patch", "post", "put", "trace"}
FORBIDDEN_PROTECTED_CATEGORIES = {
    "not_found",
    "cross_tenant_access_denied",
    "audit_access_denied",
    "folder_acl_denied",
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
VISIBILITY_VALUES = ["redacted", "metadata_only", "withheld", "unavailable", "absent"]
CLIENT_ACTION_VALUES = [
    "retry",
    "revise_request",
    "check_credentials",
    "wait_for_reconciliation",
    "contact_operator",
    "no_action",
    "refresh_state_then_submit_with_new_key",
    "do_not_retry",
    "restart_query",
]

EXACT_AUTHORIZATION_PROBLEMS = {
    "AuthenticationFailureProblem": {
        "type": "about:blank",
        "title": "Authentication required",
        "status": 401,
        "category": "authentication_failure",
        "code": "authentication_required",
        "message": "Authentication is required.",
        "retryable": False,
        "clientAction": "check_credentials",
        "visibility": "redacted",
    },
    "SafeDenialProblem": {
        "type": "about:blank",
        "title": "Resource not available",
        "status": 404,
        "category": "tenant_access_denied",
        "code": "resource_unavailable",
        "message": "The requested resource is unavailable.",
        "retryable": False,
        "clientAction": "no_action",
        "visibility": "redacted",
    },
    "AuthorityUnavailableProblem": {
        "type": "about:blank",
        "title": "Authorization evidence unavailable",
        "status": 503,
        "category": "read_model_unavailable",
        "code": "projection_unavailable",
        "message": "Authorization evidence is temporarily unavailable.",
        "retryable": True,
        "clientAction": "retry",
        "visibility": "redacted",
    },
}

AUTHORITY_UNAVAILABLE_SCHEMA_REF = "#/components/schemas/AuthorityUnavailableProblem"
OPERATION_SPECIFIC_UNAVAILABLE_SCHEMA_REF = "#/components/schemas/OperationSpecificUnavailableProblem"
AUTHORITY_AWARE_FILE_UNAVAILABLE_SCHEMA_REFS = {
    "#/components/schemas/FileMutationUnavailableProblem",
    "#/components/schemas/FileContextUnavailableProblem",
}
DIRECT_RUNTIME_PROBLEMS = [
    {
        "status": values["status"],
        "category": values["category"],
        "code": values["code"],
        "retryable": values["retryable"],
        "clientAction": values["clientAction"],
        "detailKeys": ["visibility"],
    }
    for values in EXACT_AUTHORIZATION_PROBLEMS.values()
] + [
    {
        "status": 400,
        "category": "validation_error",
        "code": code,
        "retryable": False,
        "clientAction": "revise_request",
        "detailKeys": ["visibility"],
    }
    for code in (
        "acl_entry_id_mismatch",
        "cursor_tampered",
        "idempotency_key_not_allowed",
        "invalid_pagination",
        "unsupported_read_consistency",
        "unsupported_request_schema_version",
        "validation_error",
    )
] + [
    {
        "status": 413,
        "category": "input_limit_exceeded",
        "code": "c4_input_limit_exceeded",
        "retryable": False,
        "clientAction": "revise_request",
        "detailKeys": ["visibility"],
    },
    {
        "status": 503,
        "category": "read_model_unavailable",
        "code": "evidence_unavailable",
        "retryable": True,
        "clientAction": "retry",
        "detailKeys": ["visibility"],
    },
    {
        "status": 503,
        "category": "idempotency_admission_unavailable",
        "code": "idempotency_admission_unavailable",
        "retryable": True,
        "clientAction": "retry",
        "detailKeys": ["visibility"],
    },
]
RUNTIME_DETAIL_KEYS = {
    "evidenceSource",
    "finalState",
    "reasonCategory",
    "retryReasonCode",
    "taskId",
    "todoRef",
    "visibility",
}
REQUIRED_BODY_OPERATIONS = {
    "CreateFolder", "ArchiveFolder", "UpdateFolderAclEntry", "ConfigureProviderBinding",
    "ValidateProviderReadiness", "CreateRepositoryBackedFolder", "BindRepository",
    "ConfigureBranchRefPolicy", "PrepareWorkspace", "LockWorkspace", "ReleaseWorkspaceLock",
    "AddFile", "ChangeFile", "RemoveFile", "GetFolderFileMetadata", "SearchFolderFiles",
    "SearchFolderIndexedFiles", "GlobFolderFiles", "ReadFileRange", "CommitWorkspace",
}
GATEWAY_MUTATION_OPERATIONS = {
    "CreateFolder", "ArchiveFolder", "UpdateFolderAclEntry", "ConfigureProviderBinding",
    "CreateRepositoryBackedFolder", "BindRepository", "ConfigureBranchRefPolicy",
    "PrepareWorkspace", "LockWorkspace", "ReleaseWorkspaceLock", "AddFile", "ChangeFile",
    "RemoveFile", "CommitWorkspace",
}
CONTEXT_QUERY_OPERATIONS = {
    "ListFolderFiles", "GetFolderFileMetadata", "SearchFolderFiles", "SearchFolderIndexedFiles",
    "GlobFolderFiles", "ReadFileRange",
}


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
        operation_id = match.group("operation")
        if operation_id in operations:
            raise ValueError(f"Duplicate operation row {operation_id!r} in {path}.")
        operations[operation_id] = {
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
    components: dict[str, Any],
) -> None:
    responses = operation.setdefault("responses", {})
    existing_unavailable = copy.deepcopy(responses.get("503"))
    runtime_problems = operation_runtime_problems(operation_id, operation)
    responses.pop("403", None)
    responses["401"] = {"$ref": "#/components/responses/AuthenticationFailure401"}
    responses["404"] = {"$ref": "#/components/responses/SafeDenial404"}
    responses["503"] = response_with_authority_unavailable(
        operation_id,
        existing_unavailable,
        components,
        [problem for problem in runtime_problems if problem["status"] == 503],
    )
    for status in sorted({problem["status"] for problem in runtime_problems if problem["status"] != 503}):
        ensure_runtime_problem_response(
            responses,
            status,
            [problem for problem in runtime_problems if problem["status"] == status],
            components,
        )

    categories = [
        category
        for category in operation.get("x-hexalith-canonical-error-categories", [])
        if category not in FORBIDDEN_PROTECTED_CATEGORIES
    ]
    for category in ("authentication_failure", "tenant_access_denied", "read_model_unavailable"):
        if category not in categories:
            categories.append(category)
    if operation.get("x-hexalith-idempotency-key", {}).get("required") is True:
        for category in ("concurrency_conflict", "idempotency_admission_unavailable"):
            if category not in categories:
                categories.append(category)
    operation["x-hexalith-canonical-error-categories"] = categories
    operation["x-hexalith-operation-family"] = matrix["family"]

    authorization = operation.setdefault("x-hexalith-authorization", {})
    authorization["candidateVersion"] = "2.0.0"
    authorization["operationFamily"] = matrix["family"]
    authorization["requirement"] = requirement_for(operation_id, str(authorization.get("requirement", "")))
    authorization["tenantAuthority"] = "authentication-context-and-eventstore-envelope"
    authorization["evaluationOrder"] = EVALUATION_ORDER
    authorization["scopeDimensions"] = matrix["applicable"]
    authorization["notApplicableScopeDimensions"] = matrix["not_applicable"]
    authorization["requiredScopes"] = matrix["applicable"]
    authorization["notApplicableScopes"] = matrix["not_applicable"]
    authorization["derivedScopeDimensions"] = [
        dimension
        for dimension in matrix["applicable"]
        if dimension in {"provider", "repository", "workspace", "task"}
    ]
    authorization["safeDenial"] = "safe-denial-404"
    authorization["authorityUnavailable"] = "authority-unavailable-503"
    authorization["freshNegativeOutcome"] = "safe-denial-404"
    authorization["unusableAuthorityOutcome"] = "authority-unavailable-503"
    authorization["protectedLookupAfterAuthorization"] = True
    authorization["taskBinding"] = (
        "task.folderId == route.folderId" if operation_id == "GetTaskStatus" else "not-applicable"
    )

    if "{folderId}" in matrix["path"]:
        ensure_parameter(operation, "#/components/parameters/FolderId", prepend=True)
    if operation_id == "GetEffectivePermissions":
        ensure_parameter(operation, "#/components/parameters/TaskId")

    constrain_freshness(operation, components)
    operation["x-hexalith-runtime-problem-inventory"] = [
        inventory_entry(problem) for problem in runtime_problems
    ]


def runtime_problem(
    status: int,
    category: str,
    code: str,
    retryable: bool,
    client_action: str,
    *,
    detail_values: dict[str, str] | None = None,
) -> dict[str, Any]:
    details = {"visibility": "metadata_only"}
    if detail_values:
        details.update(detail_values)
    return {
        "type": "about:blank",
        "title": "Candidate request or downstream outcome",
        "status": status,
        "category": category,
        "code": code,
        "message": "The request could not be completed.",
        "correlationId": "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
        "retryable": retryable,
        "clientAction": client_action,
        "details": details,
    }


def operation_runtime_problems(operation_id: str, operation: dict[str, Any]) -> list[dict[str, Any]]:
    problems = [
        runtime_problem(400, "validation_error", "validation_error", False, "revise_request"),
        runtime_problem(503, "read_model_unavailable", "evidence_unavailable", True, "retry"),
    ]
    idempotency_required = operation.get("x-hexalith-idempotency-key", {}).get("required") is True
    if not idempotency_required:
        problems.append(runtime_problem(
            400, "validation_error", "idempotency_key_not_allowed", False, "revise_request"))
    else:
        problems.append(runtime_problem(
            503,
            "idempotency_admission_unavailable",
            "idempotency_admission_unavailable",
            True,
            "retry",
        ))
    if operation.get("x-hexalith-read-consistency", {}).get("class"):
        problems.append(runtime_problem(
            400, "validation_error", "unsupported_read_consistency", False, "revise_request"))
    if operation_id in REQUIRED_BODY_OPERATIONS:
        if operation_id in {
            "CreateFolder", "ArchiveFolder", "UpdateFolderAclEntry", "ConfigureProviderBinding",
            "CreateRepositoryBackedFolder", "BindRepository", "ConfigureBranchRefPolicy",
            "PrepareWorkspace", "LockWorkspace", "ReleaseWorkspaceLock", "AddFile", "ChangeFile",
            "RemoveFile", "GetFolderFileMetadata", "SearchFolderFiles", "SearchFolderIndexedFiles",
            "GlobFolderFiles", "ReadFileRange", "CommitWorkspace",
        }:
            problems.append(runtime_problem(
                400, "validation_error", "unsupported_request_schema_version", False, "revise_request"))
        problems.append(runtime_problem(
            413, "input_limit_exceeded", "c4_input_limit_exceeded", False, "revise_request"))
    if operation_id in GATEWAY_MUTATION_OPERATIONS:
        problems.extend([
            runtime_problem(429, "provider_rate_limited", "provider_rate_limited", True, "retry"),
            runtime_problem(503, "read_model_unavailable", "evidence_unavailable", True, "retry"),
        ])
    if operation_id == "ArchiveFolder":
        problems.append(runtime_problem(
            400, "validation_error", "unsupported_archive_reason_code", False, "revise_request"))
    if operation_id == "UpdateFolderAclEntry":
        problems.append(runtime_problem(
            400, "validation_error", "acl_entry_id_mismatch", False, "revise_request"))
    if operation_id == "GetFolderLifecycleStatus":
        problems.extend([
            runtime_problem(503, "internal_error", "archive_state_unsupported", False, "no_action"),
            runtime_problem(503, "internal_error", "read_model_unavailable", False, "no_action"),
        ])
    if operation_id in {"ListAuditTrail", "ListOperationTimeline", "ListFolderAclEntries"}:
        problems.extend([
            runtime_problem(400, "validation_error", "cursor_tampered", False, "revise_request"),
            runtime_problem(400, "validation_error", "invalid_pagination", False, "revise_request"),
            runtime_problem(
                400,
                "validation_error",
                "filter_not_yet_supported",
                False,
                "revise_request",
                detail_values={"todoRef": "C4"},
            ),
        ])
    if operation_id in CONTEXT_QUERY_OPERATIONS:
        problems.extend([
            runtime_problem(408, "query_timeout", "query_timeout", True, "retry"),
            runtime_problem(413, "response_limit_exceeded", "response_limit_exceeded", False, "revise_request"),
            runtime_problem(422, "input_limit_exceeded", "input_limit_exceeded", False, "revise_request"),
        ])
    return deduplicate_problems(problems)


def deduplicate_problems(problems: list[dict[str, Any]]) -> list[dict[str, Any]]:
    distinct: dict[tuple[Any, ...], dict[str, Any]] = {}
    for problem in problems:
        entry = inventory_entry(problem)
        key = (
            entry["status"], entry["category"], entry["code"], entry["retryable"],
            entry["clientAction"], tuple(entry["detailKeys"]),
        )
        distinct[key] = problem
    return [distinct[key] for key in sorted(distinct, key=lambda item: tuple(str(part) for part in item))]


def inventory_entry(problem: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": problem["status"],
        "category": problem["category"],
        "code": problem["code"],
        "retryable": problem["retryable"],
        "clientAction": problem["clientAction"],
        "detailKeys": sorted(problem["details"]),
    }


def ensure_runtime_problem_response(
    responses: dict[str, Any],
    status: int,
    problems: list[dict[str, Any]],
    components: dict[str, Any],
) -> None:
    status_key = str(status)
    existing = copy.deepcopy(responses.get(status_key, {}))
    if isinstance(existing, dict) and isinstance(existing.get("$ref"), str):
        reference = existing["$ref"]
        if reference.startswith("#/components/responses/"):
            existing = copy.deepcopy(components["responses"][reference.rsplit("/", 1)[-1]])
    response = existing if isinstance(existing, dict) else {}
    response["description"] = response.get(
        "description", "Candidate runtime problem response derived from reachable producers."
    )
    media_type = response.setdefault("content", {}).setdefault("application/problem+json", {})
    media_type["schema"] = {"$ref": "#/components/schemas/ProblemDetails"}
    examples = media_type.setdefault("examples", {})
    for index, problem in enumerate(problems, start=1):
        examples[f"candidateRuntime{status}_{index}"] = {"value": copy.deepcopy(problem)}
    responses[status_key] = response


def constrain_freshness(operation: dict[str, Any], components: dict[str, Any]) -> None:
    accepted = operation.get("x-hexalith-read-consistency", {}).get("class")
    if not isinstance(accepted, str):
        return
    parameters = operation.get("parameters", [])
    for index, parameter in enumerate(parameters):
        if not isinstance(parameter, dict) or parameter.get("$ref") != "#/components/parameters/Freshness":
            continue
        exact_parameter = copy.deepcopy(components["parameters"]["Freshness"])
        exact_parameter["schema"] = {
            "$ref": "#/components/schemas/ReadConsistencyClass",
            "enum": [accepted],
        }
        exact_parameter["x-hexalith-accepted-values"] = [accepted]
        parameters[index] = exact_parameter

    for status, response in list(operation.get("responses", {}).items()):
        if not str(status).startswith("2") or not isinstance(response, dict):
            continue
        if isinstance(response.get("$ref"), str):
            reference = response["$ref"]
            if reference.startswith("#/components/responses/"):
                response = copy.deepcopy(components["responses"][reference.rsplit("/", 1)[-1]])
                operation["responses"][status] = response
        response.setdefault("headers", {})["X-Hexalith-Freshness"] = {
            "description": "The operation's declared read-consistency class.",
            "schema": {
                "$ref": "#/components/schemas/ReadConsistencyClass",
                "enum": [accepted],
            },
        }


def response_with_authority_unavailable(
    operation_id: str,
    existing: dict[str, Any] | None,
    components: dict[str, Any],
    runtime_problems: list[dict[str, Any]],
) -> dict[str, Any]:
    if existing is None and not runtime_problems:
        return {"$ref": "#/components/responses/ProtectedOperationUnavailable503"}

    response = copy.deepcopy(existing) if existing is not None else {
        "description": "Candidate runtime service-unavailable response.",
        "content": {
            "application/problem+json": {
                "schema": {"$ref": "#/components/schemas/ProblemDetails"},
                "examples": {},
            }
        },
    }
    reference = response.get("$ref")
    if isinstance(reference, str) and reference.startswith("#/components/responses/"):
        response_name = reference.rsplit("/", 1)[-1]
        response = copy.deepcopy(components["responses"][response_name])

    media_type = response.get("content", {}).get("application/problem+json")
    if not isinstance(media_type, dict) or not isinstance(media_type.get("schema"), dict):
        raise ValueError("Operation-specific 503 response must declare an application/problem+json schema.")

    examples = media_type.get("examples", {})
    if not examples and reference == "#/components/responses/ProviderUnavailable":
        examples = {"providerUnavailable": {"$ref": "#/components/examples/ProviderUnavailableProblem"}}
        media_type["examples"] = copy.deepcopy(examples)

    for index, problem in enumerate(runtime_problems, start=1):
        examples[f"candidateRuntime503_{index}"] = {"value": copy.deepcopy(problem)}
    media_type["examples"] = examples

    exact_legacy_branches: list[dict[str, Any]] = []
    exact_values: list[dict[str, Any]] = []
    for example_name, example in examples.items():
        value = resolve_example_value(example, components)
        normalized = copy.deepcopy(value)
        normalize_problem_examples(normalized)
        if not isinstance(normalized, dict) or not isinstance(normalized.get("category"), str):
            raise ValueError(f"503 example {operation_id}.{example_name} is not a problem envelope.")
        exact_values.append(normalized)
        exact_legacy_branches.append(exact_example_problem_schema(normalized))

    authority_values = {
        **{
            key: value
            for key, value in EXACT_AUTHORIZATION_PROBLEMS["AuthorityUnavailableProblem"].items()
            if key != "visibility"
        },
        "correlationId": "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
        "details": {"visibility": EXACT_AUTHORIZATION_PROBLEMS["AuthorityUnavailableProblem"]["visibility"]},
    }
    exact_values.append(authority_values)

    schema_name = f"{operation_id}UnavailableProblem"
    components["schemas"][schema_name] = {
        "description": "Exact operation-specific historical outcomes plus the exact authority-unavailable branch.",
        **operation_unavailable_wrapper_shape(exact_values),
        "oneOf": [*exact_legacy_branches, {"$ref": AUTHORITY_UNAVAILABLE_SCHEMA_REF}],
    }
    media_type["schema"] = {"$ref": f"#/components/schemas/{schema_name}"}

    response["description"] = (
        f"{response.get('description', 'Operation-specific service-unavailable response')} "
        "The exact authority-unavailable branch is emitted only before protected observation."
    )
    return response


def operation_unavailable_wrapper_shape(values: list[dict[str, Any]]) -> dict[str, Any]:
    property_names = [
        "type", "title", "status", "category", "code", "message", "correlationId",
        "retryable", "clientAction", "details",
    ]
    properties: dict[str, Any] = {}
    for property_name in property_names:
        if property_name == "correlationId":
            properties[property_name] = {"$ref": "#/components/schemas/OpaqueIdentifier"}
        elif property_name == "details":
            detail_values = [item[property_name] for item in values]
            detail_keys = sorted({key for details in detail_values for key in details})
            properties[property_name] = {
                "type": "object",
                "additionalProperties": False,
                "required": ["visibility"],
                "properties": {
                    key: enum_schema([details[key] for details in detail_values if key in details])
                    for key in detail_keys
                },
            }
        else:
            properties[property_name] = enum_schema([item[property_name] for item in values])
    return {
        "type": "object",
        "additionalProperties": False,
        "required": property_names,
        "properties": properties,
    }


def enum_schema(values: list[Any]) -> dict[str, Any]:
    distinct = list(dict.fromkeys(values))
    schema = literal_schema(distinct[0])
    schema["enum"] = distinct
    return schema


def resolve_example_value(example: Any, components: dict[str, Any]) -> Any:
    if isinstance(example, dict) and isinstance(example.get("$ref"), str):
        prefix = "#/components/examples/"
        reference = example["$ref"]
        if not reference.startswith(prefix):
            raise ValueError(f"Unsupported example reference {reference!r}.")
        example = components["examples"][reference.removeprefix(prefix)]
    if isinstance(example, dict) and "value" in example:
        return example["value"]
    raise ValueError("Problem example must provide a concrete value.")


def exact_example_problem_schema(value: dict[str, Any]) -> dict[str, Any]:
    properties: dict[str, Any] = {}
    for key, item in value.items():
        if key == "correlationId":
            properties[key] = {"$ref": "#/components/schemas/OpaqueIdentifier"}
        elif key == "details" and isinstance(item, dict):
            properties[key] = {
                "type": "object",
                "additionalProperties": False,
                "required": list(item),
                "properties": {name: literal_schema(detail) for name, detail in item.items()},
            }
        else:
            properties[key] = literal_schema(item)
    return {
        "type": "object",
        "additionalProperties": False,
        "required": list(value),
        "properties": properties,
    }


def literal_schema(value: Any) -> dict[str, Any]:
    if isinstance(value, bool):
        value_type = "boolean"
    elif isinstance(value, int):
        value_type = "integer"
    elif isinstance(value, str):
        value_type = "string"
    else:
        raise ValueError(f"Unsupported exact problem literal type: {type(value).__name__}.")
    return {"type": value_type, "enum": [value]}


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


def normalize_problem_examples(node: Any) -> None:
    if isinstance(node, dict):
        if isinstance(node.get("category"), str) and isinstance(node.get("code"), str):
            category = node["category"]
            code = node["code"]
            node.pop("taskId", None)
            node.pop("retryAfterSeconds", None)
            details = node.get("details")
            if isinstance(details, dict):
                for removed_key in ("retryReasonCode", "reasonCategory", "evidenceSource", "taskId"):
                    details.pop(removed_key, None)
                for key, value in list(details.items()):
                    if key == "visibility" or isinstance(value, str):
                        continue
                    if isinstance(value, (dict, list)):
                        details[key] = json.dumps(value, sort_keys=True, separators=(",", ":"))
                    elif isinstance(value, bool):
                        details[key] = "true" if value else "false"
                    elif value is None:
                        details[key] = "null"
                    else:
                        details[key] = str(value)
            if category == "lock_conflict":
                node["code"] = "workspace_locked"
                node["retryable"] = True
                if isinstance(details, dict):
                    details["lockStatus"] = "active"
            elif category in {
                "projection_stale", "projection_unavailable", "provider_unavailable",
                "file_policy_unavailable", "read_model_unavailable", "lock_expired",
            }:
                node["retryable"] = True
                if category == "read_model_unavailable":
                    node["code"] = "projection_unavailable"
            node["clientAction"] = client_action_for(category, code, node.get("clientAction"))
        for value in node.values():
            normalize_problem_examples(value)
    elif isinstance(node, list):
        for item in node:
            normalize_problem_examples(item)


def client_action_for(category: str, code: str, existing: Any) -> Any:
    if category == "authentication_failure":
        return "check_credentials"
    if category == "validation_error" and code == "tampered_cursor_or_changed_filter":
        return "restart_query"
    if category in {
        "validation_error", "duplicate_binding", "idempotency_conflict", "repository_conflict",
        "input_limit_exceeded", "response_limit_exceeded", "range_unsatisfiable",
        "state_transition_invalid", "workspace_preparation_failed", "query_timeout",
    }:
        return "revise_request"
    if category == "idempotency_key_expired":
        return "refresh_state_then_submit_with_new_key"
    if category in {
        "authorization_revocation_detected", "dirty_workspace", "commit_failed",
        "provider_readiness_failed",
    }:
        return "contact_operator"
    if category in {"unknown_provider_outcome", "reconciliation_required"}:
        return "wait_for_reconciliation"
    if category == "provider_failure_known":
        return "do_not_retry"
    if category in {
        "idempotency_admission_unavailable", "lock_conflict", "lock_expired", "projection_stale",
        "projection_unavailable", "provider_unavailable", "file_policy_unavailable",
        "read_model_unavailable", "provider_rate_limited",
    }:
        return "retry"
    return existing


def operation_problem_inventory(
    contract: dict[str, Any],
    operation: dict[str, Any],
) -> list[dict[str, Any]]:
    values: list[dict[str, Any]] = []
    for response in operation.get("responses", {}).values():
        if not isinstance(response, dict):
            continue
        if isinstance(response.get("$ref"), str):
            response = resolve_local_reference(contract, response["$ref"])
        media_type = response.get("content", {}).get("application/problem+json")
        if not isinstance(media_type, dict):
            continue
        for example in media_type.get("examples", {}).values():
            if isinstance(example, dict) and isinstance(example.get("$ref"), str):
                example = resolve_local_reference(contract, example["$ref"])
            if isinstance(example, dict) and isinstance(example.get("value"), dict):
                values.append(copy.deepcopy(example["value"]))

    for exact_name in ("AuthenticationFailureProblem", "SafeDenialProblem", "AuthorityUnavailableProblem"):
        exact = EXACT_AUTHORIZATION_PROBLEMS[exact_name]
        values.append({
            **{key: value for key, value in exact.items() if key != "visibility"},
            "correlationId": "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
            "details": {"visibility": exact["visibility"]},
        })
    normalize_problem_examples(values)
    return build_runtime_problem_inventory(values, include_direct=False)


def exact_problem_schema(values: dict[str, Any]) -> dict[str, Any]:
    exact_properties = {
        key: literal_schema(value)
        for key, value in values.items()
        if key != "visibility"
    }
    exact_properties["details"] = {
        "type": "object",
        "additionalProperties": False,
        "required": ["visibility"],
        "properties": {"visibility": literal_schema(values["visibility"])},
    }
    exact_properties["correlationId"] = {"$ref": "#/components/schemas/OpaqueIdentifier"}
    return {
        "allOf": [
            {"$ref": "#/components/schemas/ProblemDetails"},
            {
                "type": "object",
                "additionalProperties": False,
                "required": [
                    "type", "title", "status", "category", "code", "message",
                    "correlationId", "retryable", "clientAction", "details",
                ],
                "not": {
                    "anyOf": [
                        {"required": ["detail"]},
                        {"required": ["instance"]},
                    ]
                },
                "properties": exact_properties,
            },
        ],
        "x-hexalith-exact-envelope": exact_properties,
    }


def exact_problem_response(schema_name: str, description: str) -> dict[str, Any]:
    return {
        "description": description,
        "content": {
            "application/problem+json": {
                "schema": {"$ref": f"#/components/schemas/{schema_name}"},
            }
        },
    }


def build_runtime_problem_inventory(node: Any, *, include_direct: bool = True) -> list[dict[str, Any]]:
    inventory: dict[tuple[Any, ...], dict[str, Any]] = {}

    def add_problem(value: dict[str, Any]) -> None:
        required = ("status", "category", "code", "retryable", "clientAction")
        if not all(key in value for key in required):
            return
        if isinstance(value.get("details"), dict):
            detail_keys = sorted(value["details"])
        elif isinstance(value.get("detailKeys"), list) and all(
            isinstance(key, str) for key in value["detailKeys"]
        ):
            detail_keys = sorted(value["detailKeys"])
        else:
            return
        entry = {
            "status": value["status"],
            "category": value["category"],
            "code": value["code"],
            "retryable": value["retryable"],
            "clientAction": value["clientAction"],
            "detailKeys": detail_keys,
        }
        key = (
            entry["status"],
            entry["category"],
            entry["code"],
            entry["retryable"],
            entry["clientAction"],
            tuple(entry["detailKeys"]),
        )
        inventory[key] = entry

    def visit(value: Any) -> None:
        if isinstance(value, dict):
            add_problem(value)
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(node)
    if include_direct:
        for problem in DIRECT_RUNTIME_PROBLEMS:
            add_problem({**problem, "details": {key: "inventory" for key in problem["detailKeys"]}})
    return [inventory[key] for key in sorted(inventory, key=lambda item: tuple(str(part) for part in item))]


def transform(source: dict[str, Any], matrix: dict[str, dict[str, Any]]) -> dict[str, Any]:
    contract = copy.deepcopy(source)
    components = contract["components"]
    contract["info"]["version"] = "v2"
    contract["info"]["summary"] = "Non-routed PD10 v2 authorization Contract Spine candidate."
    contract["info"]["description"] = (
        "Digest-bound PD10 v2 candidate for Hexalith.Folders. Authentication and fresh authority are "
        "established before every protected observation. This document is generated for A6b review and is "
        "not selected by the supported production profile."
    )
    contract["servers"] = [{"url": "/", "description": "Candidate-only surface; not production-routed before A6b, Section 9, and A8."}]

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
            transform_operation(candidate_operation, operation_id, expected, components)
            candidate_path_item = generated_paths.setdefault(expected["path"], {})
            if method in candidate_path_item:
                existing_id = candidate_path_item[method].get("operationId", "unknown")
                raise ValueError(
                    f"Duplicate generated route {method.upper()} {expected['path']}: "
                    f"{existing_id} and {operation_id}."
                )
            candidate_path_item[method] = candidate_operation
            observed.add(operation_id)
    missing = sorted(set(matrix) - observed)
    if missing:
        raise ValueError(f"Matrix operations missing from historical Spine: {', '.join(missing)}")
    contract["paths"] = generated_paths

    responses = components["responses"]
    responses.pop("SafeAuthorizationDenial403", None)
    responses["AuthenticationFailure401"] = exact_problem_response(
        "AuthenticationFailureProblem",
        "Authentication failed before protected observation.",
    )
    responses["SafeDenial404"] = exact_problem_response(
        "SafeDenialProblem",
        "Non-enumerating denial emitted before protected observation.",
    )
    responses["ProtectedOperationUnavailable503"] = exact_problem_response(
        "AuthorityUnavailableProblem",
        "Authority evidence is stale, unavailable, conflicting, or incomplete. No protected lookup has occurred.",
    )
    responses.pop("AuthorityUnavailable503", None)
    components["examples"].pop("SafeDenial403Forbidden", None)
    for schema_name, values in EXACT_AUTHORIZATION_PROBLEMS.items():
        components["schemas"][schema_name] = exact_problem_schema(values)
        example_name = schema_name.removesuffix("Problem")
        components["examples"][example_name] = {
            "summary": "Canonical non-disclosing authorization response emitted before protected lookup.",
            "value": {
                **{key: value for key, value in values.items() if key != "visibility"},
                "correlationId": "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
                "details": {"visibility": values["visibility"]},
            },
        }

    principal_mismatch = components["examples"].get("PrincipalMismatchSafeDenialProblem", {}).get("value")
    if isinstance(principal_mismatch, dict):
        canonical = EXACT_AUTHORIZATION_PROBLEMS["SafeDenialProblem"]
        principal_mismatch.update(
            {key: value for key, value in canonical.items() if key != "visibility"}
        )
        principal_mismatch["details"] = {"visibility": canonical["visibility"]}

    categories = components["schemas"]["CanonicalErrorCategory"]["enum"]
    if "concurrency_conflict" not in categories:
        categories.insert(categories.index("idempotency_conflict"), "concurrency_conflict")

    mcp_failure_kind_schema = components["schemas"]["McpFailureKind"]["enum"]
    if "concurrency_conflict" not in mcp_failure_kind_schema:
        mcp_failure_kind_schema.insert(mcp_failure_kind_schema.index("idempotency_conflict"), "concurrency_conflict")

    cli_exit_codes = components["schemas"]["CliExitCode"]["enum"]
    if "77" not in cli_exit_codes:
        cli_exit_codes.append("77")

    problem_schema = components["schemas"]["ProblemDetails"]
    problem_schema["additionalProperties"] = False
    problem_schema["properties"]["clientAction"]["enum"] = CLIENT_ACTION_VALUES

    normalize_problem_examples(components.get("examples", {}))
    normalize_problem_examples(contract.get("paths", {}))
    operation_inventories: list[list[dict[str, Any]]] = []
    for path_item in contract["paths"].values():
        for operation in path_item.values():
            if not isinstance(operation, dict) or "operationId" not in operation:
                continue
            inventory = operation_problem_inventory(contract, operation)
            operation["x-hexalith-runtime-problem-inventory"] = inventory
            operation_inventories.append(inventory)
    runtime_problem_inventory = build_runtime_problem_inventory(
        operation_inventories,
        include_direct=True,
    )
    canonical_categories = components["schemas"]["CanonicalErrorCategory"]["enum"]
    components["schemas"]["CanonicalErrorCategory"]["enum"] = sorted(
        set(canonical_categories) | {item["category"] for item in runtime_problem_inventory}
    )

    details_schema = problem_schema["properties"]["details"]
    detail_keys: set[str] = set(RUNTIME_DETAIL_KEYS)
    collect_detail_keys(components.get("examples", {}), detail_keys)
    collect_detail_keys(contract.get("paths", {}), detail_keys)
    details_schema["required"] = ["visibility"]
    details_schema["additionalProperties"] = False
    details_schema["properties"] = {
        key: ({"type": "string", "enum": VISIBILITY_VALUES} if key == "visibility" else {"type": "string"})
        for key in sorted(detail_keys)
    }
    details_schema["description"] = (
        "Closed metadata-only details. Every error requires visibility; keys outside the generated vocabulary are forbidden."
    )

    remove_forbidden_enum_values(contract)
    replace_request_schema_versions(contract)

    error_codes: set[str] = {
        "authentication_required",
        "resource_unavailable",
        "projection_unavailable",
        "concurrency_conflict",
    }
    collect_error_codes(components.get("examples", {}), error_codes)
    collect_error_codes(contract.get("paths", {}), error_codes)
    error_codes.update(item["code"] for item in runtime_problem_inventory)
    components["schemas"]["CanonicalErrorCode"] = {
        "type": "string",
        "enum": sorted(error_codes),
    }
    canonical_error_code_ref = {"$ref": "#/components/schemas/CanonicalErrorCode"}
    problem_schema["properties"]["code"] = copy.deepcopy(canonical_error_code_ref)
    components["schemas"]["ExactFileProblem"]["properties"]["code"] = copy.deepcopy(canonical_error_code_ref)

    authority_branch = {"$ref": AUTHORITY_UNAVAILABLE_SCHEMA_REF}
    for union_name in ("FileMutationUnavailableProblem", "FileContextUnavailableProblem"):
        union = components["schemas"][union_name].setdefault("oneOf", [])
        if authority_branch not in union:
            union.append(copy.deepcopy(authority_branch))

    for legacy_schema_name in (
        "FileLegacyMutationReconciliationRequiredProblem",
        "FileLegacyContextReadModelUnavailableProblem",
    ):
        legacy_schema = components["schemas"][legacy_schema_name]
        authority_exclusion = {"not": {"$ref": AUTHORITY_UNAVAILABLE_SCHEMA_REF}}
        all_of = legacy_schema.setdefault("allOf", [])
        if authority_exclusion not in all_of:
            all_of.append(authority_exclusion)
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
    contract["x-hexalith-runtime-problem-inventory"] = runtime_problem_inventory
    return contract


def validate_declared_examples(contract: dict[str, Any]) -> None:
    for path, path_item in contract["paths"].items():
        for method, operation in path_item.items():
            if method not in HTTP_METHODS or not isinstance(operation, dict):
                continue
            for status, response in operation.get("responses", {}).items():
                if "$ref" in response:
                    response = resolve_local_reference(contract, response["$ref"])
                media_type = response.get("content", {}).get("application/problem+json")
                if not isinstance(media_type, dict):
                    continue
                schema = media_type.get("schema")
                for example_name, example in media_type.get("examples", {}).items():
                    if "$ref" in example:
                        example = resolve_local_reference(contract, example["$ref"])
                    resolved_schema = resolve_schema(contract, schema)
                    errors = sorted(
                        Draft202012Validator(resolved_schema).iter_errors(example["value"]),
                        key=lambda error: list(error.absolute_path),
                    )
                    if errors:
                        raise ValueError(
                            f"Example {operation['operationId']}.{status}.{example_name} does not validate "
                            f"against its fully resolved schema: {errors[0].message}"
                        )


def resolve_local_reference(contract: dict[str, Any], reference: str) -> Any:
    if not reference.startswith("#/"):
        raise ValueError(f"Only local references are supported during candidate validation: {reference!r}.")
    value: Any = contract
    for segment in reference[2:].split("/"):
        value = value[segment.replace("~1", "/").replace("~0", "~")]
    return value


def resolve_schema(contract: dict[str, Any], node: Any) -> Any:
    if isinstance(node, dict):
        if "$ref" in node:
            resolved = copy.deepcopy(resolve_local_reference(contract, node["$ref"]))
            siblings = {key: value for key, value in node.items() if key != "$ref"}
            if siblings:
                resolved = {"allOf": [resolved, siblings]}
            return resolve_schema(contract, resolved)
        return {key: resolve_schema(contract, value) for key, value in node.items()}
    if isinstance(node, list):
        return [resolve_schema(contract, item) for item in node]
    return node


def main() -> int:
    arguments = parse_arguments()
    if arguments.output.resolve() == arguments.source.resolve():
        raise ValueError("Candidate output must not alias the immutable historical v1 source.")
    if arguments.output.resolve() == arguments.matrix.resolve():
        raise ValueError("Candidate output must not alias the authorization matrix input.")
    source = yaml.safe_load(arguments.source.read_text(encoding="utf-8"))
    matrix = read_matrix(arguments.matrix)
    candidate = transform(source, matrix)
    validate_declared_examples(candidate)
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
