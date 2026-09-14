# File Mutation And Context Query Contract Groups

Status: OQ2 file-policy decision approved; downstream runtime evidence remains incomplete.

Policy version: `1.1.0`

Approved on: `2026-09-14`

Approved by: `Administrator` for PM, Architecture, and Security

Canonical policy artifact: `docs/contract/file-context-contract-groups.md`

Governance evidence: `docs/contract/oq2-file-policy-evidence.yaml`

This contract-only document is the canonical OQ2 file-policy decision. The OpenAPI Contract Spine remains the
machine-readable surface contract and must reproduce this policy without weakening it. The policy is a
governed design decision; it does not claim that file/workspace/provider/query runtime behavior or
FR32-FR35 production evidence exists.

## Reuse Points

- Story 1.10 must reuse the file mutation `operationId`, task, workspace, path metadata, retry eligibility, unknown outcome, and reconciliation vocabulary when commit and workspace status contracts are authored.
- Story 1.11 must reuse the metadata-only audit keys and safe-denial behavior for audit timeline contracts.
- Stories 12.1 and 12.3 own the durable event, state, content, and content-read prerequisites; Story 4.20 owns deployed mutation and bounded-context lifecycle proof.
- Epic 4 owns runtime file mutation behavior, prepared-workspace checks, held-lock enforcement, path policy execution, context-query execution, provider/Git/filesystem side effects, and reconciliation.
- Epic 5 owns SDK, CLI, and MCP behavioral parity over these operation groups.
- Story 6.6 must reuse the same metadata-only context-query and workspace diagnostic labels in the read-only operations console.

## Mandatory Evaluation Order And Atomicity

Every path-bearing operation preserves authorization and path policy before observation. Context queries
use this contract-binding order:

```text
tenant_access -> folder_acl -> path_policy -> sensitivity_classification -> c4_bounds -> query_execution
```

No implementation may stat, open, resolve, follow, scan, count, rank, filter, truncate, or shape a candidate
before the authorization and path-policy stages that protect it. Search-first/filter-later and
retrieval-first/filter-later are forbidden.

A mutation change set is validated as one atomic unit before any change is applied. If any path, link,
policy, content, count, aggregate-size, authorization, workspace, or lock check fails, no part of the change
set is applied and workspace/lock state remains unchanged. Diagnostics are bounded and metadata-only.

## Canonical Path Profile

`PathMetadata.normalizedPath` carries the caller's exact accepted spelling; the name does not authorize the
server to lowercase, case-fold, separator-convert, Unicode-rewrite, alias-resolve, or otherwise retarget it.
An accepted path has all of these properties:

- It is a non-empty workspace-root-relative path of at most 500 characters, with no leading or trailing slash.
- Its only characters are ASCII `A-Z a-z 0-9 . _ - /`; `/` is the only separator and empty segments are rejected.
- It is already NFC. Because version 1.1.0 permits ASCII only, NFC does not transform accepted input; the explicit declaration prevents a future Unicode profile from silently changing identity.
- `.` and `..` path segments, absolute paths, drive-qualified paths, UNC forms, backslashes, control characters, and empty segments are rejected.
- Windows device base names `CON`, `PRN`, `AUX`, `NUL`, `COM1` through `COM9`, and `LPT1` through `LPT9`, with or without an extension and in any ASCII case, are rejected as a complete segment.

The server compares full accepted paths with ordinal-ignore-case semantics against the existing namespace and
every other path touched by the same change set. Distinct spellings that compare equal are an ambiguous case
alias: the whole operation is rejected before observation, and neither spelling is selected as canonical.

For every touched path, the server inspects the touched entry and each existing ancestor with no-follow
filesystem operations. A symbolic link, junction, mount-point reparse entry, or any other reparse entry in
that chain rejects the whole operation. The server never follows the entry to learn or report its target.
For an add whose leaf does not yet exist, every existing ancestor is still checked. Rejection never echoes
the path, target, ancestor, or existence evidence.

Structural path failures use the canonical `path_validation_failed` family with metadata-only diagnostics.
They are distinct from policy-hidden paths, whose public routing is defined below.

## Server-Owned Policy Vocabulary And Precedence

`PathMetadata.pathPolicyClass` has exactly four server-owned values:

| Class | Meaning | Mutation | Metadata query | Content query |
| --- | --- | --- | --- | --- |
| `content_allowed` | The path is included, not excluded or restricted, sensitivity permits it, and its bytes satisfy the content-readable rule. | Allowed within all mutation bounds. | Allowed. | Allowed within C4 bounds. |
| `metadata_only` | The path is included and mutation-eligible, but content is binary, uses another encoding, is oversized for content reading, or policy permits metadata only. | Allowed within all mutation bounds. | Allowed without content, snippet, or content-derived evidence. | Denied with the canonical 404 safe-denial envelope. |
| `excluded` | The path did not match the required include allowlist or an exclusion matched it. | Denied; apply nothing. | Omitted from collection results; a direct target is denied. | Denied. |
| `restricted` | A restriction or sensitivity rule denies the caller access to the path. | Denied; apply nothing. | Omitted from collection results; a direct target is denied. | Denied. |

The active policy must contain a finite, server-bounded, non-empty include allowlist. Include and exclude
rules are workspace-root anchored and compared with ordinal-ignore-case semantics. A policy contains 1 through
100 combined rules; each rule contains 1 through 256 characters and the complete rule set contains at most
25,600 characters. `/` separates segments, `*` matches zero or more characters within one segment, `?`
matches exactly one non-slash character, and `**` is valid only as a complete segment where it matches zero
or more complete segments. Escapes, negation, character classes, braces, and re-inclusion are unsupported.

An invalid, empty, oversized, stale, unreadable, or unavailable policy fails closed and cannot be replaced by
a permissive default. A path must match an include entry. Exclusions are then evaluated and always win: a
later include never reverses an exclusion. Restriction and sensitivity checks run after include/exclude
classification and can only reduce visibility. Callers cannot select, override, or upgrade a class; any class
value carried through a request is revalidated against the active server policy.

Every path component is checked against the existing namespace with ordinal-ignore-case semantics. An alias
at any ancestor or leaf is rejected rather than retargeted. A segment ending in a space or dot is rejected.
The `.git` entry and all of its descendants are always `restricted`, in every ASCII case.

## Content And Mutation Bounds

The D-9 inline transport boundary remains 262,144 decoded bytes. Add/change content from 0 through 262,144
bytes uses `PutFileInline`; content from 262,145 through 1,048,576 bytes uses `PutFileStream`. A file mutation
over 1,048,576 bytes is rejected without truncation. A canonical internal `MutateFilesRequest` contains 1
through 100 caller-ordered `FileMutationRequest` items. Operation IDs and paths are unique under
ordinal-ignore-case comparison. The complete request contains at most 10,485,760 aggregate add/change bytes;
removes contribute zero bytes. Every validation or policy failure rejects the complete batch before execution.
The public AddFile, ChangeFile, and RemoveFile operations remain matching one-item adapters and do not expose
the internal batch request as a new public operation.

Declared and observed stream lengths and content hashes must agree; descriptor-only stream evidence is
insufficient. Inline content must decode as valid base64 to at most 262,144 bytes. Its decoded length must equal
`byteLength`, and its trusted observed content-hash reference must equal `contentHashReference`. Malformed
base64 or mismatched inline evidence is rejected before acceptance.

Content is readable only when the authoritative file is at most 1,048,576 bytes and its exact bytes decode
with strict UTF-8 validation, with either no byte-order mark or one UTF-8 BOM at byte zero. After decoding, the
only permitted control characters are tab, carriage return, and line feed. Invalid byte sequences, another
encoding, a disallowed control character, binary content, or content above the readable limit remains
mutation-eligible within mutation limits but is classified `metadata_only`. An authoritative visible directory
is always `metadata_only`. The server preserves exact mutation bytes; it does not transcode, repair, replace
invalid sequences, strip bytes, or silently truncate. Media type is metadata and cannot upgrade non-readable
bytes to `content_allowed`.

Collection tree/metadata/glob results may contain only visible `content_allowed` and `metadata_only` entries.
Excluded, restricted, sensitivity-denied, and unauthorized entries are removed before ordering, counts,
pagination, truncation, or response shaping, so hidden counts are not disclosed. Body search and range reads
operate only on `content_allowed` files. No metadata-only result contains file bytes, snippets, decoded text,
content hashes derived for display, or other content evidence.

A direct metadata request that names multiple paths is all-or-nothing for visibility. If any target is hidden
or missing, the complete request returns the canonical 404 outcome and no visible subset, path count, or other
partial result.

## Safe-Denial And Range Routing

Missing, excluded, restricted, sensitivity-denied, and unauthorized paths have one public outcome: HTTP 404
with `category: tenant_access_denied`, `code: resource_unavailable`, the approved safe message, and redacted
details. The envelope is identical across those causes apart from approved current-request correlation fields.
It does not expose a path, policy rule, class, existence bit, count, byte length, encoding, sensitivity, link
target, or authorization reason.

HTTP 416 is reserved for a caller that is already authorized for a visible `content_allowed` path when the
requested range is unsatisfiable. A sensitivity, policy, visibility, or authorization denial never uses 416.
Ranges are half-open. `[EOF,EOF)` succeeds with HTTP 200 and no bytes. A start beyond EOF, including an empty
range beyond EOF, and a non-empty range starting at EOF are unsatisfiable and return HTTP 416. A range starting
before EOF whose exclusive end extends beyond EOF returns the available authorized bytes with HTTP 206. The C4
262,144-byte window remains an input-bound check and is never silently truncated.

One immutable policy version and digest are pinned for the complete evaluation. If the active policy changes
before atomic mutation apply or before response shaping, the evaluated result is discarded and the operation
returns no mutation or disclosure. Drift, an unavailable policy, or inability to verify the pinned snapshot
uses HTTP 503 with `category: file_policy_unavailable`, `code: file_policy_unavailable`, `retryable: true`, and
`clientAction: retry`. This envelope is metadata-only and does not disclose a path, rule, class, identity,
count, existence bit, or prior evaluated result.

Content failure precedence is exact. Decoded inline or observed streamed content above 1,048,576 bytes returns
HTTP 422 with `category: input_limit_exceeded`, `code: file_content_limit_exceeded`, `retryable: false`, and
`clientAction: revise_request`, before inline-transport routing. Inline content from 262,145 through 1,048,576
bytes returns HTTP 413 with the retry-as-stream hint. Malformed base64 or decoded-length/trusted-hash mismatch
returns HTTP 400 with `category: validation_error`, `code: content_evidence_invalid`, `retryable: false`, and
`clientAction: revise_request`.

## D-9 Transport Headers

The request-side header `X-Hexalith-Retry-As: [caller, operator]` signals retry allocation. The response-side
header `X-Hexalith-Retry-Transport: [stream]` signals transport substitution and is emitted with
`413 Payload Too Large` on inline file mutations. The names are deliberately disjoint so a caller echoing back
a response value cannot trip request-side validation. The 413 problem carries the exact canonical
category/code/retryability/action values without surfacing the configured byte limit, keeping the response
safe for pre-authentication callers.

## Governance And Reopen Rule

`docs/contract/oq2-file-policy-evidence.yaml` binds policy version `1.1.0` and the SHA-256 digest of this LF-stable
artifact to exactly one PM, one Architecture, and one Security approval by Administrator dated 2026-09-14.
Any policy-content, policy-version, digest, authority, signer, or approval-date change reopens OQ2 until all
three authorities record fresh approval for the new version and digest. Governance approval closes the design
decision only; Stories 12.1, 12.3, and 4.20 and FR32-FR35 runtime proof remain incomplete.

## Unrelated Deferred Decisions

- 429 provider rate-limit response shape and `Retry-After` semantics remain deferred to Epic 4 runtime work.
- Semantic indexing and RAG retrieval remain downstream Memories integration work; OQ2 defines file-policy eligibility but does not implement indexing.
- Runtime file/workspace/provider/query behavior, OQ3, and broad PD10 cleanup remain owned by their downstream work.
- OQ2 permits only focused existing CLI/MCP policy-class validation and typed-error projection changes; it adds no command or tool.

## Negative Scope

This OQ2 closure does not add REST handlers, EventStore commands, domain aggregate behavior, content or
workspace stores, provider adapters, Git or filesystem side effects, query handlers, public operations, CLI
commands, MCP tools, workers, UI pages, repair automation, or nested-submodule initialization. Generated SDK
types/helpers and focused existing CLI/MCP validation/error projection may change only to reproduce this
contract. This closure does not claim FR32-FR35 runtime completion.
